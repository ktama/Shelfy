//! 設定の保存と、その場での反映（doc/SPECIFICATION.md 第 8 節）。

use std::str::FromStr;

use serde::Deserialize;
use tauri::{LogicalSize, Manager, State};
use tauri_plugin_global_shortcut::{GlobalShortcutExt, Shortcut};

use shelfy::hotkey;
use shelfy::ports::{settings_keys, AppLogger, SettingsRepository};

use crate::mutations::SimpleResult;
use crate::state::AppState;

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SettingsInput {
    pub global_hotkey: String,
    pub window_width: f64,
    pub window_height: f64,
    pub start_minimized: bool,
    pub recent_items_count: usize,
}

#[tauri::command]
pub fn save_settings(
    app: tauri::AppHandle,
    state: State<'_, AppState>,
    settings: SettingsInput,
) -> SimpleResult {
    // ホットキーは、解析できたときだけ差し替える
    let Some(spec) = hotkey::parse(&settings.global_hotkey) else {
        return SimpleResult {
            ok: false,
            message: Some("ホットキーの形が読めません。例: Ctrl+Shift+Space".into()),
        };
    };
    if !spec.has_modifier() {
        return SimpleResult {
            ok: false,
            message: Some("Ctrl、Shift、Alt、Win のいずれかを含めてください。".into()),
        };
    }

    let normalized = spec.to_string_normalized();
    let previous = SettingsRepository::get(state.store.as_ref(), settings_keys::GLOBAL_HOTKEY);
    let mut note: Option<String> = None;

    if previous.as_deref() != Some(normalized.as_str()) {
        match Shortcut::from_str(&normalized) {
            Ok(shortcut) => {
                // 登録は 1 つだけなので、まとめて外してから入れ直す
                let _ = app.global_shortcut().unregister_all();
                match app.global_shortcut().register(shortcut) {
                    Ok(()) => state.hotkey_hold.set(spec),
                    Err(e) => {
                        state
                            .logger
                            .warn(&format!("ホットキーを登録できませんでした: {e}"));
                        note = Some(
                            "ホットキーを登録できませんでした。他のアプリが使っている可能性があります。"
                                .into(),
                        );
                        // 元の設定へ戻しておく
                        if let Some(old) =
                            previous.as_deref().and_then(|t| Shortcut::from_str(t).ok())
                        {
                            let _ = app.global_shortcut().register(old);
                        }
                    }
                }
            }
            Err(_) => {
                return SimpleResult {
                    ok: false,
                    message: Some("このキーの組み合わせは登録できません。".into()),
                }
            }
        }
    }

    let store = state.store.as_ref();
    store.set(settings_keys::GLOBAL_HOTKEY, &normalized);
    store.set(
        settings_keys::WINDOW_WIDTH,
        &format!("{:.0}", settings.window_width),
    );
    store.set(
        settings_keys::WINDOW_HEIGHT,
        &format!("{:.0}", settings.window_height),
    );
    store.set(
        settings_keys::START_MINIMIZED,
        if settings.start_minimized {
            "true"
        } else {
            "false"
        },
    );
    store.set(
        settings_keys::RECENT_ITEMS_COUNT,
        &settings.recent_items_count.to_string(),
    );

    // ウィンドウの大きさは、その場で反映する
    if let Some(window) = app.get_webview_window("main") {
        let _ = window.set_size(LogicalSize::new(
            settings.window_width,
            settings.window_height,
        ));
    }

    state.flush();

    SimpleResult {
        ok: true,
        message: Some(note.unwrap_or_else(|| "設定を保存しました".into())),
    }
}

/// ウィンドウを隠す前に、そのときの大きさを覚えておく。
/// 最大化中と最小化中は、戻したときの大きさが分からないので保存しない。
pub fn remember_window_size(app: &tauri::AppHandle) {
    let Some(window) = app.get_webview_window("main") else {
        return;
    };
    if window.is_maximized().unwrap_or(false) || window.is_minimized().unwrap_or(false) {
        return;
    }
    let Some(state) = app.try_state::<AppState>() else {
        return;
    };
    let Ok(size) = window.inner_size() else {
        return;
    };
    let scale = window.scale_factor().unwrap_or(1.0);
    let logical = size.to_logical::<f64>(scale);
    if logical.width < 1.0 || logical.height < 1.0 {
        return;
    }

    let store = state.store.as_ref();
    store.set(
        settings_keys::WINDOW_WIDTH,
        &format!("{:.0}", logical.width),
    );
    store.set(
        settings_keys::WINDOW_HEIGHT,
        &format!("{:.0}", logical.height),
    );
}
