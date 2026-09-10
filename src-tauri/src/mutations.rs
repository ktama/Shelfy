//! 更新系のコマンド（doc/rebuild/03-ARCHITECTURE.md 第 6.2 節）。
//! 読み取りと起動は `commands` にある。

use serde::Serialize;
use tauri::State;

use shelfy::domain::{Item, ItemType, Shelf};
use shelfy::ports::{AppLogger, ShelfRepository};
use shelfy::usecases::items::{ItemWithShelf, UNKNOWN_SHELF};

use crate::commands::{parse_item, parse_shelf, to_item_view, ItemView, ShelfView};
use crate::state::AppState;

// ---------------------------------------------------------------- 返し方

/// `message` が入っていれば、利用者に見せる失敗である。
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ShelfResult {
    pub shelf: Option<ShelfView>,
    pub message: Option<String>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ItemResult {
    pub item: Option<ItemView>,
    pub message: Option<String>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct SimpleResult {
    pub ok: bool,
    pub message: Option<String>,
}

impl SimpleResult {
    fn ok() -> Self {
        Self {
            ok: true,
            message: None,
        }
    }

    fn note(message: impl Into<String>) -> Self {
        Self {
            ok: true,
            message: Some(message.into()),
        }
    }

    fn failed(message: impl Into<String>) -> Self {
        Self {
            ok: false,
            message: Some(message.into()),
        }
    }
}

fn shelf_failed(message: &str) -> ShelfResult {
    ShelfResult {
        shelf: None,
        message: Some(message.to_string()),
    }
}

fn item_failed(message: &str) -> ItemResult {
    ItemResult {
        item: None,
        message: Some(message.to_string()),
    }
}

fn to_shelf_view(shelf: &Shelf) -> ShelfView {
    ShelfView {
        id: shelf.id().to_string(),
        name: shelf.name().to_string(),
        parent_id: shelf.parent_id().map(|p| p.to_string()),
        sort_order: shelf.sort_order(),
        is_pinned: shelf.is_pinned(),
    }
}

fn item_view_of(state: &State<'_, AppState>, item: Item) -> ItemView {
    let shelf_name = ShelfRepository::get(state.store.as_ref(), item.shelf_id())
        .map(|s| s.name().to_string())
        .unwrap_or_else(|| UNKNOWN_SHELF.to_string());
    to_item_view(ItemWithShelf { item, shelf_name })
}

// ---------------------------------------------------------------- Shelf

#[tauri::command]
pub fn create_shelf(
    state: State<'_, AppState>,
    name: String,
    parent_id: Option<String>,
) -> Result<ShelfResult, String> {
    use shelfy::usecases::shelves::{create_shelf as run, CreateShelfResult};

    let parent = match parent_id {
        Some(id) => Some(parse_shelf(&id)?),
        None => None,
    };

    Ok(match run(state.store.as_ref(), &name, parent) {
        CreateShelfResult::Success { shelf } => ShelfResult {
            shelf: Some(to_shelf_view(&shelf)),
            message: None,
        },
        CreateShelfResult::ValidationError { .. } => shelf_failed("棚の名前を入力してください。"),
        CreateShelfResult::ParentNotFound { .. } => shelf_failed("親の棚が見つかりませんでした。"),
    })
}

#[tauri::command]
pub fn rename_shelf(
    state: State<'_, AppState>,
    shelf_id: String,
    name: String,
) -> Result<ShelfResult, String> {
    use shelfy::usecases::shelves::{rename_shelf as run, RenameShelfResult};
    let id = parse_shelf(&shelf_id)?;

    Ok(match run(state.store.as_ref(), id, &name) {
        RenameShelfResult::Success { shelf } => ShelfResult {
            shelf: Some(to_shelf_view(&shelf)),
            message: None,
        },
        RenameShelfResult::ValidationError { .. } => shelf_failed("棚の名前を入力してください。"),
        RenameShelfResult::ShelfNotFound { .. } => shelf_failed("棚が見つかりませんでした。"),
    })
}

#[tauri::command]
pub fn move_shelf(
    state: State<'_, AppState>,
    shelf_id: String,
    parent_id: Option<String>,
) -> Result<SimpleResult, String> {
    use shelfy::usecases::shelves::{move_shelf as run, MoveShelfResult};
    let id = parse_shelf(&shelf_id)?;
    let parent = match parent_id {
        Some(p) => Some(parse_shelf(&p)?),
        None => None,
    };

    Ok(match run(state.store.as_ref(), id, parent) {
        MoveShelfResult::Success { .. } => SimpleResult::ok(),
        MoveShelfResult::ShelfNotFound { .. } => SimpleResult::failed("棚が見つかりませんでした。"),
        MoveShelfResult::ParentNotFound { .. } => {
            SimpleResult::failed("移動先の棚が見つかりませんでした。")
        }
        MoveShelfResult::InvalidMove { .. } => {
            SimpleResult::failed("自分自身や、自分の中にある棚へは移動できません。")
        }
    })
}

#[tauri::command]
pub fn delete_shelf(state: State<'_, AppState>, shelf_id: String) -> Result<SimpleResult, String> {
    use shelfy::usecases::shelves::{delete_shelf as run, DeleteShelfResult};
    let id = parse_shelf(&shelf_id)?;

    Ok(match run(state.store.as_ref(), state.store.as_ref(), id) {
        DeleteShelfResult::Success {
            deleted_shelves,
            deleted_items,
        } => SimpleResult::note(format!(
            "{deleted_shelves} 個の棚と {deleted_items} 件の項目を削除しました"
        )),
        DeleteShelfResult::ShelfNotFound { .. } => {
            SimpleResult::failed("棚が見つかりませんでした。")
        }
    })
}

#[tauri::command]
pub fn toggle_pin_shelf(
    state: State<'_, AppState>,
    shelf_id: String,
) -> Result<ShelfResult, String> {
    use shelfy::usecases::shelves::{toggle_pin_shelf as run, TogglePinShelfResult};
    let id = parse_shelf(&shelf_id)?;

    Ok(match run(state.store.as_ref(), id) {
        TogglePinShelfResult::Success { shelf, .. } => ShelfResult {
            shelf: Some(to_shelf_view(&shelf)),
            message: None,
        },
        TogglePinShelfResult::ShelfNotFound { .. } => shelf_failed("棚が見つかりませんでした。"),
    })
}

#[tauri::command]
pub fn reorder_shelves(
    state: State<'_, AppState>,
    ordered_ids: Vec<String>,
) -> Result<SimpleResult, String> {
    use shelfy::usecases::shelves::{reorder_shelves as run, ReorderShelvesResult};
    let ids = ordered_ids
        .iter()
        .map(|id| parse_shelf(id))
        .collect::<Result<Vec<_>, _>>()?;

    Ok(match run(state.store.as_ref(), &ids) {
        ReorderShelvesResult::Success { .. } => SimpleResult::ok(),
        ReorderShelvesResult::ShelfNotFound { .. } => {
            SimpleResult::failed("並び替えの途中で棚が見つかりませんでした。")
        }
    })
}

// ---------------------------------------------------------------- Item

/// 参照先から種別と既定の表示名を決める（02-SPECIFICATION.md 第 9.3 節）
fn classify(target: &str) -> (ItemType, String) {
    let lower = target.to_ascii_lowercase();
    if lower.starts_with("http://") || lower.starts_with("https://") {
        let host = target
            .split("//")
            .nth(1)
            .and_then(|rest| rest.split('/').next())
            .unwrap_or(target);
        return (ItemType::Url, host.to_string());
    }

    let path = std::path::Path::new(target);
    let name = path
        .file_name()
        .map(|n| n.to_string_lossy().to_string())
        .unwrap_or_else(|| target.to_string());

    if path.is_dir() {
        (ItemType::Folder, name)
    } else {
        (ItemType::File, name)
    }
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct AddItemsResult {
    pub added: Vec<ItemView>,
    /// 追加できなかったものと、その理由
    pub skipped: Vec<String>,
}

#[tauri::command]
pub fn add_items(
    state: State<'_, AppState>,
    shelf_id: String,
    targets: Vec<String>,
) -> Result<AddItemsResult, String> {
    use shelfy::usecases::items::{add_item, AddItemResult};
    let shelf = parse_shelf(&shelf_id)?;

    let mut added = Vec::new();
    let mut skipped = Vec::new();

    for target in targets {
        let (kind, name) = classify(&target);
        match add_item(
            state.store.as_ref(),
            state.store.as_ref(),
            &state.clock,
            shelf,
            kind,
            &target,
            &name,
        ) {
            AddItemResult::Success { item } => added.push(item_view_of(&state, item)),
            AddItemResult::DuplicateItem { .. } => {
                skipped.push(format!("{name}: すでに同じ参照があります"))
            }
            AddItemResult::ShelfNotFound { .. } => {
                skipped.push(format!("{name}: 棚が見つかりません"))
            }
            AddItemResult::ValidationError { .. } => {
                skipped.push(format!("{name}: 参照先か表示名が空です"))
            }
        }
    }

    Ok(AddItemsResult { added, skipped })
}

#[tauri::command]
pub fn add_url(
    state: State<'_, AppState>,
    shelf_id: String,
    url: String,
    display_name: String,
) -> Result<ItemResult, String> {
    use shelfy::usecases::items::{add_item, AddItemResult};
    let shelf = parse_shelf(&shelf_id)?;

    // http と https の絶対 URL 以外は受け付けない（第 9.4 節）
    let lower = url.to_ascii_lowercase();
    if !(lower.starts_with("http://") || lower.starts_with("https://")) {
        return Ok(item_failed(
            "http か https で始まる URL を入力してください。",
        ));
    }

    let name = if display_name.trim().is_empty() {
        classify(&url).1
    } else {
        display_name
    };

    Ok(
        match add_item(
            state.store.as_ref(),
            state.store.as_ref(),
            &state.clock,
            shelf,
            ItemType::Url,
            &url,
            &name,
        ) {
            AddItemResult::Success { item } => ItemResult {
                item: Some(item_view_of(&state, item)),
                message: None,
            },
            AddItemResult::DuplicateItem { .. } => {
                item_failed("この棚には、すでに同じ URL があります。")
            }
            AddItemResult::ShelfNotFound { .. } => item_failed("棚が見つかりませんでした。"),
            AddItemResult::ValidationError { .. } => {
                item_failed("URL と表示名を入力してください。")
            }
        },
    )
}

#[tauri::command]
pub fn remove_item(state: State<'_, AppState>, item_id: String) -> Result<SimpleResult, String> {
    use shelfy::usecases::items::{remove_item as run, RemoveItemResult};
    let id = parse_item(&item_id)?;

    Ok(match run(state.store.as_ref(), id) {
        RemoveItemResult::Success { .. } => SimpleResult::ok(),
        RemoveItemResult::ItemNotFound { .. } => {
            SimpleResult::failed("項目が見つかりませんでした。")
        }
    })
}

#[tauri::command]
pub fn rename_item(
    state: State<'_, AppState>,
    item_id: String,
    name: String,
) -> Result<ItemResult, String> {
    use shelfy::usecases::items::{rename_item as run, RenameItemResult};
    let id = parse_item(&item_id)?;

    Ok(match run(state.store.as_ref(), id, &name) {
        RenameItemResult::Success { item } => ItemResult {
            item: Some(item_view_of(&state, item)),
            message: None,
        },
        RenameItemResult::ValidationError { .. } => item_failed("表示名を入力してください。"),
        RenameItemResult::ItemNotFound { .. } => item_failed("項目が見つかりませんでした。"),
    })
}

#[tauri::command]
pub fn update_item_memo(
    state: State<'_, AppState>,
    item_id: String,
    memo: Option<String>,
) -> Result<ItemResult, String> {
    use shelfy::usecases::items::{update_item_memo as run, UpdateItemMemoResult};
    let id = parse_item(&item_id)?;
    // 空文字は「メモなし」として扱う
    let memo = memo.filter(|m| !m.trim().is_empty());

    Ok(match run(state.store.as_ref(), id, memo) {
        UpdateItemMemoResult::Success { item } => ItemResult {
            item: Some(item_view_of(&state, item)),
            message: None,
        },
        UpdateItemMemoResult::ItemNotFound { .. } => item_failed("項目が見つかりませんでした。"),
    })
}

#[tauri::command]
pub fn move_item_to_shelf(
    state: State<'_, AppState>,
    item_id: String,
    shelf_id: String,
) -> Result<SimpleResult, String> {
    use shelfy::usecases::items::{move_item_to_shelf as run, MoveItemToShelfResult};
    let item = parse_item(&item_id)?;
    let shelf = parse_shelf(&shelf_id)?;

    Ok(
        match run(state.store.as_ref(), state.store.as_ref(), item, shelf) {
            MoveItemToShelfResult::Success { .. } => SimpleResult::ok(),
            MoveItemToShelfResult::ItemNotFound { .. } => {
                SimpleResult::failed("項目が見つかりませんでした。")
            }
            MoveItemToShelfResult::ShelfNotFound { .. } => {
                SimpleResult::failed("移動先の棚が見つかりませんでした。")
            }
            MoveItemToShelfResult::DuplicateItem { .. } => {
                SimpleResult::failed("移動先には、すでに同じ参照があります。")
            }
        },
    )
}

#[tauri::command]
pub fn reorder_items(
    state: State<'_, AppState>,
    ordered_ids: Vec<String>,
) -> Result<SimpleResult, String> {
    use shelfy::usecases::items::{reorder_items as run, ReorderItemsResult};
    let ids = ordered_ids
        .iter()
        .map(|id| parse_item(id))
        .collect::<Result<Vec<_>, _>>()?;

    Ok(match run(state.store.as_ref(), &ids) {
        ReorderItemsResult::Success { .. } => SimpleResult::ok(),
        ReorderItemsResult::ItemNotFound { .. } => {
            SimpleResult::failed("並び替えの途中で項目が見つかりませんでした。")
        }
    })
}

// ---------------------------------------------------------------- データ入出力

#[tauri::command]
pub fn export_data(state: State<'_, AppState>, path: String) -> Result<SimpleResult, String> {
    use shelfy::usecases::transfer::export_data as run;

    let data = run(state.store.as_ref(), state.store.as_ref(), &state.clock);
    let text = serde_json::to_string_pretty(&data)
        .map_err(|e| format!("書き出す形に変換できませんでした: {e}"))?;

    Ok(match std::fs::write(&path, text) {
        Ok(()) => SimpleResult::note(format!(
            "{} 個の棚と {} 件の項目を書き出しました",
            data.shelves.len(),
            data.items.len()
        )),
        Err(e) => {
            state.logger.error(&format!("書き出しに失敗しました: {e}"));
            SimpleResult::failed(format!("書き出せませんでした: {e}"))
        }
    })
}

#[tauri::command]
pub fn import_data(
    state: State<'_, AppState>,
    path: String,
    replace_all: bool,
) -> Result<SimpleResult, String> {
    use shelfy::usecases::transfer::{import_data as run, ExportData, ImportMode};

    let text = match std::fs::read_to_string(&path) {
        Ok(text) => text,
        Err(e) => return Ok(SimpleResult::failed(format!("読み込めませんでした: {e}"))),
    };

    let data: ExportData = match serde_json::from_str(&text) {
        Ok(data) => data,
        Err(e) => {
            return Ok(SimpleResult::failed(format!(
                "Shelfy の書き出しファイルとして読めませんでした: {e}"
            )))
        }
    };

    let mode = if replace_all {
        ImportMode::ReplaceAll
    } else {
        ImportMode::Merge
    };
    let summary = run(state.store.as_ref(), state.store.as_ref(), &data, mode);

    // 参照先の顔ぶれが変わるので、覚えている存在確認の結果は捨てる
    crate::existence_check::forget_cached(&state);

    // 取り込みの直後は待たずに書き出す
    state.flush();

    let mut message = format!(
        "{} 個の棚と {} 件の項目を取り込みました",
        summary.shelves_imported, summary.items_imported
    );
    let skipped = summary.shelves_skipped + summary.items_skipped;
    if skipped > 0 {
        message.push_str(&format!("（{skipped} 件は取り込みませんでした）"));
    }

    Ok(SimpleResult::note(message))
}
