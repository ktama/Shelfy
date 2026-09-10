//! 性能の計測（doc/rebuild/05-REBUILD_PLAN.md 第 5.3 節）。
//!
//! 目標は [02-SPECIFICATION.md](../../doc/rebuild/02-SPECIFICATION.md) 第 11 節にある。
//! 実ファイルを使うストアに対して、想定の上限に近い量で測る。

use std::time::{Duration, Instant};

use shelfy::adapters::store::{JsonStore, StorePaths};
use shelfy::domain::{ItemType, ShelfId};
use shelfy::ports::{ItemRepository, ShelfRepository};
use shelfy::testing::FixedClock;
use shelfy::usecases::items::add_item;
use shelfy::usecases::search::search_items;
use shelfy::usecases::shelves::{create_shelf, CreateShelfResult};
use tempfile::TempDir;
use time::macros::datetime;

/// 想定するデータ量の上限（02-SPECIFICATION.md 第 11 節）
const SHELVES: usize = 100;
const ITEMS: usize = 1_000;

/// 1 回あたりの目標
const SEARCH_BUDGET: Duration = Duration::from_millis(50);

fn build(store: &JsonStore) {
    let clock = FixedClock::new(datetime!(2026-01-05 12:00:00 UTC));

    let mut shelves: Vec<ShelfId> = Vec::with_capacity(SHELVES);
    for n in 0..SHELVES {
        match create_shelf(store, &format!("棚 {n}"), None) {
            CreateShelfResult::Success { shelf } => shelves.push(shelf.id()),
            other => panic!("{other:?}"),
        }
    }

    let kinds = [ItemType::File, ItemType::Folder, ItemType::Url];
    for n in 0..ITEMS {
        let shelf = shelves[n % SHELVES];
        let kind = kinds[n % 3];
        let target = match kind {
            ItemType::Url => format!("https://example.com/page-{n}"),
            _ => format!("C:\\work\\subdir\\report-{n}.xlsx"),
        };
        add_item(
            store,
            store,
            &clock,
            shelf,
            kind,
            &target,
            &format!("月次レポート {n}"),
        );
    }
}

/// 何度か測って中央値を返す
fn median_of<F: FnMut() -> usize>(runs: usize, mut body: F) -> (Duration, usize) {
    let mut times = Vec::with_capacity(runs);
    let mut last = 0;
    for _ in 0..runs {
        let started = Instant::now();
        last = body();
        times.push(started.elapsed());
    }
    times.sort();
    (times[times.len() / 2], last)
}

#[test]
fn search_over_a_thousand_items_stays_within_the_budget() {
    let dir = TempDir::new().unwrap();
    let (store, _) = JsonStore::load(StorePaths::in_dir(dir.path()));
    build(&store);

    assert_eq!(ShelfRepository::all(&store).len(), SHELVES);
    assert_eq!(ItemRepository::all(&store).len(), ITEMS);

    // 代表的な 3 つのクエリで測る
    let queries = [
        ("フリーテキストのみ", "レポート"),
        ("種別のみ", "type:url"),
        ("併用", "レポート type:file box:棚 1"),
    ];

    for (label, query) in queries {
        let (elapsed, hits) = median_of(100, || search_items(&store, &store, query).len());
        println!("検索「{query}」（{label}）: {elapsed:?}、{hits} 件");
        assert!(
            elapsed < SEARCH_BUDGET,
            "{label} が目標を超えた: {elapsed:?}"
        );
    }
}

#[test]
fn listing_a_shelf_stays_fast_with_a_thousand_items() {
    let dir = TempDir::new().unwrap();
    let (store, _) = JsonStore::load(StorePaths::in_dir(dir.path()));
    build(&store);

    let shelf = ShelfRepository::all(&store)[0].id();
    let (elapsed, count) = median_of(100, || ItemRepository::by_shelf(&store, shelf).len());
    println!("棚の一覧: {elapsed:?}、{count} 件");
    assert!(elapsed < SEARCH_BUDGET, "棚の一覧が遅い: {elapsed:?}");
}

#[test]
fn saving_a_thousand_items_stays_fast() {
    let dir = TempDir::new().unwrap();
    let (store, _) = JsonStore::load(StorePaths::in_dir(dir.path()));
    build(&store);

    let started = Instant::now();
    assert!(store.flush().unwrap());
    let elapsed = started.elapsed();

    let bytes = std::fs::metadata(dir.path().join("shelfy.json"))
        .unwrap()
        .len();
    println!("保存: {elapsed:?}、{bytes} バイト");

    // 保存は人の操作の裏で走るが、体感に響かない範囲に収める
    assert!(
        elapsed < Duration::from_millis(500),
        "保存が遅い: {elapsed:?}"
    );
}

#[test]
fn loading_a_thousand_items_stays_fast() {
    let dir = TempDir::new().unwrap();
    let paths = StorePaths::in_dir(dir.path());
    {
        let (store, _) = JsonStore::load(paths.clone());
        build(&store);
        store.flush().unwrap();
    }

    let started = Instant::now();
    let (store, _) = JsonStore::load(paths);
    let elapsed = started.elapsed();

    assert_eq!(ItemRepository::all(&store).len(), ITEMS);
    println!("読み込み: {elapsed:?}");

    // 起動の予算（1,000 ms）のうち、読み込みが占めてよい分
    assert!(
        elapsed < Duration::from_millis(200),
        "読み込みが遅い: {elapsed:?}"
    );
}
