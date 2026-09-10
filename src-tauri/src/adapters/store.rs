//! JSON スナップショットによる永続化。
//! 方式は doc/rebuild/03-ARCHITECTURE.md 第 7 節、書式は 04-DATA_MIGRATION.md 第 4 節に対応する。
//!
//! 起動時に 1 個のファイルを読み込んで全データをメモリに載せ、
//! 更新はメモリ上で行い、一定時間まとめてからファイルへ書き戻す。

use std::collections::{BTreeMap, HashMap};
use std::fs;
use std::io;
use std::path::{Path, PathBuf};
use std::sync::Mutex;
use std::time::{Duration, Instant};

use serde::{Deserialize, Serialize};
use time::OffsetDateTime;

use crate::domain::{Item, ItemId, Shelf, ShelfId};
use crate::ports::{ItemRepository, SettingsRepository, ShelfRepository};
use crate::usecases::transfer::{
    format_time, item_from_data, item_to_data, shelf_from_data, shelf_to_data, ItemData, ShelfData,
};

/// この版が理解できるスキーマの版数
pub const SCHEMA_VERSION: u32 = 1;

/// 保存を先延ばしする既定の待ち時間
pub const DEFAULT_SAVE_DELAY: Duration = Duration::from_millis(500);

// ---------------------------------------------------------------- 書式

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Snapshot {
    pub schema_version: u32,
    pub saved_at: String,
    #[serde(default)]
    pub shelves: Vec<ShelfData>,
    #[serde(default)]
    pub items: Vec<ItemData>,
    #[serde(default)]
    pub settings: BTreeMap<String, String>,
}

// ---------------------------------------------------------------- 配置

/// 保存に使うファイルの組
#[derive(Debug, Clone)]
pub struct StorePaths {
    pub main: PathBuf,
    pub backup: PathBuf,
    pub temp: PathBuf,
}

impl StorePaths {
    /// 指定のフォルダに `shelfy.json` とその控えを置く
    pub fn in_dir(dir: impl AsRef<Path>) -> Self {
        let dir = dir.as_ref();
        Self {
            main: dir.join("shelfy.json"),
            backup: dir.join("shelfy.json.bak"),
            temp: dir.join("shelfy.json.tmp"),
        }
    }

    /// `%LOCALAPPDATA%\Shelfy` を使う
    pub fn default_location() -> io::Result<Self> {
        let base = std::env::var("LOCALAPPDATA")
            .map_err(|_| io::Error::new(io::ErrorKind::NotFound, "LOCALAPPDATA is not set"))?;
        let dir = PathBuf::from(base).join("Shelfy");
        fs::create_dir_all(&dir)?;
        Ok(Self::in_dir(dir))
    }

    fn dir(&self) -> Option<&Path> {
        self.main.parent()
    }
}

// ---------------------------------------------------------------- 読み込み結果

/// 読み込みで何が起きたか。利用者に伝えるために使う。
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum LoadOutcome {
    /// 保存ファイルが無く、空で始めた（初回起動）
    Fresh,
    /// 本体から読めた
    Loaded,
    /// 本体が読めず、控えから回復した
    RecoveredFromBackup,
    /// どちらも読めず、空で始めた。読めなかったファイルは退避してある。
    StartedEmpty { quarantined: Option<PathBuf> },
    /// 自分が知らない新しい版数だった。読み取り専用として扱う。
    ReadOnly { schema_version: u32 },
}

// ---------------------------------------------------------------- 本体

struct Inner {
    shelves: HashMap<ShelfId, Shelf>,
    items: HashMap<ItemId, Item>,
    settings: BTreeMap<String, String>,
    dirty: bool,
    /// 最後に変更した時刻。保存を先延ばしする判断に使う。
    changed_at: Option<Instant>,
    /// 新しい版数のファイルを読んだときは書き戻さない
    read_only: bool,
}

impl Inner {
    fn empty() -> Self {
        Self {
            shelves: HashMap::new(),
            items: HashMap::new(),
            settings: BTreeMap::new(),
            dirty: false,
            changed_at: None,
            read_only: false,
        }
    }

    fn touch(&mut self) {
        self.dirty = true;
        self.changed_at = Some(Instant::now());
    }
}

/// 全データをメモリに保持し、まとめてファイルへ書き戻すストア
pub struct JsonStore {
    paths: StorePaths,
    inner: Mutex<Inner>,
}

