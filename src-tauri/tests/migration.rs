//! 移行の検証（doc/rebuild/04-DATA_MIGRATION.md 第 6 節）。
//!
//! v1.0.0 の `shelfy.db` から `tools/shelfy-migrate` が書き出した JSON を、
//! 再作成版が取り込めることを確かめる。
//! 道具が出す形をそのまま書いた見本を使う。

use shelfy::adapters::store::{JsonStore, StorePaths};
use shelfy::domain::ItemType;
use shelfy::ports::{ItemRepository, ShelfRepository};
use shelfy::usecases::transfer::{import_data, ExportData, ImportMode};
use tempfile::TempDir;

/// `tools/shelfy-migrate` が書き出す形。
/// SQLite の `IsPinned`（整数）は真偽値になり、
/// `ParentId` と `Memo` と `LastAccessedAt` の NULL は「鍵ごと無い」形になる。
/// 日時は v1.0.0 が書く小数部 7 桁のままである。
const MIGRATED: &str = r#"{
  "version": "1.0",
  "exportedAt": "2026-09-11T02:03:04Z",
  "shelves": [
    { "id": "3f2504e0-4f89-41d3-9a0c-0305e82c3301", "name": "仕事", "sortOrder": 0, "isPinned": true },
    { "id": "3f2504e0-4f89-41d3-9a0c-0305e82c3302", "name": "月次", "parentId": "3f2504e0-4f89-41d3-9a0c-0305e82c3301", "sortOrder": 0, "isPinned": false }
  ],
  "items": [
    {
      "id": "3f2504e0-4f89-41d3-9a0c-0305e82c3311",
      "shelfId": "3f2504e0-4f89-41d3-9a0c-0305e82c3301",
      "type": 0,
      "target": "C:\\work\\report.xlsx",
      "displayName": "report.xlsx",
      "memo": "月次",
      "sortOrder": 3,
      "createdAt": "2026-01-05T12:00:00.0000000Z",
      "lastAccessedAt": "2026-02-19T09:30:00.0000000Z"
    },
    {
      "id": "3f2504e0-4f89-41d3-9a0c-0305e82c3312",
      "shelfId": "3f2504e0-4f89-41d3-9a0c-0305e82c3302",
      "type": 2,
      "target": "https://example.com",
      "displayName": "example.com",
      "sortOrder": 0,
      "createdAt": "2026-01-06T12:00:00.0000000Z"
    }
  ]
}"#;

#[test]
fn a_file_from_the_migration_tool_can_be_imported() {
    let dir = TempDir::new().unwrap();
    let paths = StorePaths::in_dir(dir.path());
    let (store, _) = JsonStore::load(paths.clone());

    let data: ExportData = serde_json::from_str(MIGRATED).expect("道具の出力を読めること");
    let summary = import_data(&store, &store, &data, ImportMode::ReplaceAll);

    assert_eq!(summary.shelves_imported, 2);
    assert_eq!(summary.items_imported, 2);
    assert_eq!(summary.shelves_skipped, 0);
    assert_eq!(summary.items_skipped, 0);

    // 第 6 節の観点で照合する
    let shelves = ShelfRepository::all(&store);
    let items = ItemRepository::all(&store);
    assert_eq!(shelves.len(), 2);
    assert_eq!(items.len(), 2);

    let work = shelves.iter().find(|s| s.name() == "仕事").unwrap();
    let monthly = shelves.iter().find(|s| s.name() == "月次").unwrap();
    assert!(work.is_pinned(), "ピン留めが保たれる");
    assert_eq!(monthly.parent_id(), Some(work.id()), "階層が保たれる");

    let report = items
        .iter()
        .find(|i| i.display_name() == "report.xlsx")
        .unwrap();
    assert_eq!(report.target(), "C:\\work\\report.xlsx");
    assert_eq!(report.memo(), Some("月次"), "メモが保たれる");
    assert_eq!(report.sort_order(), 3, "並び順が保たれる");
    assert!(
        report.last_accessed_at().is_some(),
        "最終アクセスが保たれる"
    );

    let site = items
        .iter()
        .find(|i| i.display_name() == "example.com")
        .unwrap();
    assert_eq!(site.item_type(), ItemType::Url, "種別が保たれる");
    assert_eq!(site.last_accessed_at(), None, "未設定は未設定のまま");

    // 保存して読み直しても残る
    store.flush().unwrap();
    drop(store);
    let (reloaded, _) = JsonStore::load(paths);
    assert_eq!(ShelfRepository::all(&reloaded).len(), 2);
    assert_eq!(ItemRepository::all(&reloaded).len(), 2);
}

#[test]
fn importing_the_migrated_file_twice_adds_nothing() {
    let dir = TempDir::new().unwrap();
    let (store, _) = JsonStore::load(StorePaths::in_dir(dir.path()));
    let data: ExportData = serde_json::from_str(MIGRATED).unwrap();

    import_data(&store, &store, &data, ImportMode::Merge);
    let second = import_data(&store, &store, &data, ImportMode::Merge);

    assert_eq!(second.shelves_imported, 0);
    assert_eq!(second.items_imported, 0);
    assert_eq!(ItemRepository::all(&store).len(), 2);
}
