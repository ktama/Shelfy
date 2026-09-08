//! ユースケースが外界に要求する能力。
//! すべて同期で、`&self` を受ける（doc/rebuild/03-ARCHITECTURE.md 第 4.3 節）。

use std::collections::BTreeMap;

use time::OffsetDateTime;

use crate::domain::{Item, ItemId, Shelf, ShelfId};

pub trait ShelfRepository {
    fn get(&self, id: ShelfId) -> Option<Shelf>;
    fn all(&self) -> Vec<Shelf>;
    /// `None` を渡すとルート直下の Shelf を返す
    fn children(&self, parent: Option<ShelfId>) -> Vec<Shelf>;
    fn add(&self, shelf: Shelf);
    fn update(&self, shelf: Shelf);
    fn delete(&self, id: ShelfId);
}

pub trait ItemRepository {
    fn get(&self, id: ItemId) -> Option<Item>;
    /// 表示順（`sort_order`、次に表示名）で返す
    fn by_shelf(&self, shelf: ShelfId) -> Vec<Item>;
    /// 表示名、参照先、メモのいずれかに部分一致するものを返す（大文字小文字を区別しない）
    fn search(&self, text: &str) -> Vec<Item>;
    /// 最終アクセス日時を持つものだけを降順で、先頭から `count` 件返す
    fn recent(&self, count: usize) -> Vec<Item>;
    fn all(&self) -> Vec<Item>;
    fn add(&self, item: Item);
    fn update(&self, item: Item);
    fn delete(&self, id: ItemId);
    fn delete_by_shelf(&self, shelf: ShelfId);
}

pub trait SettingsRepository {
    fn get(&self, key: &str) -> Option<String>;
    fn set(&self, key: &str, value: &str);
    fn remove(&self, key: &str);
    fn all(&self) -> BTreeMap<String, String>;
}

pub trait ItemLauncher {
    /// 参照先を OS の既定の関連付けで開く
    fn launch(&self, item: &Item) -> bool;
    /// 参照先を選択した状態で親フォルダを開く
    fn open_parent_folder(&self, item: &Item) -> bool;
}

pub trait ExistenceChecker {
    fn exists(&self, target: &str) -> bool;
}

pub trait HotkeyHoldState {
    /// ホットキーの修飾キーが押されたままかどうか
    fn is_held(&self) -> bool;
}

pub trait Clock {
    fn now_utc(&self) -> OffsetDateTime;
}

pub trait AppLogger {
    fn info(&self, message: &str);
    fn warn(&self, message: &str);
    fn error(&self, message: &str);
}

/// 設定のキー（doc/rebuild/02-SPECIFICATION.md 第 8 節）
pub mod settings_keys {
    pub const GLOBAL_HOTKEY: &str = "GlobalHotkey";
    pub const WINDOW_WIDTH: &str = "WindowWidth";
    pub const WINDOW_HEIGHT: &str = "WindowHeight";
    pub const START_MINIMIZED: &str = "StartMinimized";
    pub const RECENT_ITEMS_COUNT: &str = "RecentItemsCount";

    pub const DEFAULT_GLOBAL_HOTKEY: &str = "Ctrl+Shift+Space";
    pub const DEFAULT_WINDOW_WIDTH: f64 = 800.0;
    pub const DEFAULT_WINDOW_HEIGHT: f64 = 500.0;
    pub const DEFAULT_START_MINIMIZED: bool = false;
    pub const DEFAULT_RECENT_ITEMS_COUNT: usize = 20;
}
