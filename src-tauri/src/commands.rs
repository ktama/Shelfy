//! IPC の境界（doc/ARCHITECTURE.md 第 6 節）。
//!
//! コマンドは「利用者の 1 操作」に対応させる。
//! 更新系は、成功したら更新後の状態を返し、画面が問い合わせ直さずに済むようにする。

use serde::Serialize;
use tauri::{Manager, State};

use shelfy::domain::{ItemId, ShelfId};
use shelfy::ports::{ItemRepository, ShelfRepository};
use shelfy::usecases::items::{get_missing_items, get_recent_items, ItemWithShelf};
use shelfy::usecases::launch::{
    launch_item, open_parent_folder, LaunchItemResult, OpenParentFolderResult,
};
use shelfy::usecases::search::search_items;
use shelfy::usecases::settings::read_settings as read_stored_settings;

use crate::state::{AppState, WindowEffects};

// ---------------------------------------------------------------- 画面へ渡す形

/// 一覧に出す 1 件
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ItemView {
    pub id: String,
    pub shelf_id: String,
    pub shelf_name: String,
    pub kind: &'static str,
    pub target: String,
    pub display_name: String,
    pub memo: Option<String>,
    pub sort_order: i32,
    pub last_accessed_at: Option<String>,
}

/// ツリーに出す 1 件。階層は `parentId` から画面側で組み立てる。
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ShelfView {
    pub id: String,
    pub name: String,
    pub parent_id: Option<String>,
    pub sort_order: i32,
    pub is_pinned: bool,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct SettingsView {
    pub global_hotkey: String,
    pub window_width: f64,
    pub window_height: f64,
    pub start_minimized: bool,
    pub recent_items_count: usize,
}

/// 起動直後に画面へ伝える状態
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct StartupInfo {
    /// 保存ファイルの読み込みで何が起きたか
    pub storage: String,
    /// 利用者に伝えるべきことがあれば入る
    pub notice: Option<String>,
    pub read_only: bool,
    /// Mica を適用できたか。画面はこれを見て背景を透かす（第 5.3 節）。
    pub mica: bool,
    pub settings: SettingsView,
}

pub(crate) fn to_item_view(entry: ItemWithShelf) -> ItemView {
    let item = entry.item;
    ItemView {
        id: item.id().to_string(),
        shelf_id: item.shelf_id().to_string(),
        shelf_name: entry.shelf_name,
        kind: match item.item_type() {
            shelfy::domain::ItemType::File => "file",
            shelfy::domain::ItemType::Folder => "folder",
            shelfy::domain::ItemType::Url => "url",
        },
        target: item.target().to_string(),
        display_name: item.display_name().to_string(),
        memo: item.memo().map(|m| m.to_string()),
        sort_order: item.sort_order(),
        last_accessed_at: item
            .last_accessed_at()
            .map(shelfy::usecases::transfer::format_time),
    }
}

pub(crate) fn parse_shelf(id: &str) -> Result<ShelfId, String> {
    ShelfId::parse(id).ok_or_else(|| format!("shelf id を読めません: {id}"))
}

pub(crate) fn parse_item(id: &str) -> Result<ItemId, String> {
    ItemId::parse(id).ok_or_else(|| format!("item id を読めません: {id}"))
}

// ---------------------------------------------------------------- 読み取り

/// Shelf の一覧。表示順（ピン留め、並び順、名前）に並べて返す。
#[tauri::command]
pub fn load_shelves(state: State<'_, AppState>) -> Vec<ShelfView> {
    let mut shelves = ShelfRepository::all(state.store.as_ref());
    shelves.sort_by(|a, b| {
        b.is_pinned()
            .cmp(&a.is_pinned())
            .then_with(|| a.sort_order().cmp(&b.sort_order()))
            .then_with(|| a.name().cmp(b.name()))
    });
    shelves
        .iter()
        .map(crate::mutations::to_shelf_view)
        .collect()
}

/// 指定した Shelf の Item。並び順、次に表示名で並ぶ。
#[tauri::command]
pub fn load_items(state: State<'_, AppState>, shelf_id: String) -> Result<Vec<ItemView>, String> {
    let shelf_id = parse_shelf(&shelf_id)?;
    let name = ShelfRepository::get(state.store.as_ref(), shelf_id)
        .map(|s| s.name().to_string())
        .unwrap_or_else(|| shelfy::usecases::items::UNKNOWN_SHELF.to_string());

    Ok(ItemRepository::by_shelf(state.store.as_ref(), shelf_id)
        .into_iter()
        .map(|item| {
            to_item_view(ItemWithShelf {
                item,
                shelf_name: name.clone(),
            })
        })
        .collect())
}

#[tauri::command]
pub fn search(state: State<'_, AppState>, query: String) -> Vec<ItemView> {
    search_items(state.store.as_ref(), state.store.as_ref(), &query)
        .into_iter()
        .map(to_item_view)
        .collect()
}

#[tauri::command]
pub fn recent_items(state: State<'_, AppState>) -> Vec<ItemView> {
    let count = read_stored_settings(state.store.as_ref()).recent_items_count;
    get_recent_items(state.store.as_ref(), state.store.as_ref(), Some(count))
        .into_iter()
        .map(to_item_view)
        .collect()
}

#[tauri::command]
pub fn missing_items(state: State<'_, AppState>) -> Vec<ItemView> {
    get_missing_items(state.store.as_ref(), state.store.as_ref(), &state.existence)
        .into_iter()
        .map(to_item_view)
        .collect()
}

#[tauri::command]
pub fn startup_info(state: State<'_, AppState>, effects: State<'_, WindowEffects>) -> StartupInfo {
    use shelfy::adapters::store::LoadOutcome;

    let (storage, notice) = match &state.load_outcome {
        LoadOutcome::Fresh => ("fresh", None),
        LoadOutcome::Loaded => ("loaded", None),
        LoadOutcome::RecoveredFromBackup => (
            "recovered",
            Some("保存ファイルを読めなかったため、控えから回復しました。".to_string()),
        ),
        LoadOutcome::StartedEmpty { quarantined } => (
            "empty",
            Some(match quarantined {
                Some(path) => format!(
                    "保存ファイルを読めませんでした。壊れたファイルは {} へ退避しています。",
                    path.display()
                ),
                None => "保存ファイルを読めませんでした。空で開始します。".to_string(),
            }),
        ),
        LoadOutcome::ReadOnly { schema_version } => (
            "readOnly",
            Some(format!(
                "この Shelfy が知らない版数（{schema_version}）のデータです。読み取り専用で開いています。"
            )),
        ),
    };

    StartupInfo {
        storage: storage.to_string(),
        notice,
        read_only: state.store.is_read_only(),
        mica: effects.mica,
        settings: read_settings(&state),
    }
}

fn read_settings(state: &State<'_, AppState>) -> SettingsView {
    // 既定値の適用規則は usecases::settings に 1 か所だけ置いてある
    let settings = read_stored_settings(state.store.as_ref());
    SettingsView {
        global_hotkey: settings.global_hotkey,
        window_width: settings.window_width,
        window_height: settings.window_height,
        start_minimized: settings.start_minimized,
        recent_items_count: settings.recent_items_count,
    }
}

// ---------------------------------------------------------------- 起動

#[derive(Debug, Serialize)]
#[serde(tag = "kind", rename_all = "camelCase")]
pub enum LaunchOutcome {
    /// 起動できた。`hide` が true ならウィンドウを隠す。
    Launched {
        hide: bool,
    },
    Failed {
        message: String,
    },
}

#[tauri::command]
pub fn launch(
    app: tauri::AppHandle,
    state: State<'_, AppState>,
    item_id: String,
) -> Result<LaunchOutcome, String> {
    let item_id = parse_item(&item_id)?;

    let result = launch_item(
        state.store.as_ref(),
        &state.launcher,
        &state.hotkey_hold,
        &state.clock,
        item_id,
    );

    let outcome = match result {
        LaunchItemResult::Success { post_action } => {
            use shelfy::usecases::launch::PostLaunchAction;
            let hide = post_action == PostLaunchAction::HideWindow;
            if hide {
                hide_main_window(&app);
            }
            // 最終アクセス日時が変わっているので書き出しておく
            state.flush();
            LaunchOutcome::Launched { hide }
        }
        LaunchItemResult::ItemNotFound { .. } => LaunchOutcome::Failed {
            message: "この項目は見つかりませんでした。".into(),
        },
        LaunchItemResult::LaunchFailed { target } => LaunchOutcome::Failed {
            message: format!("開けませんでした: {target}"),
        },
    };

    Ok(outcome)
}

#[tauri::command]
pub fn open_parent(state: State<'_, AppState>, item_id: String) -> Result<Option<String>, String> {
    let item_id = parse_item(&item_id)?;
    let message = match open_parent_folder(state.store.as_ref(), &state.launcher, item_id) {
        OpenParentFolderResult::Success => None,
        OpenParentFolderResult::ItemNotFound { .. } => {
            Some("この項目は見つかりませんでした。".to_string())
        }
        OpenParentFolderResult::NotSupported { .. } => {
            Some("URL には親フォルダがありません。".to_string())
        }
        OpenParentFolderResult::OpenFailed { target } => {
            Some(format!("親フォルダを開けませんでした: {target}"))
        }
    };
    Ok(message)
}

// ---------------------------------------------------------------- ウィンドウ

#[tauri::command]
pub fn hide_window(app: tauri::AppHandle, state: State<'_, AppState>) {
    crate::settings::remember_window_size(&app);
    // 隠すときは待たずに書き出す
    state.flush();
    hide_main_window(&app);
}

pub fn hide_main_window(app: &tauri::AppHandle) {
    if let Some(window) = app.get_webview_window("main") {
        let _ = window.hide();
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use shelfy::domain::{Item, ItemId, ItemType, Shelf, ShelfId};
    use shelfy::usecases::settings::Settings;
    use time::macros::datetime;

    fn item_of(kind: ItemType, memo: Option<&str>, accessed: bool) -> ItemWithShelf {
        let item = Item::new(
            ItemId::new(),
            ShelfId::new(),
            kind,
            r"C:\work\a.txt",
            "a.txt",
            datetime!(2026-01-05 12:00:00 UTC),
            memo.map(|m| m.to_string()),
            3,
            accessed.then_some(datetime!(2026-02-19 09:30:00 UTC)),
        )
        .unwrap();
        ItemWithShelf {
            item,
            shelf_name: "仕事".to_string(),
        }
    }

    /// 画面との契約は鍵の「集合」であり、並び順ではない。
    /// 並べてから比べる。
    fn keys(value: &serde_json::Value) -> Vec<String> {
        let mut names: Vec<String> = value
            .as_object()
            .expect("オブジェクトであること")
            .keys()
            .cloned()
            .collect();
        names.sort();
        names
    }

    /// 画面の `ItemKind`（src/lib/ipc.ts）と綴りが一致していなければ、
    /// アイコンも絞り込みも静かに外れる。
    #[test]
    fn the_kind_matches_the_names_the_screen_knows() {
        assert_eq!(
            to_item_view(item_of(ItemType::File, None, false)).kind,
            "file"
        );
        assert_eq!(
            to_item_view(item_of(ItemType::Folder, None, false)).kind,
            "folder"
        );
        assert_eq!(
            to_item_view(item_of(ItemType::Url, None, false)).kind,
            "url"
        );
    }

    /// 画面が読む鍵の集合（src/lib/ipc.ts の `ItemView`）と一致させる。
    /// Rust 側で名前を変えても、型検査では気づけない。
    #[test]
    fn the_item_view_carries_exactly_the_keys_the_screen_reads() {
        let view = to_item_view(item_of(ItemType::File, Some("月次"), true));
        let json = serde_json::to_value(&view).unwrap();

        assert_eq!(
            keys(&json),
            vec![
                "displayName",
                "id",
                "kind",
                "lastAccessedAt",
                "memo",
                "shelfId",
                "shelfName",
                "sortOrder",
                "target",
            ]
        );
        assert_eq!(json["shelfName"], "仕事");
        assert_eq!(json["memo"], "月次");
        assert_eq!(json["sortOrder"], 3);
    }

    #[test]
    fn an_item_that_was_never_launched_has_no_timestamp() {
        let view = to_item_view(item_of(ItemType::File, None, false));
        let json = serde_json::to_value(&view).unwrap();

        assert_eq!(json["lastAccessedAt"], serde_json::Value::Null);
        assert_eq!(json["memo"], serde_json::Value::Null);
    }

    /// 日時は交換形式と同じ書式で渡す。画面はこれをそのまま解釈する。
    #[test]
    fn the_timestamp_is_written_in_the_documented_format() {
        let view = to_item_view(item_of(ItemType::File, None, true));

        let text = view.last_accessed_at.expect("値があること");
        assert!(text.starts_with("2026-02-19T09:30:00"), "{text}");
        assert!(
            text.ends_with('Z'),
            "UTC であることが分かる形にする: {text}"
        );
    }

    #[test]
    fn the_shelf_view_carries_exactly_the_keys_the_screen_reads() {
        let shelf = Shelf::new(ShelfId::new(), "仕事", None, 2, true).unwrap();
        let view = crate::mutations::to_shelf_view(&shelf);
        let json = serde_json::to_value(&view).unwrap();

        assert_eq!(
            keys(&json),
            vec!["id", "isPinned", "name", "parentId", "sortOrder"]
        );
        assert_eq!(json["parentId"], serde_json::Value::Null);
        assert_eq!(json["isPinned"], true);
    }

    #[test]
    fn the_settings_view_carries_exactly_the_keys_the_screen_reads() {
        let s = Settings::default();
        let view = SettingsView {
            global_hotkey: s.global_hotkey,
            window_width: s.window_width,
            window_height: s.window_height,
            start_minimized: s.start_minimized,
            recent_items_count: s.recent_items_count,
        };
        let json = serde_json::to_value(&view).unwrap();

        assert_eq!(
            keys(&json),
            vec![
                "globalHotkey",
                "recentItemsCount",
                "startMinimized",
                "windowHeight",
                "windowWidth",
            ]
        );
    }

    // ---- 識別子の読み取り ----

    #[test]
    fn a_valid_identifier_round_trips() {
        let id = ShelfId::new();
        assert_eq!(parse_shelf(&id.to_string()).unwrap(), id);

        let id = ItemId::new();
        assert_eq!(parse_item(&id.to_string()).unwrap(), id);
    }

    /// 壊れた識別子は、どの値が読めなかったかが分かる形で失敗させる。
    #[test]
    fn a_broken_identifier_is_reported_with_the_offending_value() {
        let message = parse_shelf("not-a-uuid").unwrap_err();
        assert!(message.contains("not-a-uuid"), "{message}");

        let message = parse_item("").unwrap_err();
        assert!(message.contains("item id"), "{message}");
    }
}
