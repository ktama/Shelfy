//! フェーズ 3 の完了条件の確認。
//! フェーズ 2 で試験用の実装に対して確かめた振る舞いが、
//! 実ファイルを使う `JsonStore` でも同じように成り立つことを見る。

use std::time::Duration;

use shelfy::adapters::store::{JsonStore, LoadOutcome, StorePaths};
use shelfy::domain::{ItemType, ShelfId};
use shelfy::ports::{settings_keys, ItemRepository, SettingsRepository, ShelfRepository};
use shelfy::testing::{FakeExistenceChecker, FakeHotkeyHoldState, FakeLauncher, FixedClock};
use shelfy::usecases::items::*;
use shelfy::usecases::launch::*;
use shelfy::usecases::search::search_items;
use shelfy::usecases::shelves::*;
use shelfy::usecases::transfer::*;
use tempfile::TempDir;
use time::macros::datetime;

fn clock() -> FixedClock {
    FixedClock::new(datetime!(2026-01-05 12:00:00 UTC))
}

fn shelf_id(result: CreateShelfResult) -> ShelfId {
    match result {
        CreateShelfResult::Success { shelf } => shelf.id(),
        other => panic!("expected success, got {other:?}"),
    }
}

fn item_id(result: AddItemResult) -> shelfy::domain::ItemId {
    match result {
        AddItemResult::Success { item } => item.id(),
        other => panic!("expected success, got {other:?}"),
    }
}

/// Shelf を作り、Item を並べ、並び替えとピン留めとメモまで行う。
/// 保存して読み直したあと、すべてが残っていることを確かめる。
#[test]
fn a_full_session_survives_a_save_and_reload() {
    let dir = TempDir::new().unwrap();
    let paths = StorePaths::in_dir(dir.path());
    let clock = clock();

    let (store, outcome) = JsonStore::load(paths.clone());
    assert_eq!(outcome, LoadOutcome::Fresh);

    // 階層を作る
    let work = shelf_id(create_shelf(&store, "仕事", None));
    let monthly = shelf_id(create_shelf(&store, "月次", Some(work)));
    let personal = shelf_id(create_shelf(&store, "個人", None));

    // Item を並べる
    let report = item_id(add_item(
        &store,
        &store,
        &clock,
        work,
        ItemType::File,
        "C:\\work\\report.xlsx",
        "報告書",
    ));
    let design = item_id(add_item(
        &store,
        &store,
        &clock,
        work,
        ItemType::Folder,
        "C:\\work\\design",
        "設計資料",
    ));
    let docs = item_id(add_item(
        &store,
        &store,
        &clock,
        personal,
        ItemType::Url,
        "https://example.com/docs",
        "ドキュメント",
    ));

    // 重複は拒まれる（大文字小文字を区別しない）
    assert!(matches!(
        add_item(
            &store,
            &store,
            &clock,
            work,
            ItemType::File,
            "c:\\work\\REPORT.xlsx",
            "同じもの"
        ),
        AddItemResult::DuplicateItem { .. }
    ));

    // 並び替え、ピン留め、メモ、別の棚への移動
    assert!(matches!(
        reorder_items(&store, &[design, report]),
        ReorderItemsResult::Success { .. }
    ));
    assert!(matches!(
        toggle_pin_shelf(&store, work),
        TogglePinShelfResult::Success {
            is_pinned: true,
            ..
        }
    ));
    assert!(matches!(
        update_item_memo(&store, report, Some("経理向け".into())),
        UpdateItemMemoResult::Success { .. }
    ));
    assert!(matches!(
        move_item_to_shelf(&store, &store, docs, monthly),
        MoveItemToShelfResult::Success { .. }
    ));

    // 起動して最終アクセス日時を記録する
    let launcher = FakeLauncher::new();
    let hotkey = FakeHotkeyHoldState::released();
    clock.set(datetime!(2026-02-20 09:30:00 UTC));
    assert_eq!(
        launch_item(&store, &launcher, &hotkey, &clock, report),
        LaunchItemResult::Success {
            post_action: PostLaunchAction::HideWindow
        }
    );

    // 設定も同じファイルに入る
    store.set(settings_keys::GLOBAL_HOTKEY, "Ctrl+Alt+S");
    store.set(settings_keys::RECENT_ITEMS_COUNT, "10");

    store.flush().unwrap();
    drop(store);

    // 読み直す
    let (store, outcome) = JsonStore::load(paths);
    assert_eq!(outcome, LoadOutcome::Loaded);

    assert_eq!(ShelfRepository::all(&store).len(), 3);
    assert_eq!(ItemRepository::all(&store).len(), 3);

    let work_shelf = ShelfRepository::get(&store, work).unwrap();
    assert!(work_shelf.is_pinned());
    assert_eq!(
        ShelfRepository::get(&store, monthly).unwrap().parent_id(),
        Some(work)
    );

    // 並び順が保たれている
    let in_work = ItemRepository::by_shelf(&store, work);
    assert_eq!(in_work.len(), 2);
    assert_eq!(in_work[0].id(), design);
    assert_eq!(in_work[1].id(), report);

    // メモ、移動先、最終アクセス日時が保たれている
    let restored_report = ItemRepository::get(&store, report).unwrap();
    assert_eq!(restored_report.memo(), Some("経理向け"));
    assert_eq!(
        restored_report.last_accessed_at(),
        Some(datetime!(2026-02-20 09:30:00 UTC))
    );
    assert_eq!(
        ItemRepository::get(&store, docs).unwrap().shelf_id(),
        monthly
    );

    // 設定が保たれている
    assert_eq!(
        SettingsRepository::get(&store, settings_keys::GLOBAL_HOTKEY).as_deref(),
        Some("Ctrl+Alt+S")
    );
}