impl JsonStore {
    /// ファイルを読み込んでストアを作る。読めない場合も起動できる形で返す。
    pub fn load(paths: StorePaths) -> (Self, LoadOutcome) {
        let store = Self {
            paths,
            inner: Mutex::new(Inner::empty()),
        };
        let outcome = store.load_into_memory();
        (store, outcome)
    }

    fn load_into_memory(&self) -> LoadOutcome {
        let main_exists = self.paths.main.exists();

        if let Some(snapshot) = read_snapshot(&self.paths.main) {
            return self.apply(snapshot, LoadOutcome::Loaded);
        }

        if let Some(snapshot) = read_snapshot(&self.paths.backup) {
            return self.apply(snapshot, LoadOutcome::RecoveredFromBackup);
        }

        if !main_exists && !self.paths.backup.exists() {
            return LoadOutcome::Fresh;
        }

        // 本体も控えも読めない。上書きしてしまわないよう退避する。
        let quarantined = self.quarantine_main();
        LoadOutcome::StartedEmpty { quarantined }
    }

    fn apply(&self, snapshot: Snapshot, outcome: LoadOutcome) -> LoadOutcome {
        let read_only = snapshot.schema_version > SCHEMA_VERSION;
        let version = snapshot.schema_version;

        let mut inner = self.inner.lock().unwrap();
        *inner = Inner::empty();
        inner.read_only = read_only;

        // 読めないレコードは、そのレコードだけを飛ばす
        for data in &snapshot.shelves {
            if let Some(shelf) = shelf_from_data(data) {
                inner.shelves.insert(shelf.id(), shelf);
            }
        }
        for data in &snapshot.items {
            if let Some(item) = item_from_data(data) {
                inner.items.insert(item.id(), item);
            }
        }
        inner.settings = snapshot.settings;

        if read_only {
            LoadOutcome::ReadOnly {
                schema_version: version,
            }
        } else {
            outcome
        }
    }

    /// 読めなかった本体を `shelfy.json.corrupt.<日時>` へ移す
    fn quarantine_main(&self) -> Option<PathBuf> {
        if !self.paths.main.exists() {
            return None;
        }
        let stamp = OffsetDateTime::now_utc()
            .format(&time::format_description::well_known::Rfc3339)
            .unwrap_or_else(|_| String::from("unknown"))
            .replace([':', '.'], "-");
        let target = self
            .paths
            .dir()?
            .join(format!("shelfy.json.corrupt.{stamp}"));
        fs::rename(&self.paths.main, &target).ok()?;
        Some(target)
    }

    /// 上書き保存を行わない状態かどうか
    pub fn is_read_only(&self) -> bool {
        self.inner.lock().unwrap().read_only
    }

    /// 未保存の変更があるか
    pub fn is_dirty(&self) -> bool {
        self.inner.lock().unwrap().dirty
    }

    /// 未保存の変更があれば、待たずに書き出す。
    /// ウィンドウを隠すとき、終了するとき、取り込みの直後に呼ぶ。
    pub fn flush(&self) -> io::Result<bool> {
        self.write_if_needed(Duration::ZERO)
    }

    /// 最後の変更から `delay` が過ぎていれば書き出す。
    /// 常駐中に一定間隔で呼ぶ。
    pub fn flush_if_due(&self, delay: Duration) -> io::Result<bool> {
        self.write_if_needed(delay)
    }

    fn write_if_needed(&self, delay: Duration) -> io::Result<bool> {
        let snapshot = {
            let inner = self.inner.lock().unwrap();
            if !inner.dirty || inner.read_only {
                return Ok(false);
            }
            if let Some(changed_at) = inner.changed_at {
                if changed_at.elapsed() < delay {
                    return Ok(false);
                }
            }
            self.snapshot_locked(&inner)
        };

        self.write_atomically(&snapshot)?;

        let mut inner = self.inner.lock().unwrap();
        inner.dirty = false;
        inner.changed_at = None;
        Ok(true)
    }

