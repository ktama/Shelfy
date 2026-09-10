//! Item の操作（doc/SPECIFICATION.md 第 4.2 節）

use serde::Serialize;

use crate::domain::{Item, ItemId, ItemType, ShelfId};
use crate::ports::{Clock, ExistenceChecker, ItemRepository, ShelfRepository};
use crate::usecases::shelves::next_sort_order;

#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum AddItemResult {
    Success { item: Item },
    ValidationError { message: String },
    ShelfNotFound { shelf_id: ShelfId },
    DuplicateItem { existing_id: ItemId },
}

/// Shelf に参照を追加する。並び順は同一 Shelf 内の最大値 + 1。
pub fn add_item(
    items: &dyn ItemRepository,
    shelves: &dyn ShelfRepository,
    clock: &dyn Clock,
    shelf_id: ShelfId,
    item_type: ItemType,
    target: &str,
    display_name: &str,
) -> AddItemResult {
    add_item_with_memo(
        items,
        shelves,
        clock,
        shelf_id,
        item_type,
        target,
        display_name,
        None,
    )
}

#[allow(clippy::too_many_arguments)]
pub fn add_item_with_memo(
    items: &dyn ItemRepository,
    shelves: &dyn ShelfRepository,
    clock: &dyn Clock,
    shelf_id: ShelfId,
    item_type: ItemType,
    target: &str,
    display_name: &str,
    memo: Option<String>,
) -> AddItemResult {
    if target.trim().is_empty() {
        return AddItemResult::ValidationError {
            message: "target cannot be empty".into(),
        };
    }
    if display_name.trim().is_empty() {
        return AddItemResult::ValidationError {
            message: "display name cannot be empty".into(),
        };
    }

    if shelves.get(shelf_id).is_none() {
        return AddItemResult::ShelfNotFound { shelf_id };
    }

    let existing = items.by_shelf(shelf_id);
    if let Some(dup) = existing
        .iter()
        .find(|i| i.is_same_reference(item_type, target))
    {
        return AddItemResult::DuplicateItem {
            existing_id: dup.id(),
        };
    }

    let sort_order = next_sort_order(existing.iter().map(|i| i.sort_order()));

    let item = match Item::new(
        ItemId::new(),
        shelf_id,
        item_type,
        target,
        display_name,
        clock.now_utc(),
        memo,
        sort_order,
        None,
    ) {
        Ok(i) => i,
        Err(e) => {
            return AddItemResult::ValidationError {
                message: e.to_string(),
            }
        }
    };

    items.add(item.clone());
    AddItemResult::Success { item }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum RemoveItemResult {
    Success { item_id: ItemId },
    ItemNotFound { item_id: ItemId },
}

/// 参照だけを消す。参照先の実体には触れない。
pub fn remove_item(items: &dyn ItemRepository, item_id: ItemId) -> RemoveItemResult {
    if items.get(item_id).is_none() {
        return RemoveItemResult::ItemNotFound { item_id };
    }
    items.delete(item_id);
    RemoveItemResult::Success { item_id }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum RenameItemResult {
    Success { item: Item },
    ValidationError { message: String },
    ItemNotFound { item_id: ItemId },
}

pub fn rename_item(
    items: &dyn ItemRepository,
    item_id: ItemId,
    new_display_name: &str,
) -> RenameItemResult {
    if new_display_name.trim().is_empty() {
        return RenameItemResult::ValidationError {
            message: "display name cannot be empty".into(),
        };
    }

    let Some(mut item) = items.get(item_id) else {
        return RenameItemResult::ItemNotFound { item_id };
    };

    if let Err(e) = item.rename(new_display_name) {
        return RenameItemResult::ValidationError {
            message: e.to_string(),
        };
    }

    items.update(item.clone());
    RenameItemResult::Success { item }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum UpdateItemMemoResult {
    Success { item: Item },
    ItemNotFound { item_id: ItemId },
}

/// メモを更新する。`None` で未設定に戻せる。
pub fn update_item_memo(
    items: &dyn ItemRepository,
    item_id: ItemId,
    memo: Option<String>,
) -> UpdateItemMemoResult {
    let Some(mut item) = items.get(item_id) else {
        return UpdateItemMemoResult::ItemNotFound { item_id };
    };
    item.update_memo(memo);
    items.update(item.clone());
    UpdateItemMemoResult::Success { item }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum MoveItemToShelfResult {
    Success { item: Item },
    ItemNotFound { item_id: ItemId },
    ShelfNotFound { shelf_id: ShelfId },
    DuplicateItem { existing_id: ItemId },
}

/// 別の Shelf へ移す。移動先の末尾に置く。
pub fn move_item_to_shelf(
    items: &dyn ItemRepository,
    shelves: &dyn ShelfRepository,
    item_id: ItemId,
    target_shelf: ShelfId,
) -> MoveItemToShelfResult {
    let Some(mut item) = items.get(item_id) else {
        return MoveItemToShelfResult::ItemNotFound { item_id };
    };

    if shelves.get(target_shelf).is_none() {
        return MoveItemToShelfResult::ShelfNotFound {
            shelf_id: target_shelf,
        };
    }

    let existing = items.by_shelf(target_shelf);
    if let Some(dup) = existing
        .iter()
        .find(|i| i.id() != item_id && i.is_same_reference(item.item_type(), item.target()))
    {
        return MoveItemToShelfResult::DuplicateItem {
            existing_id: dup.id(),
        };
    }

    let sort_order = next_sort_order(existing.iter().map(|i| i.sort_order()));
    item.set_sort_order(sort_order);
    item.move_to_shelf(target_shelf);
    items.update(item.clone());

    MoveItemToShelfResult::Success { item }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum ReorderItemsResult {
    Success { items: Vec<Item> },
    ItemNotFound { item_id: ItemId },
}

/// 与えられた順に 0 から始まる連番を割り当てる。
pub fn reorder_items(items: &dyn ItemRepository, ordered: &[ItemId]) -> ReorderItemsResult {
    if ordered.is_empty() {
        return ReorderItemsResult::Success { items: Vec::new() };
    }

    let mut updated = Vec::with_capacity(ordered.len());
    for (index, id) in ordered.iter().enumerate() {
        let Some(mut item) = items.get(*id) else {
            return ReorderItemsResult::ItemNotFound { item_id: *id };
        };
        item.set_sort_order(index as i32);
        updated.push(item);
    }

    for item in &updated {
        items.update(item.clone());
    }

    ReorderItemsResult::Success { items: updated }
}

/// Shelf 名を添えた Item
#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ItemWithShelf {
    pub item: Item,
    pub shelf_name: String,
}

/// Shelf 名が解決できないときの表示
pub const UNKNOWN_SHELF: &str = "Unknown";

pub(crate) fn with_shelf_names(
    shelves: &dyn ShelfRepository,
    found: Vec<Item>,
) -> Vec<ItemWithShelf> {
    let names: std::collections::HashMap<ShelfId, String> = shelves
        .all()
        .into_iter()
        .map(|s| (s.id(), s.name().to_string()))
        .collect();

    found
        .into_iter()
        .map(|item| {
            let shelf_name = names
                .get(&item.shelf_id())
                .cloned()
                .unwrap_or_else(|| UNKNOWN_SHELF.to_string());
            ItemWithShelf { item, shelf_name }
        })
        .collect()
}

/// 最近起動した Item を、最終アクセス日時の降順で返す
pub fn get_recent_items(
    items: &dyn ItemRepository,
    shelves: &dyn ShelfRepository,
    count: Option<usize>,
) -> Vec<ItemWithShelf> {
    let take = count.unwrap_or(crate::ports::settings_keys::DEFAULT_RECENT_ITEMS_COUNT);
    with_shelf_names(shelves, items.recent(take))
}

/// 参照先が存在しない Item を返す
pub fn get_missing_items(
    items: &dyn ItemRepository,
    shelves: &dyn ShelfRepository,
    checker: &dyn ExistenceChecker,
) -> Vec<ItemWithShelf> {
    let missing: Vec<Item> = items
        .all()
        .into_iter()
        .filter(|i| !checker.exists(i.target()))
        .collect();
    with_shelf_names(shelves, missing)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::testing::{
        FakeExistenceChecker, FixedClock, InMemoryItemRepository, InMemoryShelfRepository,
    };
    use crate::usecases::shelves::{create_shelf, CreateShelfResult};
    use time::macros::datetime;

    struct Fixture {
        items: InMemoryItemRepository,
        shelves: InMemoryShelfRepository,
        clock: FixedClock,
        shelf: ShelfId,
    }

    fn fixture() -> Fixture {
        let shelves = InMemoryShelfRepository::new();
        let shelf = match create_shelf(&shelves, "仕事", None) {
            CreateShelfResult::Success { shelf } => shelf.id(),
            other => panic!("{other:?}"),
        };
        Fixture {
            items: InMemoryItemRepository::new(),
            shelves,
            clock: FixedClock::new(datetime!(2026-01-05 12:00:00 UTC)),
            shelf,
        }
    }

    fn added(result: AddItemResult) -> Item {
        match result {
            AddItemResult::Success { item } => item,
            other => panic!("expected success, got {other:?}"),
        }
    }

    fn add(f: &Fixture, target: &str, name: &str) -> Item {
        added(add_item(
            &f.items,
            &f.shelves,
            &f.clock,
            f.shelf,
            ItemType::File,
            target,
            name,
        ))
    }

    // --- AddItem ---

    #[test]
    fn add_item_rejects_blank_target_and_name() {
        let f = fixture();
        assert!(matches!(
            add_item(
                &f.items,
                &f.shelves,
                &f.clock,
                f.shelf,
                ItemType::File,
                " ",
                "n"
            ),
            AddItemResult::ValidationError { .. }
        ));
        assert!(matches!(
            add_item(
                &f.items,
                &f.shelves,
                &f.clock,
                f.shelf,
                ItemType::File,
                "C:\\a",
                " "
            ),
            AddItemResult::ValidationError { .. }
        ));
        assert_eq!(f.items.count(), 0);
    }

    #[test]
    fn add_item_reports_missing_shelf() {
        let f = fixture();
        let missing = ShelfId::new();
        assert_eq!(
            add_item(
                &f.items,
                &f.shelves,
                &f.clock,
                missing,
                ItemType::File,
                "C:\\a",
                "a"
            ),
            AddItemResult::ShelfNotFound { shelf_id: missing }
        );
    }

    #[test]
    fn add_item_rejects_duplicate_paths_ignoring_case() {
        let f = fixture();
        let first = add(&f, "C:\\Work\\A.txt", "A");
        let r = add_item(
            &f.items,
            &f.shelves,
            &f.clock,
            f.shelf,
            ItemType::File,
            "c:\\work\\a.txt",
            "同じもの",
        );
        assert_eq!(
            r,
            AddItemResult::DuplicateItem {
                existing_id: first.id()
            }
        );
        assert_eq!(f.items.count(), 1);
    }

    #[test]
    fn add_item_treats_urls_case_sensitively() {
        let f = fixture();
        add_item(
            &f.items,
            &f.shelves,
            &f.clock,
            f.shelf,
            ItemType::Url,
            "https://example.com/Path",
            "A",
        );
        let r = add_item(
            &f.items,
            &f.shelves,
            &f.clock,
            f.shelf,
            ItemType::Url,
            "https://example.com/path",
            "B",
        );
        assert!(matches!(r, AddItemResult::Success { .. }));
        assert_eq!(f.items.count(), 2);
    }

    #[test]
    fn add_item_allows_same_target_with_different_type() {
        let f = fixture();
        add(&f, "C:\\Work", "file");
        let r = add_item(
            &f.items,
            &f.shelves,
            &f.clock,
            f.shelf,
            ItemType::Folder,
            "C:\\Work",
            "folder",
        );
        assert!(matches!(r, AddItemResult::Success { .. }));
    }

    #[test]
    fn add_item_numbers_sort_order_from_zero_within_a_shelf() {
        let f = fixture();
        let a = add(&f, "C:\\1.txt", "1");
        let b = add(&f, "C:\\2.txt", "2");
        assert_eq!(a.sort_order(), 0);
        assert_eq!(b.sort_order(), 1);
    }

    #[test]
    fn add_item_records_creation_time_from_the_clock() {
        let f = fixture();
        let i = add(&f, "C:\\1.txt", "1");
        assert_eq!(i.created_at(), datetime!(2026-01-05 12:00:00 UTC));
        assert_eq!(i.last_accessed_at(), None);
    }

    // --- RemoveItem ---

    #[test]
    fn remove_item_deletes_only_the_reference() {
        let f = fixture();
        let i = add(&f, "C:\\1.txt", "1");
        assert_eq!(
            remove_item(&f.items, i.id()),
            RemoveItemResult::Success { item_id: i.id() }
        );
        assert_eq!(f.items.count(), 0);
    }

    #[test]
    fn remove_item_reports_missing_item() {
        let f = fixture();
        let missing = ItemId::new();
        assert_eq!(
            remove_item(&f.items, missing),
            RemoveItemResult::ItemNotFound { item_id: missing }
        );
    }

    // --- RenameItem ---

    #[test]
    fn rename_item_checks_input_and_existence() {
        let f = fixture();
        let i = add(&f, "C:\\1.txt", "1");

        assert!(matches!(
            rename_item(&f.items, i.id(), "  "),
            RenameItemResult::ValidationError { .. }
        ));

        let missing = ItemId::new();
        assert_eq!(
            rename_item(&f.items, missing, "x"),
            RenameItemResult::ItemNotFound { item_id: missing }
        );

        assert!(matches!(
            rename_item(&f.items, i.id(), "報告書"),
            RenameItemResult::Success { .. }
        ));
        assert_eq!(f.items.get(i.id()).unwrap().display_name(), "報告書");
    }

    // --- UpdateItemMemo ---

    #[test]
    fn update_memo_sets_and_clears() {
        let f = fixture();
        let i = add(&f, "C:\\1.txt", "1");

        update_item_memo(&f.items, i.id(), Some("月次".into()));
        assert_eq!(f.items.get(i.id()).unwrap().memo(), Some("月次"));

        update_item_memo(&f.items, i.id(), None);
        assert_eq!(f.items.get(i.id()).unwrap().memo(), None);

        let missing = ItemId::new();
        assert_eq!(
            update_item_memo(&f.items, missing, None),
            UpdateItemMemoResult::ItemNotFound { item_id: missing }
        );
    }

    // --- MoveItemToShelf ---

    #[test]
    fn move_item_appends_to_the_target_shelf() {
        let f = fixture();
        let other = match create_shelf(&f.shelves, "個人", None) {
            CreateShelfResult::Success { shelf } => shelf.id(),
            other => panic!("{other:?}"),
        };
        added(add_item(
            &f.items,
            &f.shelves,
            &f.clock,
            other,
            ItemType::File,
            "C:\\x.txt",
            "x",
        ));
        let moving = add(&f, "C:\\1.txt", "1");

        let r = move_item_to_shelf(&f.items, &f.shelves, moving.id(), other);
        assert!(matches!(r, MoveItemToShelfResult::Success { .. }));

        let moved = f.items.get(moving.id()).unwrap();
        assert_eq!(moved.shelf_id(), other);
        assert_eq!(moved.sort_order(), 1);
    }

    #[test]
    fn move_item_rejects_duplicate_in_the_target_shelf() {
        let f = fixture();
        let other = match create_shelf(&f.shelves, "個人", None) {
            CreateShelfResult::Success { shelf } => shelf.id(),
            o => panic!("{o:?}"),
        };
        let existing = added(add_item(
            &f.items,
            &f.shelves,
            &f.clock,
            other,
            ItemType::File,
            "C:\\same.txt",
            "same",
        ));
        let moving = add(&f, "C:\\SAME.txt", "same");

        assert_eq!(
            move_item_to_shelf(&f.items, &f.shelves, moving.id(), other),
            MoveItemToShelfResult::DuplicateItem {
                existing_id: existing.id()
            }
        );
        assert_eq!(f.items.get(moving.id()).unwrap().shelf_id(), f.shelf);
    }

    #[test]
    fn move_item_reports_missing_item_and_shelf() {
        let f = fixture();
        let i = add(&f, "C:\\1.txt", "1");
        let missing_item = ItemId::new();
        let missing_shelf = ShelfId::new();

        assert_eq!(
            move_item_to_shelf(&f.items, &f.shelves, missing_item, f.shelf),
            MoveItemToShelfResult::ItemNotFound {
                item_id: missing_item
            }
        );
        assert_eq!(
            move_item_to_shelf(&f.items, &f.shelves, i.id(), missing_shelf),
            MoveItemToShelfResult::ShelfNotFound {
                shelf_id: missing_shelf
            }
        );
    }

    #[test]
    fn move_item_to_the_same_shelf_ignores_itself_when_checking_duplicates() {
        let f = fixture();
        let i = add(&f, "C:\\1.txt", "1");
        let r = move_item_to_shelf(&f.items, &f.shelves, i.id(), f.shelf);
        assert!(matches!(r, MoveItemToShelfResult::Success { .. }));
    }

    // --- ReorderItems ---

    #[test]
    fn reorder_items_assigns_consecutive_numbers() {
        let f = fixture();
        let a = add(&f, "C:\\1.txt", "1");
        let b = add(&f, "C:\\2.txt", "2");
        let c = add(&f, "C:\\3.txt", "3");

        assert!(matches!(
            reorder_items(&f.items, &[c.id(), a.id(), b.id()]),
            ReorderItemsResult::Success { .. }
        ));
        assert_eq!(f.items.get(c.id()).unwrap().sort_order(), 0);
        assert_eq!(f.items.get(a.id()).unwrap().sort_order(), 1);
        assert_eq!(f.items.get(b.id()).unwrap().sort_order(), 2);
    }

    #[test]
    fn reorder_items_accepts_empty_and_rejects_unknown() {
        let f = fixture();
        assert_eq!(
            reorder_items(&f.items, &[]),
            ReorderItemsResult::Success { items: Vec::new() }
        );

        let a = add(&f, "C:\\1.txt", "1");
        let missing = ItemId::new();
        assert_eq!(
            reorder_items(&f.items, &[missing, a.id()]),
            ReorderItemsResult::ItemNotFound { item_id: missing }
        );
        assert_eq!(f.items.get(a.id()).unwrap().sort_order(), 0);
    }

    // --- GetRecentItems ---

    #[test]
    fn recent_items_exclude_never_launched_and_sort_by_latest() {
        let f = fixture();
        let a = add(&f, "C:\\1.txt", "1");
        let b = add(&f, "C:\\2.txt", "2");
        let _never = add(&f, "C:\\3.txt", "3");

        let mut a2 = f.items.get(a.id()).unwrap();
        a2.mark_accessed(datetime!(2026-02-01 10:00:00 UTC));
        f.items.update(a2);
        let mut b2 = f.items.get(b.id()).unwrap();
        b2.mark_accessed(datetime!(2026-02-05 10:00:00 UTC));
        f.items.update(b2);

        let recent = get_recent_items(&f.items, &f.shelves, None);
        assert_eq!(recent.len(), 2);
        assert_eq!(recent[0].item.id(), b.id());
        assert_eq!(recent[1].item.id(), a.id());
        assert_eq!(recent[0].shelf_name, "仕事");
    }

    #[test]
    fn recent_items_respect_the_requested_count() {
        let f = fixture();
        for n in 0..5 {
            let i = add(&f, &format!("C:\\{n}.txt"), &format!("{n}"));
            let mut i2 = f.items.get(i.id()).unwrap();
            i2.mark_accessed(datetime!(2026-02-01 10:00:00 UTC) + time::Duration::minutes(n));
            f.items.update(i2);
        }
        assert_eq!(get_recent_items(&f.items, &f.shelves, Some(2)).len(), 2);
        assert_eq!(get_recent_items(&f.items, &f.shelves, None).len(), 5);
    }

    // --- GetMissingItems ---

    #[test]
    fn missing_items_report_only_absent_targets() {
        let f = fixture();
        add(&f, "C:\\present.txt", "present");
        let gone = add(&f, "C:\\gone.txt", "gone");
        added(add_item(
            &f.items,
            &f.shelves,
            &f.clock,
            f.shelf,
            ItemType::Url,
            "https://example.com",
            "url",
        ));

        let checker = FakeExistenceChecker::with(&["C:\\present.txt"]);
        let missing = get_missing_items(&f.items, &f.shelves, &checker);

        assert_eq!(missing.len(), 1);
        assert_eq!(missing[0].item.id(), gone.id());
        assert_eq!(missing[0].shelf_name, "仕事");
    }

    #[test]
    fn shelf_name_falls_back_when_the_shelf_is_gone() {
        let f = fixture();
        let i = add(&f, "C:\\1.txt", "1");
        f.shelves.delete(f.shelf);

        let checker = FakeExistenceChecker::none();
        let missing = get_missing_items(&f.items, &f.shelves, &checker);
        assert_eq!(missing.len(), 1);
        assert_eq!(missing[0].item.id(), i.id());
        assert_eq!(missing[0].shelf_name, UNKNOWN_SHELF);
    }
}