#[test]
fn search_and_the_listing_views_work_over_the_file_store() {
    let dir = TempDir::new().unwrap();
    let clock = clock();
    let (store, _) = JsonStore::load(StorePaths::in_dir(dir.path()));

    let work = shelf_id(create_shelf(&store, "仕事", None));
    let tools = shelf_id(create_shelf(&store, "ツール", None));

    let report = item_id(add_item_with_memo(
        &store,
        &store,
        &clock,
        work,
        ItemType::File,
        "C:\\work\\report.xlsx",
        "月次レポート",
        Some("経理向け".into()),
    ));
    let viewer = item_id(add_item(
        &store,
        &store,
        &clock,
        tools,
        ItemType::File,
        "C:\\tools\\report-viewer.exe",
        "ビューア",
    ));
    let site = item_id(add_item(
        &store,
        &store,
        &clock,
        tools,
        ItemType::Url,
        "https://example.com",
        "サイト",
    ));

    // フリーテキストは表示名、パス、メモに当たる
    assert_eq!(search_items(&store, &store, "経理").len(), 1);
    assert_eq!(search_items(&store, &store, "report").len(), 2);

    // Shelf 名に当たれば棚ごと出る
    assert_eq!(search_items(&store, &store, "ツール").len(), 2);

    // 絞り込み
    let found = search_items(&store, &store, "type:url");
    assert_eq!(found.len(), 1);
    assert_eq!(found[0].item.id(), site);
    assert_eq!(found[0].shelf_name, "ツール");

    assert_eq!(search_items(&store, &store, "report in:ツール").len(), 1);
    assert_eq!(search_items(&store, &store, "type:shortcut").len(), 3);
    assert!(search_items(&store, &store, "").is_empty());

    // 最近使ったもの
    let launcher = FakeLauncher::new();
    let hotkey = FakeHotkeyHoldState::released();
    clock.set(datetime!(2026-02-01 10:00:00 UTC));
    launch_item(&store, &launcher, &hotkey, &clock, viewer);
    clock.set(datetime!(2026-02-05 10:00:00 UTC));
    launch_item(&store, &launcher, &hotkey, &clock, report);

    let recent = get_recent_items(&store, &store, None);
    assert_eq!(recent.len(), 2);
    assert_eq!(recent[0].item.id(), report);
    assert_eq!(recent[1].item.id(), viewer);

    // 欠損したもの
    let checker = FakeExistenceChecker::with(&["C:\\work\\report.xlsx"]);
    let missing = get_missing_items(&store, &store, &checker);
    assert_eq!(missing.len(), 1);
    assert_eq!(missing[0].item.id(), viewer);
}

#[test]
fn deleting_a_shelf_removes_its_descendants_from_the_saved_file() {
    let dir = TempDir::new().unwrap();
    let paths = StorePaths::in_dir(dir.path());
    let clock = clock();
    let (store, _) = JsonStore::load(paths.clone());

    let root = shelf_id(create_shelf(&store, "root", None));
    let child = shelf_id(create_shelf(&store, "child", Some(root)));
    let other = shelf_id(create_shelf(&store, "other", None));
    add_item(
        &store,
        &store,
        &clock,
        root,
        ItemType::File,
        "C:\\1.txt",
        "1",
    );
    add_item(
        &store,
        &store,
        &clock,
        child,
        ItemType::File,
        "C:\\2.txt",
        "2",
    );
    add_item(
        &store,
        &store,
        &clock,
        other,
        ItemType::File,
        "C:\\3.txt",
        "3",
    );

    assert_eq!(
        delete_shelf(&store, &store, root),
        DeleteShelfResult::Success {
            deleted_shelves: 2,
            deleted_items: 2
        }
    );
    store.flush().unwrap();
    drop(store);

    let (store, _) = JsonStore::load(paths);
    assert_eq!(ShelfRepository::all(&store).len(), 1);
    assert_eq!(ItemRepository::all(&store).len(), 1);
    assert!(ShelfRepository::get(&store, other).is_some());
}