    fn snapshot_locked(&self, inner: &Inner) -> Snapshot {
        let mut shelves: Vec<ShelfData> = inner.shelves.values().map(shelf_to_data).collect();
        let mut items: Vec<ItemData> = inner.items.values().map(item_to_data).collect();
        // 連想配列の走査順は決まらないため、差分を読みやすいよう識別子で並べる
        shelves.sort_by(|a, b| a.id.cmp(&b.id));
        items.sort_by(|a, b| a.id.cmp(&b.id));

        Snapshot {
            schema_version: SCHEMA_VERSION,
            saved_at: format_time(OffsetDateTime::now_utc()),
            shelves,
            items,
            settings: inner.settings.clone(),
        }
    }

    /// 一時ファイルへ書いてから本体を置き換える。
    /// 書き込み中に電源が落ちても、本体が半端な内容にならないようにする。
    fn write_atomically(&self, snapshot: &Snapshot) -> io::Result<()> {
        if let Some(dir) = self.paths.dir() {
            fs::create_dir_all(dir)?;
        }

        let text = serde_json::to_string_pretty(snapshot)
            .map_err(|e| io::Error::new(io::ErrorKind::InvalidData, e))?;
        fs::write(&self.paths.temp, text.as_bytes())?;

        // 直前の内容を控えへ移してから置き換える。
        // 置き換えに失敗しても、控えから回復できる状態を保つ。
        if self.paths.main.exists() {
            let _ = fs::remove_file(&self.paths.backup);
            fs::rename(&self.paths.main, &self.paths.backup)?;
        }
        fs::rename(&self.paths.temp, &self.paths.main)?;
        Ok(())
    }

    /// 取り込みなどで全体を入れ替えたあとに呼ぶ
    pub fn mark_changed(&self) {
        self.inner.lock().unwrap().touch();
    }
}

// ---------------------------------------------------------------- ポートの実装

impl ShelfRepository for JsonStore {
    fn get(&self, id: ShelfId) -> Option<Shelf> {
        self.inner.lock().unwrap().shelves.get(&id).cloned()
    }

    fn all(&self) -> Vec<Shelf> {
        self.inner
            .lock()
            .unwrap()
            .shelves
            .values()
            .cloned()
            .collect()
    }

    fn children(&self, parent: Option<ShelfId>) -> Vec<Shelf> {
        self.inner
            .lock()
            .unwrap()
            .shelves
            .values()
            .filter(|s| s.parent_id() == parent)
            .cloned()
            .collect()
    }

    fn add(&self, shelf: Shelf) {
        let mut inner = self.inner.lock().unwrap();
        inner.shelves.insert(shelf.id(), shelf);
        inner.touch();
    }

    fn update(&self, shelf: Shelf) {
        let mut inner = self.inner.lock().unwrap();
        let replaced = match inner.shelves.get_mut(&shelf.id()) {
            Some(slot) => {
                *slot = shelf;
                true
            }
            None => false,
        };
        if replaced {
            inner.touch();
        }
    }

    fn delete(&self, id: ShelfId) {
        let mut inner = self.inner.lock().unwrap();
        if inner.shelves.remove(&id).is_some() {
            inner.touch();
        }
    }
}

impl ItemRepository for JsonStore {
    fn get(&self, id: ItemId) -> Option<Item> {
        self.inner.lock().unwrap().items.get(&id).cloned()
    }

    fn by_shelf(&self, shelf: ShelfId) -> Vec<Item> {
        let mut found: Vec<Item> = self
            .inner
            .lock()
            .unwrap()
            .items
            .values()
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
        self.inner
            .lock()
            .unwrap()
            .items
            .values()
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
            .inner
            .lock()
            .unwrap()
            .items
            .values()
            .filter(|i| i.last_accessed_at().is_some())
            .cloned()
            .collect();
        found.sort_by_key(|i| std::cmp::Reverse(i.last_accessed_at()));
        found.truncate(count);
        found
    }

    fn all(&self) -> Vec<Item> {
        self.inner.lock().unwrap().items.values().cloned().collect()
    }

    fn add(&self, item: Item) {
        let mut inner = self.inner.lock().unwrap();
        inner.items.insert(item.id(), item);
        inner.touch();
    }

    fn update(&self, item: Item) {
        let mut inner = self.inner.lock().unwrap();
        let replaced = match inner.items.get_mut(&item.id()) {
            Some(slot) => {
                *slot = item;
                true
            }
            None => false,
        };
        if replaced {
            inner.touch();
        }
    }

    fn delete(&self, id: ItemId) {
        let mut inner = self.inner.lock().unwrap();
        if inner.items.remove(&id).is_some() {
            inner.touch();
        }
    }

