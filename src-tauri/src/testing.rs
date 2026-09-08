//! ポートの試験用実装。ユースケースのテストで差し替えて使う。

use std::cell::RefCell;
use std::collections::BTreeMap;

use time::OffsetDateTime;

use crate::domain::{Item, ItemId, Shelf, ShelfId};
use crate::ports::*;

// ---------------------------------------------------------------- 永続化

#[derive(Default)]
pub struct InMemoryShelfRepository {
    shelves: RefCell<Vec<Shelf>>,
}

impl InMemoryShelfRepository {
    pub fn new() -> Self {
        Self::default()
    }

    pub fn with(shelves: Vec<Shelf>) -> Self {
        Self {
            shelves: RefCell::new(shelves),
        }
    }

    pub fn count(&self) -> usize {
        self.shelves.borrow().len()
    }
}

impl ShelfRepository for InMemoryShelfRepository {
    fn get(&self, id: ShelfId) -> Option<Shelf> {
        self.shelves.borrow().iter().find(|s| s.id() == id).cloned()
    }

    fn all(&self) -> Vec<Shelf> {
        self.shelves.borrow().clone()
    }

    fn children(&self, parent: Option<ShelfId>) -> Vec<Shelf> {
        self.shelves
            .borrow()
            .iter()
            .filter(|s| s.parent_id() == parent)
            .cloned()
            .collect()
    }

    fn add(&self, shelf: Shelf) {
        self.shelves.borrow_mut().push(shelf);
    }

    fn update(&self, shelf: Shelf) {
        let mut list = self.shelves.borrow_mut();
        if let Some(slot) = list.iter_mut().find(|s| s.id() == shelf.id()) {
            *slot = shelf;
        }
    }

    fn delete(&self, id: ShelfId) {
        self.shelves.borrow_mut().retain(|s| s.id() != id);
    }
}

#[derive(Default)]
pub struct InMemoryItemRepository {
    items: RefCell<Vec<Item>>,
}

impl InMemoryItemRepository {
    pub fn new() -> Self {
        Self::default()
    }

    pub fn with(items: Vec<Item>) -> Self {
        Self {
            items: RefCell::new(items),
        }
    }

    pub fn count(&self) -> usize {
        self.items.borrow().len()
    }
}

impl ItemRepository for InMemoryItemRepository {
    fn get(&self, id: ItemId) -> Option<Item> {
        self.items.borrow().iter().find(|i| i.id() == id).cloned()
    }

    fn by_shelf(&self, shelf: ShelfId) -> Vec<Item> {
        let mut found: Vec<Item> = self
            .items
            .borrow()
            .iter()
            .filter(|i| i.shelf_id() == shelf)
            .cloned()
            .collect();
        found.sort_by(|a, b| {
            a.sort_order()
                .cmp(&b.sort_order())
                .then_with(|| a.display_name().cmp(b.display_name()))
        });
        found
    }

    fn search(&self, text: &str) -> Vec<Item> {
        let needle = text.to_lowercase();
        self.items
            .borrow()
            .iter()
            .filter(|i| {
                i.display_name().to_lowercase().contains(&needle)
                    || i.target().to_lowercase().contains(&needle)
                    || i.memo()
                        .map(|m| m.to_lowercase().contains(&needle))
                        .unwrap_or(false)
            })
            .cloned()
            .collect()
    }

    fn recent(&self, count: usize) -> Vec<Item> {
        let mut found: Vec<Item> = self
            .items
            .borrow()
            .iter()
            .filter(|i| i.last_accessed_at().is_some())
            .cloned()
            .collect();
        found.sort_by_key(|i| std::cmp::Reverse(i.last_accessed_at()));
        found.truncate(count);
        found
    }

    fn all(&self) -> Vec<Item> {
        self.items.borrow().clone()
    }

    fn add(&self, item: Item) {
        self.items.borrow_mut().push(item);
    }

    fn update(&self, item: Item) {
        let mut list = self.items.borrow_mut();
        if let Some(slot) = list.iter_mut().find(|i| i.id() == item.id()) {
            *slot = item;
        }
    }

    fn delete(&self, id: ItemId) {
        self.items.borrow_mut().retain(|i| i.id() != id);
    }

    fn delete_by_shelf(&self, shelf: ShelfId) {
        self.items.borrow_mut().retain(|i| i.shelf_id() != shelf);
    }
}

#[derive(Default)]
pub struct InMemorySettingsRepository {
    values: RefCell<BTreeMap<String, String>>,
}

