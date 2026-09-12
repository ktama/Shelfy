//! Windows 連携（doc/ARCHITECTURE.md 第 8 節）。
//! OS を知るのはこのファイルと `store` に限る。

use std::ffi::OsStr;
use std::fs::OpenOptions;
use std::io::Write;
use std::os::windows::ffi::OsStrExt;
use std::path::{Path, PathBuf};
use std::sync::Mutex;

use time::OffsetDateTime;
use windows_sys::Win32::Foundation::{ERROR_SUCCESS, HWND};
use windows_sys::Win32::System::Registry::{
    RegCloseKey, RegGetValueW, RegNotifyChangeKeyValue, RegOpenKeyExW, HKEY, HKEY_CURRENT_USER,
    HKEY_LOCAL_MACHINE, KEY_NOTIFY, REG_NOTIFY_CHANGE_LAST_SET, RRF_RT_REG_DWORD, RRF_RT_REG_SZ,
};
use windows_sys::Win32::UI::Input::KeyboardAndMouse::{
    GetAsyncKeyState, VK_CONTROL, VK_LWIN, VK_MENU, VK_RWIN, VK_SHIFT,
};
use windows_sys::Win32::UI::Shell::ShellExecuteW;
use windows_sys::Win32::UI::WindowsAndMessaging::{GetSystemMetrics, SM_CXSMICON, SW_SHOWNORMAL};

use crate::domain::{Item, ItemType};
use crate::hotkey::HotkeySpec;
use crate::ports::{AppLogger, Clock, ExistenceChecker, HotkeyHoldState, ItemLauncher};

fn to_wide(s: impl AsRef<OsStr>) -> Vec<u16> {
    s.as_ref().encode_wide().chain(std::iter::once(0)).collect()
}

// ---------------------------------------------------------------- 時計

pub struct SystemClock;

impl Clock for SystemClock {
    fn now_utc(&self) -> OffsetDateTime {
        OffsetDateTime::now_utc()
    }
}

// ---------------------------------------------------------------- 存在確認

pub struct FileExistenceChecker;

impl ExistenceChecker for FileExistenceChecker {
    fn exists(&self, target: &str) -> bool {
        if target.trim().is_empty() {
            return false;
        }
        // URL は通信せず存在する扱いにする（SPECIFICATION.md 第 4.2 節）
        let lower = target.to_ascii_lowercase();
        if lower.starts_with("http://") || lower.starts_with("https://") {
            return true;
        }
        Path::new(target).exists()
    }
}

// ---------------------------------------------------------------- 起動

pub struct ShellLauncher;

impl ShellLauncher {
    fn shell_execute(verb: &str, file: &str, args: Option<&str>) -> bool {
        let verb_w = to_wide(verb);
        let file_w = to_wide(file);
        let args_w = args.map(to_wide);
        let result = unsafe {
            ShellExecuteW(
                std::ptr::null_mut::<core::ffi::c_void>() as HWND,
                verb_w.as_ptr(),
                file_w.as_ptr(),
                args_w
                    .as_ref()
                    .map(|a| a.as_ptr())
                    .unwrap_or(std::ptr::null()),
                std::ptr::null(),
                SW_SHOWNORMAL,
            )
        };
        // 32 以下は失敗を表す
        (result as isize) > 32
    }
}

impl ItemLauncher for ShellLauncher {
    fn launch(&self, item: &Item) -> bool {
        Self::shell_execute("open", item.target(), None)
    }

    fn open_parent_folder(&self, item: &Item) -> bool {
        if item.item_type() == ItemType::Url {
            return false;
        }
        let path = Path::new(item.target());
        let Some(parent) = path.parent() else {
            return false;
        };
        if !parent.exists() {
            return false;
        }
        // 対象を選択した状態でエクスプローラを開く
        let args = format!("/select,\"{}\"", item.target());
        Self::shell_execute("open", "explorer.exe", Some(&args))
    }
}

// ---------------------------------------------------------------- ホットキーの押下状態

/// 設定されたホットキーの修飾キーが、いま押されたままかを見る
pub struct Win32HotkeyHoldState {
    spec: Mutex<HotkeySpec>,
}

impl Win32HotkeyHoldState {
    pub fn new(spec: HotkeySpec) -> Self {
        Self {
            spec: Mutex::new(spec),
        }
    }

    /// 設定変更で登録し直したときに呼ぶ
    pub fn set(&self, spec: HotkeySpec) {
        *self.spec.lock().unwrap() = spec;
    }
}

fn is_down(vk: u16) -> bool {
    // 最上位ビットが立っていれば押されている
    (unsafe { GetAsyncKeyState(vk as i32) } as u16 & 0x8000) != 0
}

