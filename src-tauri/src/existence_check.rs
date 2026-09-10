//! 参照先の存在確認を、一覧を出したあとに背景で行う
//! （doc/SPECIFICATION.md 第 7.5 節、第 12 節の 3 番）。
//!
//! 一覧の要素を作るたびにファイルへ問い合わせると、
//! 切断されたネットワークドライブが 1 件混ざるだけで一覧全体が待たされる。
//! 表示は先に済ませ、判明した分から画面へ送る。

use serde::Serialize;
use tauri::{AppHandle, Emitter, Manager, State};

use shelfy::domain::ItemId;
use shelfy::ports::{ExistenceChecker, ItemRepository};

use crate::state::AppState;

/// 一度に送る件数。少しずつ送って、判明した分から印が付くようにする。
const CHUNK: usize = 25;

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ExistenceEntry {
    pub id: String,
    pub exists: bool,
}

/// 指定した項目の存在を背景で確かめ、`existence-updated` で知らせる。
/// すぐ返り、確認そのものは別のスレッドで進む。
#[tauri::command]
pub fn check_existence(app: AppHandle, state: State<'_, AppState>, item_ids: Vec<String>) {
    // 参照先の取り出しだけを先に済ませ、ファイルへの問い合わせは鍵を持たずに行う
    let targets: Vec<(String, String)> = item_ids
        .iter()
        .filter_map(|id| ItemId::parse(id))
        .filter_map(|id| {
            ItemRepository::get(state.store.as_ref(), id)
                .map(|item| (id.to_string(), item.target().to_string()))
        })
        .collect();

    if targets.is_empty() {
        return;
    }

    std::thread::spawn(move || {
        let Some(state) = app.try_state::<AppState>() else {
            return;
        };

        let mut batch: Vec<ExistenceEntry> = Vec::with_capacity(CHUNK);
        for (id, target) in targets {
            let exists = state.existence.exists(&target);
            batch.push(ExistenceEntry { id, exists });

            if batch.len() >= CHUNK {
                let _ = app.emit("existence-updated", &batch);
                batch.clear();
            }
        }

        if !batch.is_empty() {
            let _ = app.emit("existence-updated", &batch);
        }
    });
}

/// 取り込みなどで参照先の顔ぶれが変わったら、覚えている結果を捨てる
pub fn forget_cached(state: &State<'_, AppState>) {
    state.existence.clear();
}
