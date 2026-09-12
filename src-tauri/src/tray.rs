//! トレイアイコン（doc/ARCHITECTURE.md 第 8.3 節、doc/DESIGN.md 第 10 節）。

use tauri::image::Image;
use tauri::menu::{Menu, MenuItem};
use tauri::tray::{MouseButton, TrayIconBuilder, TrayIconEvent};
use tauri::{AppHandle, Manager};

use shelfy::adapters::windows::{small_icon_size, taskbar_uses_light_theme, ThemeSettingsWatch};
use shelfy::ports::AppLogger;

use crate::state::AppState;

const TRAY_ID: &str = "main";

pub fn setup(app: &mut tauri::App) -> tauri::Result<()> {
    let show_item = MenuItem::with_id(app, "show", "Shelfy を表示", true, None::<&str>)?;
    let quit_item = MenuItem::with_id(app, "quit", "終了", true, None::<&str>)?;
    let menu = Menu::with_items(app, &[&show_item, &quit_item])?;

    TrayIconBuilder::with_id(TRAY_ID)
        .icon(icon_for(taskbar_uses_light_theme(), small_icon_size()))
        .tooltip("Shelfy")
        .menu(&menu)
        .show_menu_on_left_click(false)
        .on_menu_event(|app, event| match event.id.as_ref() {
            "show" => crate::show_main_window(app),
            "quit" => {
                if let Some(state) = app.try_state::<AppState>() {
                    state.flush();
                    state.logger.info("終了します");
                }
                app.exit(0);
            }
            _ => {}
        })
        .on_tray_icon_event(|tray, event| {
            if let TrayIconEvent::DoubleClick {
                button: MouseButton::Left,
                ..
            } = event
            {
                crate::show_main_window(tray.app_handle());
            }
        })
        .build(app)?;

    follow_taskbar_theme(app.handle().clone());
    Ok(())
}

/// タスクバーのテーマに合う単色のアイコン。
///
/// 画像は tools/export-icons.ps1 が SVG から書き出した生の RGBA である。
/// PNG で持つと、実行時に復号器を抱えることになる。
fn icon_for(light_taskbar: bool, size: u32) -> Image<'static> {
    // 表示倍率 125% の 20px などは、一つ上の大きさを OS に縮めてもらう
    let side = match size {
        0..=16 => 16,
        17..=24 => 24,
        _ => 32,
    };
    let rgba: &'static [u8] = match (light_taskbar, side) {
        (true, 16) => include_bytes!("../icons/tray/light-16.rgba"),
        (true, 24) => include_bytes!("../icons/tray/light-24.rgba"),
        (true, _) => include_bytes!("../icons/tray/light-32.rgba"),
        (false, 16) => include_bytes!("../icons/tray/dark-16.rgba"),
        (false, 24) => include_bytes!("../icons/tray/dark-24.rgba"),
        (false, _) => include_bytes!("../icons/tray/dark-32.rgba"),
    };
    Image::new(rgba, side, side)
}

/// タスクバーのテーマが変わったら、トレイのアイコンを差し替える。
///
/// ウィンドウのテーマ変更の通知は、アプリのテーマが変わったときにしか届かない。
/// タスクバーだけを切り替えた場合も拾うため、設定のレジストリを直接見張る。
fn follow_taskbar_theme(app: AppHandle) {
    let Some(watch) = ThemeSettingsWatch::open() else {
        if let Some(state) = app.try_state::<AppState>() {
            state
                .logger
                .warn("テーマの設定を見張れません。トレイのアイコンは起動時のテーマのままです。");
        }
        return;
    };

    std::thread::spawn(move || {
        let mut light = taskbar_uses_light_theme();
        // 同じキーにあるアプリのテーマや透明効果の変更でも起きるので、値を比べてから差し替える
        while watch.wait() {
            let now = taskbar_uses_light_theme();
            if now == light {
                continue;
            }
            light = now;
            if let Some(tray) = app.tray_by_id(TRAY_ID) {
                let _ = tray.set_icon(Some(icon_for(light, small_icon_size())));
            }
        }
    });
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn every_tray_image_matches_its_declared_size() {
        for light in [true, false] {
            for (requested, side) in [(16, 16), (20, 24), (24, 24), (32, 32), (48, 32)] {
                let image = icon_for(light, requested);
                assert_eq!(image.width(), side);
                assert_eq!(image.height(), side);
                assert_eq!(image.rgba().len(), (side * side * 4) as usize);
            }
        }
    }

    #[test]
    fn the_two_themes_use_different_images() {
        assert_ne!(icon_for(true, 16).rgba(), icon_for(false, 16).rgba());
    }
}