impl HotkeyHoldState for Win32HotkeyHoldState {
    fn is_held(&self) -> bool {
        let spec = self.spec.lock().unwrap();
        if !spec.has_modifier() {
            return false;
        }
        if spec.ctrl && !is_down(VK_CONTROL) {
            return false;
        }
        if spec.shift && !is_down(VK_SHIFT) {
            return false;
        }
        if spec.alt && !is_down(VK_MENU) {
            return false;
        }
        if spec.win && !is_down(VK_LWIN) && !is_down(VK_RWIN) {
            return false;
        }
        true
    }
}

// ---------------------------------------------------------------- ログ

/// 追記だけを行うログ。書き込みに失敗してもアプリの動作に影響させない。
pub struct FileLogger {
    path: PathBuf,
    lock: Mutex<()>,
}

impl FileLogger {
    pub fn new(path: PathBuf) -> Self {
        Self {
            path,
            lock: Mutex::new(()),
        }
    }

    fn write(&self, level: &str, message: &str) {
        let stamp = OffsetDateTime::now_utc()
            .format(&time::format_description::well_known::Rfc3339)
            .unwrap_or_default();
        let line = format!("{stamp} [{level}] {message}\n");

        let _guard = self.lock.lock();
        if let Some(dir) = self.path.parent() {
            let _ = std::fs::create_dir_all(dir);
        }
        if let Ok(mut file) = OpenOptions::new()
            .create(true)
            .append(true)
            .open(&self.path)
        {
            let _ = file.write_all(line.as_bytes());
        }
    }
}

impl AppLogger for FileLogger {
    fn info(&self, message: &str) {
        self.write("INFO", message);
    }

    fn warn(&self, message: &str) {
        self.write("WARN", message);
    }

    fn error(&self, message: &str) {
        self.write("ERROR", message);
    }
}

// ---------------------------------------------------------------- WebView2 の有無

/// WebView2 ランタイムの版数。無ければ `None`。
///
/// この構成で唯一、実行環境に前提を置く箇所であり、
/// 黙って落ちないよう起動前に確かめる（ARCHITECTURE.md 第 8.4 節）。
pub fn webview2_version() -> Option<String> {
    const CLIENT: &str = "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";
    let candidates = [
        (
            HKEY_LOCAL_MACHINE,
            format!("SOFTWARE\\WOW6432Node\\Microsoft\\EdgeUpdate\\Clients\\{CLIENT}"),
        ),
        (
            HKEY_LOCAL_MACHINE,
            format!("SOFTWARE\\Microsoft\\EdgeUpdate\\Clients\\{CLIENT}"),
        ),
        (
            HKEY_CURRENT_USER,
            format!("SOFTWARE\\Microsoft\\EdgeUpdate\\Clients\\{CLIENT}"),
        ),
    ];

    for (root, subkey) in candidates {
        if let Some(version) = read_registry_string(root, &subkey, "pv") {
            if !version.is_empty() && version != "0.0.0.0" {
                return Some(version);
            }
        }
    }
    None
}

// ---------------------------------------------------------------- タスクバーのテーマ

/// テーマの設定が置かれているキー
const PERSONALIZE_KEY: &str = "Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize";

/// タスクバーが明るいテーマか（doc/DESIGN.md 第 10 節）。
///
/// タスクバーのテーマ（`SystemUsesLightTheme`）はアプリのテーマ（`AppsUseLightTheme`）と
/// 別に設定できるため、ウィンドウのテーマでは判断しない。
/// 値が無い環境では暗いとみなす。明るいタスクバーを選べるようになる前の Windows 10 は、
/// タスクバーが暗い色だった。
pub fn taskbar_uses_light_theme() -> bool {
    read_registry_dword(HKEY_CURRENT_USER, PERSONALIZE_KEY, "SystemUsesLightTheme") == Some(1)
}

/// 通知領域のアイコンの一辺（px）。表示倍率 100% で 16、150% で 24 になる。
pub fn small_icon_size() -> u32 {
    let size = unsafe { GetSystemMetrics(SM_CXSMICON) };
    u32::try_from(size).ok().filter(|&s| s > 0).unwrap_or(16)
}

/// テーマの設定が変わるのを待つ。
///
/// レジストリの変更通知で止まって待つので、変わらないあいだは CPU を使わない。
pub struct ThemeSettingsWatch {
    key: HKEY,
}

// キーのハンドルは、開いたスレッド以外で使ってもよい
unsafe impl Send for ThemeSettingsWatch {}

impl ThemeSettingsWatch {
    pub fn open() -> Option<Self> {
        let subkey = to_wide(PERSONALIZE_KEY);
        let mut key: HKEY = std::ptr::null_mut();
        let status =
            unsafe { RegOpenKeyExW(HKEY_CURRENT_USER, subkey.as_ptr(), 0, KEY_NOTIFY, &mut key) };
        (status == ERROR_SUCCESS).then_some(Self { key })
    }

