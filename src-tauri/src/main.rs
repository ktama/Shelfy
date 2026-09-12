//! 組み立てと起動（doc/ARCHITECTURE.md 第 10 節）。
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

mod commands;
mod existence_check;
mod mutations;
mod settings;
mod state;
mod tray;

use std::str::FromStr;
use std::time::{Duration, Instant};

use tauri::window::{Effect, EffectsBuilder};
use tauri::{AppHandle, Emitter, Manager, WindowEvent};
use tauri_plugin_global_shortcut::{Shortcut, ShortcutState};

use shelfy::adapters::store::{StorePaths, DEFAULT_SAVE_DELAY};
use shelfy::adapters::windows::webview2_version;
use shelfy::ports::AppLogger;
use shelfy::usecases::settings::{read_settings, Settings};

use crate::state::{AppState, WindowEffects};

/// 未保存の変更を書き出すか確かめる間隔
const SAVE_TICK: Duration = Duration::from_millis(250);

fn main() {
    let started = Instant::now();

    // この構成で唯一、実行環境に前提を置く箇所。黙って落ちないよう先に確かめる。
    if webview2_version().is_none() {
        show_message(
            "Shelfy を起動できません",
            "WebView2 ランタイムが見つかりません。\n\n\
             Windows 11 には標準で入っています。\n\
             入っていない場合は、Microsoft の配布ページから\n\
             「Microsoft Edge WebView2 ランタイム」を導入してください。",
        );
        return;
    }

    let paths = match StorePaths::default_location() {
        Ok(paths) => paths,
        Err(e) => {
            show_message(
                "Shelfy を起動できません",
                &format!("データの置き場所を用意できませんでした。\n\n{e}"),
            );
            return;
        }
    };

    let app_state = AppState::open(paths);
    let hotkey_spec = app_state.hotkey_spec();
    let settings = read_settings(app_state.store.as_ref());
    let start_minimized = settings.start_minimized;
    let (window_width, window_height) = (settings.window_width, settings.window_height);

    let shortcut = Shortcut::from_str(&hotkey_spec.to_string_normalized())
        .unwrap_or_else(|_| Shortcut::from_str(&Settings::default().global_hotkey).unwrap());

    let result = tauri::Builder::default()
        .plugin(tauri_plugin_single_instance::init(|app, _argv, _cwd| {
            show_main_window(app);
        }))
        .plugin(tauri_plugin_dialog::init())
        .plugin(
            tauri_plugin_global_shortcut::Builder::new()
                // 登録するホットキーは 1 つだけなので、どれが押されたかは見ない。
                // 設定で入れ替えたあとも、この判定のままで動く。
                .with_handler(move |app, _pressed, event| {
                    if event.state() == ShortcutState::Pressed {
                        toggle_main_window(app);
                    }
                })
                .build(),
        )
        .manage(app_state)
        .invoke_handler(tauri::generate_handler![
            commands::startup_info,
            commands::load_shelves,
            commands::load_items,
            commands::search,
            commands::recent_items,
            commands::missing_items,
            commands::launch,
            commands::open_parent,
            commands::hide_window,
            mutations::create_shelf,
            mutations::rename_shelf,
            mutations::move_shelf,
            mutations::delete_shelf,
            mutations::toggle_pin_shelf,
            mutations::reorder_shelves,
            mutations::add_items,
            mutations::add_url,
            mutations::remove_item,
            mutations::rename_item,
            mutations::update_item_memo,
            mutations::move_item_to_shelf,
            mutations::reorder_items,
            mutations::export_data,
            mutations::import_data,
            settings::save_settings,
            existence_check::check_existence,
        ])
        .setup(move |app| {
            let handle = app.handle().clone();
            let window = app.get_webview_window("main").expect("main window");

            // Mica を試す。適用できない環境では画面側が不透明な背景に落ちる。
            let mica = window
                .set_effects(EffectsBuilder::new().effect(Effect::Mica).build())
                .is_ok();
            // 適用できたかを画面へ伝える。画面はこれを見て背景を透かす（第 5.3 節）。
            app.manage(WindowEffects { mica });

            tray::setup(app)?;
            register_hotkey(app, &shortcut);

            // 閉じる操作は終了ではなく非表示にする
            let for_close = handle.clone();
            window.on_window_event(move |event| {
                if let WindowEvent::CloseRequested { api, .. } = event {
                    api.prevent_close();
                    settings::remember_window_size(&for_close);
                    if let Some(state) = for_close.try_state::<AppState>() {
                        state.flush();
                    }
                    commands::hide_main_window(&for_close);
                }
            });

            // 未保存の変更をまとめて書き出す
            let for_save = handle.clone();
            std::thread::spawn(move || loop {
                std::thread::sleep(SAVE_TICK);
                if let Some(state) = for_save.try_state::<AppState>() {
                    if let Err(e) = state.store.flush_if_due(DEFAULT_SAVE_DELAY) {
                        state.logger.error(&format!("保存に失敗しました: {e}"));
                    }
                }
            });

            let state = app.state::<AppState>();
            state.logger.info(&format!(
                "起動しました（{} ms、Mica: {}）",
                started.elapsed().as_millis(),
                if mica { "適用" } else { "未適用" }
            ));

            // 保存されている大きさに戻す
            let _ = window.set_size(tauri::LogicalSize::new(window_width, window_height));

            // ウィンドウは作り切ってから隠しておく。呼び出しでは表示を切り替えるだけにする。
            if !start_minimized {
                let _ = window.show();
                let _ = window.set_focus();
            }
            Ok(())
        })
        .run(tauri::generate_context!());

    if let Err(e) = result {
        show_message(
            "Shelfy を起動できません",
            &format!("画面を作れませんでした。\n\n{e}"),
        );
    }
}

fn register_hotkey(app: &tauri::App, shortcut: &Shortcut) {
    use tauri_plugin_global_shortcut::GlobalShortcutExt;
    let state = app.state::<AppState>();
    if let Err(e) = app.global_shortcut().register(*shortcut) {
        state.logger.warn(&format!(
            "ホットキーを登録できませんでした（他のアプリが使っている可能性があります）: {e}"
        ));
        let _ = app.emit(
            "hotkey-unavailable",
            "ホットキーを登録できませんでした。トレイアイコンから呼び出せます。",
        );
    }
}

fn show_main_window(app: &AppHandle) {
    if let Some(window) = app.get_webview_window("main") {
        let _ = window.show();
        let _ = window.unminimize();
        let _ = window.set_focus();
        let _ = app.emit("window-shown", ());
    }
}

fn toggle_main_window(app: &AppHandle) {
    let Some(window) = app.get_webview_window("main") else {
        return;
    };
    if window.is_visible().unwrap_or(false) {
        settings::remember_window_size(app);
        if let Some(state) = app.try_state::<AppState>() {
            state.flush();
        }
        let _ = window.hide();
    } else {
        show_main_window(app);
    }
}

/// 画面を作れないときに使う、OS 標準の案内
fn show_message(title: &str, body: &str) {
    use std::ffi::OsStr;
    use std::os::windows::ffi::OsStrExt;
    use windows_sys::Win32::UI::WindowsAndMessaging::{MessageBoxW, MB_ICONERROR, MB_OK};

    let to_wide = |s: &str| -> Vec<u16> {
        OsStr::new(s)
            .encode_wide()
            .chain(std::iter::once(0))
            .collect()
    };
    let title = to_wide(title);
    let body = to_wide(body);
    unsafe {
        MessageBoxW(
            std::ptr::null_mut(),
            body.as_ptr(),
            title.as_ptr(),
            MB_OK | MB_ICONERROR,
        );
    }
}