    fn delete_by_shelf(&self, shelf: ShelfId) {
        let mut inner = self.inner.lock().unwrap();
        let before = inner.items.len();
        inner.items.retain(|_, i| i.shelf_id() != shelf);
        if inner.items.len() != before {
            inner.touch();
        }
    }
}

impl SettingsRepository for JsonStore {
    fn get(&self, key: &str) -> Option<String> {
        self.inner.lock().unwrap().settings.get(key).cloned()
    }

    fn set(&self, key: &str, value: &str) {
        let mut inner = self.inner.lock().unwrap();
        inner.settings.insert(key.to_string(), value.to_string());
        inner.touch();
    }

    fn remove(&self, key: &str) {
        let mut inner = self.inner.lock().unwrap();
        if inner.settings.remove(key).is_some() {
            inner.touch();
        }
    }

    fn all(&self) -> BTreeMap<String, String> {
        self.inner.lock().unwrap().settings.clone()
    }
}

// ---------------------------------------------------------------- 補助

fn read_snapshot(path: &Path) -> Option<Snapshot> {
    let text = fs::read_to_string(path).ok()?;
    serde_json::from_str(&text).ok()
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::domain::ItemType;
    use crate::ports::settings_keys;
    use crate::testing::FixedClock;
    use crate::usecases::items::{add_item, AddItemResult};
    use crate::usecases::shelves::{create_shelf, CreateShelfResult};
    use tempfile::TempDir;
    use time::macros::datetime;

    fn new_store() -> (TempDir, JsonStore) {
        let dir = TempDir::new().unwrap();
        let (store, outcome) = JsonStore::load(StorePaths::in_dir(dir.path()));
        assert_eq!(outcome, LoadOutcome::Fresh);
        (dir, store)
    }

    fn seed(store: &JsonStore) -> (ShelfId, ItemId) {
        let clock = FixedClock::new(datetime!(2026-01-05 12:00:00 UTC));
        let shelf = match create_shelf(store, "仕事", None) {
            CreateShelfResult::Success { shelf } => shelf.id(),
            o => panic!("{o:?}"),
        };
        let item = match add_item(
            store,
            store,
            &clock,
            shelf,
            ItemType::File,
            "C:\\work\\report.xlsx",
            "報告書",
        ) {
            AddItemResult::Success { item } => item.id(),
            o => panic!("{o:?}"),
        };
        (shelf, item)
    }

    // --- 読み書き ---

    #[test]
    fn a_missing_file_starts_fresh_and_writes_nothing() {
        let (dir, store) = new_store();
        assert!(!store.is_dirty());
        assert!(!store.flush().unwrap());
        assert!(!dir.path().join("shelfy.json").exists());
    }

    #[test]
    fn changes_mark_the_store_dirty_and_flush_writes_the_file() {
        let (dir, store) = new_store();
        seed(&store);

        assert!(store.is_dirty());
        assert!(store.flush().unwrap());
        assert!(!store.is_dirty());
        assert!(dir.path().join("shelfy.json").exists());
        // 一時ファイルは残さない
        assert!(!dir.path().join("shelfy.json.tmp").exists());
    }

    #[test]
    fn saved_data_comes_back_after_reloading() {
        let dir = TempDir::new().unwrap();
        let paths = StorePaths::in_dir(dir.path());

        let (store, _) = JsonStore::load(paths.clone());
        let (shelf, item) = seed(&store);
        store.set(settings_keys::GLOBAL_HOTKEY, "Ctrl+Alt+S");
        store.flush().unwrap();
        drop(store);

        let (reloaded, outcome) = JsonStore::load(paths);
        assert_eq!(outcome, LoadOutcome::Loaded);

        let restored_shelf = ShelfRepository::get(&reloaded, shelf).unwrap();
        assert_eq!(restored_shelf.name(), "仕事");

        let restored_item = ItemRepository::get(&reloaded, item).unwrap();
        assert_eq!(restored_item.display_name(), "報告書");
        assert_eq!(restored_item.target(), "C:\\work\\report.xlsx");
        assert_eq!(
            restored_item.created_at(),
            datetime!(2026-01-05 12:00:00 UTC)
        );

        assert_eq!(
            SettingsRepository::get(&reloaded, settings_keys::GLOBAL_HOTKEY).as_deref(),
            Some("Ctrl+Alt+S")
        );
        assert!(!reloaded.is_dirty());
    }

    #[test]
    fn the_saved_file_uses_the_documented_shape() {
        let (dir, store) = new_store();
        seed(&store);
        store.flush().unwrap();

        let text = fs::read_to_string(dir.path().join("shelfy.json")).unwrap();
        assert!(text.contains("\"schemaVersion\": 1"));
        assert!(text.contains("\"savedAt\""));
        assert!(text.contains("\"displayName\""));
        assert!(text.contains("\"sortOrder\""));
        assert!(text.contains("\"settings\""));

        let snapshot: Snapshot = serde_json::from_str(&text).unwrap();
        assert_eq!(snapshot.schema_version, SCHEMA_VERSION);
        assert_eq!(snapshot.shelves.len(), 1);
        assert_eq!(snapshot.items.len(), 1);
    }

    // --- 遅延保存 ---

    #[test]
    fn flush_if_due_waits_for_the_delay_to_pass() {
        let (dir, store) = new_store();
        seed(&store);

        // まだ待ち時間が過ぎていない
        assert!(!store.flush_if_due(Duration::from_secs(60)).unwrap());
        assert!(store.is_dirty());
        assert!(!dir.path().join("shelfy.json").exists());

        // 待ち時間なしなら書き出す
        assert!(store.flush_if_due(Duration::ZERO).unwrap());
        assert!(!store.is_dirty());
        assert!(dir.path().join("shelfy.json").exists());
    }

    #[test]
    fn flushing_twice_without_changes_writes_only_once() {
        let (_dir, store) = new_store();
        seed(&store);
        assert!(store.flush().unwrap());
        assert!(!store.flush().unwrap());
    }

    // --- 原子的な置き換えと控え ---

    #[test]
    fn the_previous_content_is_kept_as_a_backup() {
        let dir = TempDir::new().unwrap();
        let paths = StorePaths::in_dir(dir.path());
        let (store, _) = JsonStore::load(paths.clone());

        let (shelf, _) = seed(&store);
        store.flush().unwrap();
        assert!(!paths.backup.exists());

        // 2 回目の保存で、直前の内容が控えに残る
        match create_shelf(&store, "個人", None) {
            CreateShelfResult::Success { .. } => {}
            o => panic!("{o:?}"),
        }
        store.flush().unwrap();

        assert!(paths.backup.exists());
        let previous: Snapshot =
            serde_json::from_str(&fs::read_to_string(&paths.backup).unwrap()).unwrap();
        assert_eq!(previous.shelves.len(), 1);
        assert_eq!(previous.shelves[0].id, shelf.to_string());

        let current: Snapshot =
            serde_json::from_str(&fs::read_to_string(&paths.main).unwrap()).unwrap();
        assert_eq!(current.shelves.len(), 2);
    }

    // --- 破損からの回復 ---

    #[test]
    fn a_broken_main_file_is_recovered_from_the_backup() {
        let dir = TempDir::new().unwrap();
        let paths = StorePaths::in_dir(dir.path());
        let (store, _) = JsonStore::load(paths.clone());
        seed(&store);
        store.flush().unwrap();

        // 本体を控えにも残したうえで壊す
        fs::copy(&paths.main, &paths.backup).unwrap();
        fs::write(&paths.main, b"{ this is not json").unwrap();

        let (reloaded, outcome) = JsonStore::load(paths);
        assert_eq!(outcome, LoadOutcome::RecoveredFromBackup);
        assert_eq!(ShelfRepository::all(&reloaded).len(), 1);
        assert_eq!(ItemRepository::all(&reloaded).len(), 1);
    }

    #[test]
    fn when_nothing_can_be_read_the_broken_file_is_quarantined() {
        let dir = TempDir::new().unwrap();
        let paths = StorePaths::in_dir(dir.path());
        fs::write(&paths.main, b"broken").unwrap();
        fs::write(&paths.backup, b"also broken").unwrap();

        let (store, outcome) = JsonStore::load(paths.clone());

        let quarantined = match outcome {
            LoadOutcome::StartedEmpty { quarantined } => quarantined.expect("退避先"),
            o => panic!("expected StartedEmpty, got {o:?}"),
        };
        assert!(quarantined.exists());
        assert!(quarantined
            .file_name()
            .unwrap()
            .to_string_lossy()
            .starts_with("shelfy.json.corrupt."));
        // 壊れた本体は残さない
        assert!(!paths.main.exists());
        assert_eq!(ShelfRepository::all(&store).len(), 0);
    }

    #[test]
    fn a_record_that_cannot_be_read_is_skipped_without_losing_the_rest() {
        let dir = TempDir::new().unwrap();
        let paths = StorePaths::in_dir(dir.path());
        let (store, _) = JsonStore::load(paths.clone());
        let (shelf, _) = seed(&store);
        store.flush().unwrap();

        // 日時が壊れたレコードを 1 件足す
        let mut snapshot: Snapshot =
            serde_json::from_str(&fs::read_to_string(&paths.main).unwrap()).unwrap();
        let mut broken = snapshot.items[0].clone();
        broken.id = ItemId::new().to_string();
        broken.created_at = "壊れた日時".into();
        snapshot.items.push(broken);
        fs::write(&paths.main, serde_json::to_string(&snapshot).unwrap()).unwrap();

        let (reloaded, outcome) = JsonStore::load(paths);
        assert_eq!(outcome, LoadOutcome::Loaded);
        assert_eq!(ItemRepository::all(&reloaded).len(), 1);
        assert_eq!(
            ShelfRepository::get(&reloaded, shelf).unwrap().name(),
            "仕事"
        );
    }

    // --- 新しい版数 ---

    #[test]
    fn a_newer_schema_version_is_treated_as_read_only() {
        let dir = TempDir::new().unwrap();
        let paths = StorePaths::in_dir(dir.path());
        let (store, _) = JsonStore::load(paths.clone());
        seed(&store);
        store.flush().unwrap();

        let mut snapshot: Snapshot =
            serde_json::from_str(&fs::read_to_string(&paths.main).unwrap()).unwrap();
        snapshot.schema_version = SCHEMA_VERSION + 1;
        let raised = serde_json::to_string(&snapshot).unwrap();
        fs::write(&paths.main, &raised).unwrap();

        let (reloaded, outcome) = JsonStore::load(paths.clone());
        assert_eq!(
            outcome,
            LoadOutcome::ReadOnly {
                schema_version: SCHEMA_VERSION + 1
            }
        );
        assert!(reloaded.is_read_only());
        // 読めたデータは使える
        assert_eq!(ShelfRepository::all(&reloaded).len(), 1);

        // 変更しても書き戻さない
        create_shelf(&reloaded, "増やしてみる", None);
        assert!(!reloaded.flush().unwrap());
        assert_eq!(fs::read_to_string(&paths.main).unwrap(), raised);
    }

    // --- ポートとしての振る舞い ---

    #[test]
    fn updating_an_unknown_record_changes_nothing() {
        let (_dir, store) = new_store();
        let (shelf, _) = seed(&store);
        store.flush().unwrap();

        let stray = Shelf::new(ShelfId::new(), "居ない棚", None, 0, false).unwrap();
        ShelfRepository::update(&store, stray);
        assert!(!store.is_dirty());
        assert_eq!(ShelfRepository::all(&store).len(), 1);
        assert!(ShelfRepository::get(&store, shelf).is_some());
    }

    #[test]
    fn deleting_by_shelf_only_touches_that_shelf() {
        let (_dir, store) = new_store();
        let clock = FixedClock::new(datetime!(2026-01-05 12:00:00 UTC));
        let (work, _) = seed(&store);
        let other = match create_shelf(&store, "個人", None) {
            CreateShelfResult::Success { shelf } => shelf.id(),
            o => panic!("{o:?}"),
        };
        add_item(
            &store,
            &store,
            &clock,
            other,
            ItemType::File,
            "C:\\p.txt",
            "p",
        );

        ItemRepository::delete_by_shelf(&store, work);

        assert_eq!(ItemRepository::all(&store).len(), 1);
        assert_eq!(ItemRepository::by_shelf(&store, other).len(), 1);
    }

    #[test]
    fn deleting_something_absent_does_not_mark_the_store_dirty() {
        let (_dir, store) = new_store();
        seed(&store);
        store.flush().unwrap();

        ItemRepository::delete(&store, ItemId::new());
        ShelfRepository::delete(&store, ShelfId::new());
        SettingsRepository::remove(&store, "存在しないキー");

        assert!(!store.is_dirty());
    }
}
