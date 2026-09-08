//! 起動と親フォルダを開く操作（doc/rebuild/02-SPECIFICATION.md 第 4.3 節）

use serde::Serialize;

use crate::domain::{ItemId, ItemType};
use crate::ports::{Clock, HotkeyHoldState, ItemLauncher, ItemRepository};

/// 起動したあとにウィンドウをどうするか
#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum PostLaunchAction {
    /// 通常はウィンドウを隠す
    HideWindow,
    /// ホットキーの修飾キーが押されたままなら残す
    KeepWindow,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum LaunchItemResult {
    Success { post_action: PostLaunchAction },
    ItemNotFound { item_id: ItemId },
    LaunchFailed { target: String },
}

/// 参照先を開き、成功したら最終アクセス日時を記録する。
/// 起動後にウィンドウを残すかどうかは、ここで決める（UI 側で決めない）。
pub fn launch_item(
    items: &dyn ItemRepository,
    launcher: &dyn ItemLauncher,
    hotkey: &dyn HotkeyHoldState,
    clock: &dyn Clock,
    item_id: ItemId,
) -> LaunchItemResult {
    let Some(mut item) = items.get(item_id) else {
        return LaunchItemResult::ItemNotFound { item_id };
    };

    if !launcher.launch(&item) {
        return LaunchItemResult::LaunchFailed {
            target: item.target().to_string(),
        };
    }

    item.mark_accessed(clock.now_utc());
    items.update(item);

    let post_action = if hotkey.is_held() {
        PostLaunchAction::KeepWindow
    } else {
        PostLaunchAction::HideWindow
    };

    LaunchItemResult::Success { post_action }
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum OpenParentFolderResult {
    Success,
    ItemNotFound { item_id: ItemId },
    NotSupported { message: String },
    OpenFailed { target: String },
}

/// 参照先を選択した状態で親フォルダを開く。URL は対象外。
pub fn open_parent_folder(
    items: &dyn ItemRepository,
    launcher: &dyn ItemLauncher,
    item_id: ItemId,
) -> OpenParentFolderResult {
    let Some(item) = items.get(item_id) else {
        return OpenParentFolderResult::ItemNotFound { item_id };
    };

    if item.item_type() == ItemType::Url {
        return OpenParentFolderResult::NotSupported {
            message: "cannot open parent folder for url items".into(),
        };
    }

    if !launcher.open_parent_folder(&item) {
        return OpenParentFolderResult::OpenFailed {
            target: item.target().to_string(),
        };
    }

    OpenParentFolderResult::Success
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::domain::ShelfId;
    use crate::testing::{
        FakeHotkeyHoldState, FakeLauncher, FixedClock, InMemoryItemRepository,
        InMemoryShelfRepository,
    };
    use crate::usecases::items::{add_item, AddItemResult};
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
            o => panic!("{o:?}"),
        };
        Fixture {
            items: InMemoryItemRepository::new(),
            shelves,
            clock: FixedClock::new(datetime!(2026-01-05 12:00:00 UTC)),
            shelf,
        }
    }

    fn add(f: &Fixture, t: ItemType, target: &str) -> ItemId {
        match add_item(&f.items, &f.shelves, &f.clock, f.shelf, t, target, "name") {
            AddItemResult::Success { item } => item.id(),
            o => panic!("{o:?}"),
        }
    }

    // --- LaunchItem ---

    #[test]
    fn launch_hides_the_window_when_the_hotkey_is_released() {
        let f = fixture();
        let id = add(&f, ItemType::File, "C:\\1.txt");
        let launcher = FakeLauncher::new();
        let hotkey = FakeHotkeyHoldState::released();

        let r = launch_item(&f.items, &launcher, &hotkey, &f.clock, id);
        assert_eq!(
            r,
            LaunchItemResult::Success {
                post_action: PostLaunchAction::HideWindow
            }
        );
        assert_eq!(launcher.launched(), vec![id]);
    }

    #[test]
    fn launch_keeps_the_window_while_the_hotkey_is_held() {
        let f = fixture();
        let id = add(&f, ItemType::File, "C:\\1.txt");
        let launcher = FakeLauncher::new();
        let hotkey = FakeHotkeyHoldState::held();

        let r = launch_item(&f.items, &launcher, &hotkey, &f.clock, id);
        assert_eq!(
            r,
            LaunchItemResult::Success {
                post_action: PostLaunchAction::KeepWindow
            }
        );
    }

    #[test]
    fn launch_records_the_access_time_on_success() {
        let f = fixture();
        let id = add(&f, ItemType::File, "C:\\1.txt");
        let launcher = FakeLauncher::new();
        let hotkey = FakeHotkeyHoldState::released();
        f.clock.set(datetime!(2026-02-20 09:30:00 UTC));

        launch_item(&f.items, &launcher, &hotkey, &f.clock, id);

        assert_eq!(
            f.items.get(id).unwrap().last_accessed_at(),
            Some(datetime!(2026-02-20 09:30:00 UTC))
        );
    }

    #[test]
    fn launch_failure_leaves_the_access_time_untouched() {
        let f = fixture();
        let id = add(&f, ItemType::File, "C:\\1.txt");
        let launcher = FakeLauncher::failing();
        let hotkey = FakeHotkeyHoldState::released();

        let r = launch_item(&f.items, &launcher, &hotkey, &f.clock, id);
        assert_eq!(
            r,
            LaunchItemResult::LaunchFailed {
                target: "C:\\1.txt".into()
            }
        );
        assert_eq!(f.items.get(id).unwrap().last_accessed_at(), None);
    }

    #[test]
    fn launch_reports_missing_item_without_calling_the_launcher() {
        let f = fixture();
        let launcher = FakeLauncher::new();
        let hotkey = FakeHotkeyHoldState::released();
        let missing = ItemId::new();

        assert_eq!(
            launch_item(&f.items, &launcher, &hotkey, &f.clock, missing),
            LaunchItemResult::ItemNotFound { item_id: missing }
        );
        assert!(launcher.launched().is_empty());
    }

    // --- OpenParentFolder ---

    #[test]
    fn open_parent_folder_works_for_files_and_folders() {
        let f = fixture();
        let file = add(&f, ItemType::File, "C:\\1.txt");
        let folder = add(&f, ItemType::Folder, "C:\\work");
        let launcher = FakeLauncher::new();

        assert_eq!(
            open_parent_folder(&f.items, &launcher, file),
            OpenParentFolderResult::Success
        );
        assert_eq!(
            open_parent_folder(&f.items, &launcher, folder),
            OpenParentFolderResult::Success
        );
        assert_eq!(launcher.opened(), vec![file, folder]);
    }

    #[test]
    fn open_parent_folder_is_not_supported_for_urls() {
        let f = fixture();
        let url = add(&f, ItemType::Url, "https://example.com");
        let launcher = FakeLauncher::new();

        let r = open_parent_folder(&f.items, &launcher, url);
        assert!(matches!(r, OpenParentFolderResult::NotSupported { .. }));
        assert!(launcher.opened().is_empty());
    }

    #[test]
    fn open_parent_folder_reports_failure_and_missing_item() {
        let f = fixture();
        let file = add(&f, ItemType::File, "C:\\1.txt");
        let failing = FakeLauncher::failing();
        assert_eq!(
            open_parent_folder(&f.items, &failing, file),
            OpenParentFolderResult::OpenFailed {
                target: "C:\\1.txt".into()
            }
        );

        let launcher = FakeLauncher::new();
        let missing = ItemId::new();
        assert_eq!(
            open_parent_folder(&f.items, &launcher, missing),
            OpenParentFolderResult::ItemNotFound { item_id: missing }
        );
    }
}