#[test]
fn export_from_one_file_can_be_imported_into_another() {
    let source_dir = TempDir::new().unwrap();
    let target_dir = TempDir::new().unwrap();
    let clock = clock();

    let (source, _) = JsonStore::load(StorePaths::in_dir(source_dir.path()));
    let work = shelf_id(create_shelf(&source, "仕事", None));
    let monthly = shelf_id(create_shelf(&source, "月次", Some(work)));
    add_item(
        &source,
        &source,
        &clock,
        work,
        ItemType::File,
        "C:\\1.txt",
        "1",
    );
    add_item(
        &source,
        &source,
        &clock,
        monthly,
        ItemType::Url,
        "https://example.com",
        "2",
    );
    source.flush().unwrap();

    let exported = export_data(&source, &source, &clock);
    let text = serde_json::to_string_pretty(&exported).unwrap();

    let target_paths = StorePaths::in_dir(target_dir.path());
    let (target, _) = JsonStore::load(target_paths.clone());
    let restored: ExportData = serde_json::from_str(&text).unwrap();
    let summary = import_data(&target, &target, &restored, ImportMode::ReplaceAll);

    assert_eq!(summary.shelves_imported, 2);
    assert_eq!(summary.items_imported, 2);
    assert_eq!(summary.items_skipped, 0);

    // 取り込みの直後は待たずに書き出す
    target.flush().unwrap();
    drop(target);

    let (reloaded, outcome) = JsonStore::load(target_paths);
    assert_eq!(outcome, LoadOutcome::Loaded);
    assert_eq!(ShelfRepository::all(&reloaded).len(), 2);
    assert_eq!(ItemRepository::all(&reloaded).len(), 2);
    assert_eq!(
        ShelfRepository::get(&reloaded, monthly)
            .unwrap()
            .parent_id(),
        Some(work)
    );
}

#[test]
fn an_abrupt_end_loses_only_what_was_never_flushed() {
    let dir = TempDir::new().unwrap();
    let paths = StorePaths::in_dir(dir.path());
    let clock = clock();

    let (store, _) = JsonStore::load(paths.clone());
    let work = shelf_id(create_shelf(&store, "仕事", None));
    add_item(
        &store,
        &store,
        &clock,
        work,
        ItemType::File,
        "C:\\1.txt",
        "1",
    );
    store.flush().unwrap();

    // ここから先は保存しないまま終わる
    add_item(
        &store,
        &store,
        &clock,
        work,
        ItemType::File,
        "C:\\2.txt",
        "2",
    );
    assert!(store.is_dirty());
    drop(store);

    let (reloaded, outcome) = JsonStore::load(paths);
    assert_eq!(outcome, LoadOutcome::Loaded);
    // 保存済みのものは残っている
    assert_eq!(ItemRepository::all(&reloaded).len(), 1);
}

#[test]
fn repeated_edits_are_written_once_after_the_delay() {
    let dir = TempDir::new().unwrap();
    let paths = StorePaths::in_dir(dir.path());
    let clock = clock();
    let (store, _) = JsonStore::load(paths.clone());

    let work = shelf_id(create_shelf(&store, "仕事", None));
    for n in 0..20 {
        add_item(
            &store,
            &store,
            &clock,
            work,
            ItemType::File,
            &format!("C:\\{n}.txt"),
            &format!("{n}"),
        );
    }

    // 待ち時間が過ぎるまでは書かない
    assert!(!store.flush_if_due(Duration::from_secs(60)).unwrap());
    assert!(!paths.main.exists());

    // 過ぎれば 1 回だけ書く
    assert!(store.flush_if_due(Duration::ZERO).unwrap());
    assert!(!store.flush_if_due(Duration::ZERO).unwrap());

    drop(store);
    let (reloaded, _) = JsonStore::load(paths);
    assert_eq!(ItemRepository::all(&reloaded).len(), 20);
}