    /// キーの値のどれかが変わるまで戻らない。待てなくなったら `false` を返す。
    pub fn wait(&self) -> bool {
        let status = unsafe {
            RegNotifyChangeKeyValue(
                self.key,
                0,
                REG_NOTIFY_CHANGE_LAST_SET,
                std::ptr::null_mut(),
                0,
            )
        };
        status == ERROR_SUCCESS
    }
}

impl Drop for ThemeSettingsWatch {
    fn drop(&mut self) {
        unsafe {
            RegCloseKey(self.key);
        }
    }
}

fn read_registry_dword(root: HKEY, subkey: &str, value: &str) -> Option<u32> {
    let subkey_w = to_wide(subkey);
    let value_w = to_wide(value);
    let mut data: u32 = 0;
    let mut size = std::mem::size_of::<u32>() as u32;
    let status = unsafe {
        RegGetValueW(
            root,
            subkey_w.as_ptr(),
            value_w.as_ptr(),
            RRF_RT_REG_DWORD,
            std::ptr::null_mut(),
            (&mut data as *mut u32).cast(),
            &mut size,
        )
    };
    (status == ERROR_SUCCESS).then_some(data)
}

fn read_registry_string(root: HKEY, subkey: &str, value: &str) -> Option<String> {
    let subkey_w = to_wide(subkey);
    let value_w = to_wide(value);
    let mut size: u32 = 0;

    let status = unsafe {
        RegGetValueW(
            root,
            subkey_w.as_ptr(),
            value_w.as_ptr(),
            RRF_RT_REG_SZ,
            std::ptr::null_mut(),
            std::ptr::null_mut(),
            &mut size,
        )
    };
    if status != ERROR_SUCCESS || size == 0 {
        return None;
    }

    let mut buffer = vec![0u16; (size as usize).div_ceil(2)];
    let status = unsafe {
        RegGetValueW(
            root,
            subkey_w.as_ptr(),
            value_w.as_ptr(),
            RRF_RT_REG_SZ,
            std::ptr::null_mut(),
            buffer.as_mut_ptr().cast(),
            &mut size,
        )
    };
    if status != ERROR_SUCCESS {
        return None;
    }

    let text: String = String::from_utf16_lossy(&buffer);
    Some(text.trim_end_matches('\0').to_string())
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::hotkey;

    #[test]
    fn urls_are_treated_as_present_without_touching_the_network() {
        let checker = FileExistenceChecker;
        assert!(checker.exists("https://example.com"));
        assert!(checker.exists("HTTP://EXAMPLE.COM"));
    }

    #[test]
    fn blank_targets_are_treated_as_absent() {
        let checker = FileExistenceChecker;
        assert!(!checker.exists(""));
        assert!(!checker.exists("   "));
    }

    #[test]
    fn an_existing_path_is_found_and_a_missing_one_is_not() {
        let checker = FileExistenceChecker;
        let dir = std::env::temp_dir();
        assert!(checker.exists(dir.to_str().unwrap()));
        assert!(!checker.exists(dir.join("shelfy-no-such-file.txt").to_str().unwrap()));
    }

    #[test]
    fn a_hotkey_without_modifiers_is_never_reported_as_held() {
        let state = Win32HotkeyHoldState::new(hotkey::parse("Space").unwrap());
        assert!(!state.is_held());
    }

    #[test]
    fn the_logger_appends_and_survives_a_missing_folder() {
        let dir = tempfile::TempDir::new().unwrap();
        let path = dir.path().join("nested").join("Shelfy.log");
        let logger = FileLogger::new(path.clone());

        logger.info("始めました");
        logger.warn("気になること");
        logger.error("失敗");

        let text = std::fs::read_to_string(&path).unwrap();
        assert_eq!(text.lines().count(), 3);
        assert!(text.contains("[INFO] 始めました"));
        assert!(text.contains("[WARN]"));
        assert!(text.contains("[ERROR] 失敗"));
    }

    #[test]
    fn the_webview2_check_returns_a_version_or_nothing_without_panicking() {
        // どちらの環境でも落ちないことを見る
        let found = webview2_version();
        if let Some(version) = found {
            assert!(version.contains('.'), "version looked odd: {version}");
        }
    }

    #[test]
    fn the_taskbar_theme_and_icon_size_can_be_read_without_panicking() {
        // テーマはどちらでもよい。読めることと、大きさが現実的な範囲にあることを見る。
        let _ = taskbar_uses_light_theme();
        let size = small_icon_size();
        assert!(
            (16..=64).contains(&size),
            "small icon size looked odd: {size}"
        );
        // CI の環境にはテーマのキーが無いことがあるので、開けるかどうかは問わない。
        // 開けた場合に、閉じるところまで落ちずに済むことだけを見る。
        drop(ThemeSettingsWatch::open());
    }
}
