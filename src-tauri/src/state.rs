//! アプリ全体の状態。Tauri の管理状態として 1 つだけ持つ。

use std::sync::Arc;

use shelfy::adapters::existence::{CachedExistenceChecker, DEFAULT_TTL};
use shelfy::adapters::store::{JsonStore, LoadOutcome, StorePaths};
use shelfy::adapters::windows::{
    FileExistenceChecker, FileLogger, ShellLauncher, SystemClock, Win32HotkeyHoldState,
};
use shelfy::hotkey::{self, HotkeySpec};
use shelfy::ports::{settings_keys, AppLogger, SettingsRepository};

/// ウィンドウに掛けられた効果。画面はこれを見て背景の塗り方を決める。
pub struct WindowEffects {
    /// Mica を適用できたか。できていれば画面は背景を塗らずに透かす。
    pub mica: bool,
}

pub struct AppState {
    pub store: Arc<JsonStore>,
    pub launcher: ShellLauncher,
    pub existence: CachedExistenceChecker<FileExistenceChecker>,
    pub hotkey_hold: Win32HotkeyHoldState,
    pub clock: SystemClock,
    pub logger: FileLogger,
    /// 読み込みで何が起きたか。起動直後に画面へ伝える。
    pub load_outcome: LoadOutcome,
}

impl AppState {
    pub fn open(paths: StorePaths) -> Self {
        let logger = FileLogger::new(paths.log_file());
        let (store, load_outcome) = JsonStore::load(paths);

        match &load_outcome {
            LoadOutcome::Fresh => logger.info("保存ファイルが無いため、空で開始します"),
            LoadOutcome::Loaded => logger.info("保存ファイルを読み込みました"),
            LoadOutcome::RecoveredFromBackup => {
                logger.warn("保存ファイルを読めないため、控えから回復しました")
            }
            LoadOutcome::StartedEmpty { quarantined } => logger.error(&format!(
                "保存ファイルも控えも読めません。退避先: {}",
                quarantined
                    .as_ref()
                    .map(|p| p.display().to_string())
                    .unwrap_or_else(|| "なし".into())
            )),
            LoadOutcome::ReadOnly { schema_version } => logger.warn(&format!(
                "知らない版数({schema_version})のため、読み取り専用で開きます"
            )),
        }

        let spec = Self::hotkey_from(&store);

        Self {
            hotkey_hold: Win32HotkeyHoldState::new(spec),
            store: Arc::new(store),
            launcher: ShellLauncher,
            existence: CachedExistenceChecker::new(FileExistenceChecker, DEFAULT_TTL),
            clock: SystemClock,
            logger,
            load_outcome,
        }
    }

    fn hotkey_from(store: &JsonStore) -> HotkeySpec {
        SettingsRepository::get(store, settings_keys::GLOBAL_HOTKEY)
            .and_then(|text| hotkey::parse(&text))
            .unwrap_or_else(hotkey::default_spec)
    }

    /// 保存されているホットキー。無ければ既定値。
    pub fn hotkey_spec(&self) -> HotkeySpec {
        Self::hotkey_from(&self.store)
    }

    /// 未保存の変更を書き出す。失敗はログに残すだけで、操作は止めない。
    pub fn flush(&self) {
        if let Err(e) = self.store.flush() {
            self.logger.error(&format!("保存に失敗しました: {e}"));
        }
    }
}
