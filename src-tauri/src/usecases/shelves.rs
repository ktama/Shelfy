//! Shelf の操作（doc/SPECIFICATION.md 第 4.1 節）

use serde::Serialize;

use crate::domain::{Shelf, ShelfId};
use crate::ports::{ItemRepository, ShelfRepository};

#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum CreateShelfResult {
    Success { shelf: Shelf },
    ValidationError { message: String },
    ParentNotFound { parent_id: ShelfId },
}

/// 名前と親を受け取って Shelf を作る。並び順は同一階層の最大値 + 1。
pub fn create_shelf(
    shelves: &dyn ShelfRepository,
    name: &str,
    parent_id: Option<ShelfId>,
) -> CreateShelfResult {
    if name.trim().is_empty() {
        return CreateShelfResult::ValidationError {
            message: "shelf name cannot be empty".into(),
        };
    }

    if let Some(parent) = parent_id {
        if shelves.get(parent).is_none() {
            return CreateShelfResult::ParentNotFound { parent_id: parent };
        }
    }

    let siblings = shelves.children(parent_id);
    let sort_order = next_sort_order(siblings.iter().map(|s| s.sort_order()));

    let shelf = match Shelf::new(ShelfId::new(), name, parent_id, sort_order, false) {
        Ok(s) => s,
        Err(e) => {
            return CreateShelfResult::ValidationError {
                message: e.to_string(),
            }
        }
    };

    shelves.add(shelf.clone());
    CreateShelfResult::Success { shelf }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum RenameShelfResult {
    Success { shelf: Shelf },
    ValidationError { message: String },
    ShelfNotFound { shelf_id: ShelfId },
}

pub fn rename_shelf(
    shelves: &dyn ShelfRepository,
    shelf_id: ShelfId,
    new_name: &str,
) -> RenameShelfResult {
    if new_name.trim().is_empty() {
        return RenameShelfResult::ValidationError {
            message: "shelf name cannot be empty".into(),
        };
    }

    let Some(mut shelf) = shelves.get(shelf_id) else {
        return RenameShelfResult::ShelfNotFound { shelf_id };
    };

    if let Err(e) = shelf.rename(new_name) {
        return RenameShelfResult::ValidationError {
            message: e.to_string(),
        };
    }

    shelves.update(shelf.clone());
    RenameShelfResult::Success { shelf }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum MoveShelfResult {
    Success { shelf: Shelf },
    ShelfNotFound { shelf_id: ShelfId },
    ParentNotFound { parent_id: ShelfId },
    InvalidMove { message: String },
}

/// 親を付け替える。`None` はルートへの移動。
/// 自分自身と自分の子孫は親にできない。
pub fn move_shelf(
    shelves: &dyn ShelfRepository,
    shelf_id: ShelfId,
    new_parent: Option<ShelfId>,
) -> MoveShelfResult {
    let Some(mut shelf) = shelves.get(shelf_id) else {
        return MoveShelfResult::ShelfNotFound { shelf_id };
    };

    if let Some(parent) = new_parent {
        if parent == shelf_id {
            return MoveShelfResult::InvalidMove {
                message: "cannot move a shelf into itself".into(),
            };
        }
        if shelves.get(parent).is_none() {
            return MoveShelfResult::ParentNotFound { parent_id: parent };
        }
        if is_descendant_of(shelves, parent, shelf_id) {
            return MoveShelfResult::InvalidMove {
                message: "cannot move a shelf into its own descendant".into(),
            };
        }
    }

    shelf.move_to(new_parent);
    shelves.update(shelf.clone());
    MoveShelfResult::Success { shelf }
}

/// `candidate` が `ancestor` の子孫かどうかを、親をたどって調べる
fn is_descendant_of(shelves: &dyn ShelfRepository, candidate: ShelfId, ancestor: ShelfId) -> bool {
    let mut current = shelves.get(candidate);
    // 壊れたデータで親子が輪になっている場合に備え、たどる回数に上限を置く
    let mut guard = 0;
    while let Some(shelf) = current {
        let Some(parent) = shelf.parent_id() else {
            return false;
        };
        if parent == ancestor {
            return true;
        }
        guard += 1;
        if guard > 10_000 {
            return false;
        }
        current = shelves.get(parent);
    }
    false
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum DeleteShelfResult {
    Success {
        deleted_shelves: usize,
        deleted_items: usize,
    },
    ShelfNotFound {
        shelf_id: ShelfId,
    },
}

/// 配下の Shelf と Item をまとめて削除する
pub fn delete_shelf(
    shelves: &dyn ShelfRepository,
    items: &dyn ItemRepository,
    shelf_id: ShelfId,
) -> DeleteShelfResult {
    if shelves.get(shelf_id).is_none() {
        return DeleteShelfResult::ShelfNotFound { shelf_id };
    }

    let mut deleted_shelves = 0;
    let mut deleted_items = 0;
    delete_recursive(
        shelves,
        items,
        shelf_id,
        &mut deleted_shelves,
        &mut deleted_items,
    );

    DeleteShelfResult::Success {
        deleted_shelves,
        deleted_items,
    }
}

fn delete_recursive(
    shelves: &dyn ShelfRepository,
    items: &dyn ItemRepository,
    shelf_id: ShelfId,
    deleted_shelves: &mut usize,
    deleted_items: &mut usize,
) {
    for child in shelves.children(Some(shelf_id)) {
        delete_recursive(shelves, items, child.id(), deleted_shelves, deleted_items);
    }
    *deleted_items += items.by_shelf(shelf_id).len();
    items.delete_by_shelf(shelf_id);
    shelves.delete(shelf_id);
    *deleted_shelves += 1;
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum TogglePinShelfResult {
    Success { shelf: Shelf, is_pinned: bool },
    ShelfNotFound { shelf_id: ShelfId },
}

pub fn toggle_pin_shelf(shelves: &dyn ShelfRepository, shelf_id: ShelfId) -> TogglePinShelfResult {
    let Some(mut shelf) = shelves.get(shelf_id) else {
        return TogglePinShelfResult::ShelfNotFound { shelf_id };
    };

    let is_pinned = shelf.toggle_pin();
    shelves.update(shelf.clone());
    TogglePinShelfResult::Success { shelf, is_pinned }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum ReorderShelvesResult {
    Success { shelves: Vec<Shelf> },
    ShelfNotFound { shelf_id: ShelfId },
}

/// 与えられた順に 0 から始まる連番を割り当てる。
/// 1 件でも見つからなければ、何も変更せずに終える。
pub fn reorder_shelves(shelves: &dyn ShelfRepository, ordered: &[ShelfId]) -> ReorderShelvesResult {
    if ordered.is_empty() {
        return ReorderShelvesResult::Success {
            shelves: Vec::new(),
        };
    }

    let mut updated = Vec::with_capacity(ordered.len());
    for (index, id) in ordered.iter().enumerate() {
        let Some(mut shelf) = shelves.get(*id) else {
            return ReorderShelvesResult::ShelfNotFound { shelf_id: *id };
        };
        shelf.set_sort_order(index as i32);
        updated.push(shelf);
    }

    for shelf in &updated {
        shelves.update(shelf.clone());
    }

    ReorderShelvesResult::Success { shelves: updated }
}

/// 既存の並び順の最大値 + 1。要素がなければ 0。
pub(crate) fn next_sort_order(existing: impl Iterator<Item = i32>) -> i32 {
    match existing.max() {
        Some(max) => max + 1,
        None => 0,
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::domain::ItemType;
    use crate::testing::FixedClock;
    use crate::testing::{InMemoryItemRepository, InMemoryShelfRepository};
    use crate::usecases::items::add_item;
    use time::macros::datetime;

    fn repo() -> InMemoryShelfRepository {
        InMemoryShelfRepository::new()
    }

    fn created(result: CreateShelfResult) -> Shelf {
        match result {
            CreateShelfResult::Success { shelf } => shelf,
            other => panic!("expected success, got {other:?}"),
        }
    }

    // --- CreateShelf ---

    #[test]
    fn create_shelf_rejects_blank_name() {
        let shelves = repo();
        let r = create_shelf(&shelves, "   ", None);
        assert!(matches!(r, CreateShelfResult::ValidationError { .. }));
        assert_eq!(shelves.count(), 0);
    }

    #[test]
    fn create_shelf_reports_missing_parent() {
        let shelves = repo();
        let missing = ShelfId::new();
        let r = create_shelf(&shelves, "子", Some(missing));
        assert_eq!(r, CreateShelfResult::ParentNotFound { parent_id: missing });
        assert_eq!(shelves.count(), 0);
    }

    #[test]
    fn create_shelf_numbers_siblings_from_zero() {
        let shelves = repo();
        let a = created(create_shelf(&shelves, "A", None));
        let b = created(create_shelf(&shelves, "B", None));
        let c = created(create_shelf(&shelves, "C", None));
        assert_eq!(a.sort_order(), 0);
        assert_eq!(b.sort_order(), 1);
        assert_eq!(c.sort_order(), 2);
    }

    #[test]
    fn create_shelf_counts_sort_order_per_level() {
        let shelves = repo();
        let parent = created(create_shelf(&shelves, "親", None));
        let _sibling = created(create_shelf(&shelves, "別の根", None));
        let child = created(create_shelf(&shelves, "子", Some(parent.id())));
        // 親が違えば並び順は 0 から始まる
        assert_eq!(child.sort_order(), 0);
        assert_eq!(child.parent_id(), Some(parent.id()));
    }

    // --- RenameShelf ---

    #[test]
    fn rename_shelf_checks_input_and_existence() {
        let shelves = repo();
        let s = created(create_shelf(&shelves, "仕事", None));

        let r = rename_shelf(&shelves, s.id(), " ");
        assert!(matches!(r, RenameShelfResult::ValidationError { .. }));

        let missing = ShelfId::new();
        assert_eq!(
            rename_shelf(&shelves, missing, "新しい名前"),
            RenameShelfResult::ShelfNotFound { shelf_id: missing }
        );

        let r = rename_shelf(&shelves, s.id(), "個人");
        assert!(matches!(r, RenameShelfResult::Success { .. }));
        assert_eq!(shelves.get(s.id()).unwrap().name(), "個人");
    }

    // --- MoveShelf ---

    #[test]
    fn move_shelf_rejects_itself_as_parent() {
        let shelves = repo();
        let s = created(create_shelf(&shelves, "仕事", None));
        let r = move_shelf(&shelves, s.id(), Some(s.id()));
        assert!(matches!(r, MoveShelfResult::InvalidMove { .. }));
    }

    #[test]
    fn move_shelf_rejects_moving_into_own_descendant() {
        let shelves = repo();
        let root = created(create_shelf(&shelves, "root", None));
        let child = created(create_shelf(&shelves, "child", Some(root.id())));
        let grandchild = created(create_shelf(&shelves, "grandchild", Some(child.id())));

        let r = move_shelf(&shelves, root.id(), Some(grandchild.id()));
        assert!(matches!(r, MoveShelfResult::InvalidMove { .. }));
        // 変更されていない
        assert_eq!(shelves.get(root.id()).unwrap().parent_id(), None);
    }

    #[test]
    fn move_shelf_allows_moving_to_root_and_to_another_parent() {
        let shelves = repo();
        let a = created(create_shelf(&shelves, "A", None));
        let b = created(create_shelf(&shelves, "B", None));
        let child = created(create_shelf(&shelves, "child", Some(a.id())));

        assert!(matches!(
            move_shelf(&shelves, child.id(), Some(b.id())),
            MoveShelfResult::Success { .. }
        ));
        assert_eq!(shelves.get(child.id()).unwrap().parent_id(), Some(b.id()));

        assert!(matches!(
            move_shelf(&shelves, child.id(), None),
            MoveShelfResult::Success { .. }
        ));
        assert_eq!(shelves.get(child.id()).unwrap().parent_id(), None);
    }

    #[test]
    fn move_shelf_reports_missing_shelf_and_parent() {
        let shelves = repo();
        let s = created(create_shelf(&shelves, "A", None));
        let missing = ShelfId::new();

        assert_eq!(
            move_shelf(&shelves, missing, None),
            MoveShelfResult::ShelfNotFound { shelf_id: missing }
        );
        assert_eq!(
            move_shelf(&shelves, s.id(), Some(missing)),
            MoveShelfResult::ParentNotFound { parent_id: missing }
        );
    }

    // --- DeleteShelf ---

    #[test]
    fn delete_shelf_removes_descendants_and_their_items() {
        let shelves = repo();
        let items = InMemoryItemRepository::new();
        let clock = FixedClock::new(datetime!(2026-01-05 12:00:00 UTC));

        let root = created(create_shelf(&shelves, "root", None));
        let child = created(create_shelf(&shelves, "child", Some(root.id())));
        let other = created(create_shelf(&shelves, "other", None));

        add_item(
            &items,
            &shelves,
            &clock,
            root.id(),
            ItemType::File,
            "C:\\1.txt",
            "1",
        );
        add_item(
            &items,
            &shelves,
            &clock,
            child.id(),
            ItemType::File,
            "C:\\2.txt",
            "2",
        );
        add_item(
            &items,
            &shelves,
            &clock,
            other.id(),
            ItemType::File,
            "C:\\3.txt",
            "3",
        );

        let r = delete_shelf(&shelves, &items, root.id());
        assert_eq!(
            r,
            DeleteShelfResult::Success {
                deleted_shelves: 2,
                deleted_items: 2
            }
        );
        assert_eq!(shelves.count(), 1);
        assert_eq!(items.count(), 1);
        assert!(shelves.get(other.id()).is_some());
    }

    #[test]
    fn delete_shelf_reports_missing_shelf() {
        let shelves = repo();
        let items = InMemoryItemRepository::new();
        let missing = ShelfId::new();
        assert_eq!(
            delete_shelf(&shelves, &items, missing),
            DeleteShelfResult::ShelfNotFound { shelf_id: missing }
        );
    }

    // --- TogglePinShelf ---

    #[test]
    fn toggle_pin_returns_new_state_and_persists_it() {
        let shelves = repo();
        let s = created(create_shelf(&shelves, "仕事", None));

        let r = toggle_pin_shelf(&shelves, s.id());
        assert!(matches!(
            r,
            TogglePinShelfResult::Success {
                is_pinned: true,
                ..
            }
        ));
        assert!(shelves.get(s.id()).unwrap().is_pinned());

        let r = toggle_pin_shelf(&shelves, s.id());
        assert!(matches!(
            r,
            TogglePinShelfResult::Success {
                is_pinned: false,
                ..
            }
        ));
        assert!(!shelves.get(s.id()).unwrap().is_pinned());
    }

    #[test]
    fn toggle_pin_reports_missing_shelf() {
        let shelves = repo();
        let missing = ShelfId::new();
        assert_eq!(
            toggle_pin_shelf(&shelves, missing),
            TogglePinShelfResult::ShelfNotFound { shelf_id: missing }
        );
    }

    // --- ReorderShelves ---

    #[test]
    fn reorder_assigns_consecutive_numbers_from_zero() {
        let shelves = repo();
        let a = created(create_shelf(&shelves, "A", None));
        let b = created(create_shelf(&shelves, "B", None));
        let c = created(create_shelf(&shelves, "C", None));

        let r = reorder_shelves(&shelves, &[c.id(), a.id(), b.id()]);
        assert!(matches!(r, ReorderShelvesResult::Success { .. }));
        assert_eq!(shelves.get(c.id()).unwrap().sort_order(), 0);
        assert_eq!(shelves.get(a.id()).unwrap().sort_order(), 1);
        assert_eq!(shelves.get(b.id()).unwrap().sort_order(), 2);
    }

    #[test]
    fn reorder_accepts_empty_input() {
        let shelves = repo();
        assert_eq!(
            reorder_shelves(&shelves, &[]),
            ReorderShelvesResult::Success {
                shelves: Vec::new()
            }
        );
    }

    #[test]
    fn reorder_with_unknown_id_changes_nothing() {
        let shelves = repo();
        let a = created(create_shelf(&shelves, "A", None));
        let b = created(create_shelf(&shelves, "B", None));
        let missing = ShelfId::new();

        let r = reorder_shelves(&shelves, &[b.id(), missing, a.id()]);
        assert_eq!(r, ReorderShelvesResult::ShelfNotFound { shelf_id: missing });
        // 途中まで書き換わっていないこと
        assert_eq!(shelves.get(a.id()).unwrap().sort_order(), 0);
        assert_eq!(shelves.get(b.id()).unwrap().sort_order(), 1);
    }
}