impl InMemorySettingsRepository {
    pub fn new() -> Self {
        Self::default()
    }
}

impl SettingsRepository for InMemorySettingsRepository {
    fn get(&self, key: &str) -> Option<String> {
        self.values.borrow().get(key).cloned()
    }

    fn set(&self, key: &str, value: &str) {
        self.values
            .borrow_mut()
            .insert(key.to_string(), value.to_string());
    }

    fn remove(&self, key: &str) {
        self.values.borrow_mut().remove(key);
    }

    fn all(&self) -> BTreeMap<String, String> {
        self.values.borrow().clone()
    }
}

// ---------------------------------------------------------------- システム

/// 呼ばれるたびに固定の時刻を返す。`advance` で進められる。
pub struct FixedClock {
    now: RefCell<OffsetDateTime>,
}

impl FixedClock {
    pub fn new(now: OffsetDateTime) -> Self {
        Self {
            now: RefCell::new(now),
        }
    }

    pub fn set(&self, now: OffsetDateTime) {
        *self.now.borrow_mut() = now;
    }
}

impl Clock for FixedClock {
    fn now_utc(&self) -> OffsetDateTime {
        *self.now.borrow()
    }
}

/// 起動の成否を決められる launcher。呼ばれた対象を記録する。
pub struct FakeLauncher {
    pub launch_succeeds: bool,
    pub open_parent_succeeds: bool,
    launched: RefCell<Vec<ItemId>>,
    opened: RefCell<Vec<ItemId>>,
}

impl FakeLauncher {
    pub fn new() -> Self {
        Self {
            launch_succeeds: true,
            open_parent_succeeds: true,
            launched: RefCell::new(Vec::new()),
            opened: RefCell::new(Vec::new()),
        }
    }

    pub fn failing() -> Self {
        Self {
            launch_succeeds: false,
            open_parent_succeeds: false,
            ..Self::new()
        }
    }

    pub fn launched(&self) -> Vec<ItemId> {
        self.launched.borrow().clone()
    }

    pub fn opened(&self) -> Vec<ItemId> {
        self.opened.borrow().clone()
    }
}

impl Default for FakeLauncher {
    fn default() -> Self {
        Self::new()
    }
}

impl ItemLauncher for FakeLauncher {
    fn launch(&self, item: &Item) -> bool {
        self.launched.borrow_mut().push(item.id());
        self.launch_succeeds
    }

    fn open_parent_folder(&self, item: &Item) -> bool {
        self.opened.borrow_mut().push(item.id());
        self.open_parent_succeeds
    }
}

/// 指定した参照先だけを「存在する」とみなす。URL は常に存在する扱い。
pub struct FakeExistenceChecker {
    existing: Vec<String>,
}

impl FakeExistenceChecker {
    pub fn with(existing: &[&str]) -> Self {
        Self {
            existing: existing.iter().map(|s| s.to_string()).collect(),
        }
    }

    pub fn none() -> Self {
        Self {
            existing: Vec::new(),
        }
    }
}

impl ExistenceChecker for FakeExistenceChecker {
    fn exists(&self, target: &str) -> bool {
        if target.trim().is_empty() {
            return false;
        }
        let lower = target.to_lowercase();
        if lower.starts_with("http://") || lower.starts_with("https://") {
            return true;
        }
        self.existing.iter().any(|e| e.eq_ignore_ascii_case(target))
    }
}

pub struct FakeHotkeyHoldState {
    pub held: bool,
}

impl FakeHotkeyHoldState {
    pub fn held() -> Self {
        Self { held: true }
    }

    pub fn released() -> Self {
        Self { held: false }
    }
}

impl HotkeyHoldState for FakeHotkeyHoldState {
    fn is_held(&self) -> bool {
        self.held
    }
}

#[derive(Default)]
pub struct RecordingLogger {
    lines: RefCell<Vec<String>>,
}

impl RecordingLogger {
    pub fn new() -> Self {
        Self::default()
    }

    pub fn lines(&self) -> Vec<String> {
        self.lines.borrow().clone()
    }
}

impl AppLogger for RecordingLogger {
    fn info(&self, message: &str) {
        self.lines.borrow_mut().push(format!("INFO {message}"));
    }

    fn warn(&self, message: &str) {
        self.lines.borrow_mut().push(format!("WARN {message}"));
    }

    fn error(&self, message: &str) {
        self.lines.borrow_mut().push(format!("ERROR {message}"));
    }
}
