//! v1.0.0（C# と WPF と SQLite）の `shelfy.db` を、
//! 交換形式の JSON へ書き出す一度きりの道具。
//!
//! 使い方は doc/DATA_MIGRATION.md 第 5.2 節にある。
//! 配布物には含めない。SQLite への依存を持つのは、この道具だけである。
//!
//! ```text
//! shelfy-migrate [<shelfy.db のパス>] [<書き出し先の JSON>]
//! ```
//!
//! 省略したときは `%LOCALAPPDATA%\Shelfy\shelfy.db` を読み、
//! 同じフォルダではなく作業フォルダへ書き出す。
//! 元のファイルは読むだけで、変更しない。

use std::path::{Path, PathBuf};
use std::process::ExitCode;

use rusqlite::{Connection, OpenFlags};
use serde_json::{json, Map, Value};

/// 交換形式の書式バージョン（DATA_MIGRATION.md 第 3 節）
const EXCHANGE_VERSION: &str = "1.0";

fn main() -> ExitCode {
    let mut args = std::env::args().skip(1);
    let source = args.next().map(PathBuf::from).unwrap_or_else(default_source);
    let target = args
        .next()
        .map(PathBuf::from)
        .unwrap_or_else(|| PathBuf::from("shelfy_export_from_v1.json"));

    match run(&source, &target) {
        Ok((shelves, items)) => {
            println!("読み込み元: {}", source.display());
            println!("書き出し先: {}", target.display());
            println!("棚 {shelves} 個、項目 {items} 件を書き出しました。");
            println!();
            println!("Shelfy の「取込」から、このファイルを読み込んでください。");
            ExitCode::SUCCESS
        }
        Err(message) => {
            eprintln!("移行できませんでした: {message}");
            ExitCode::FAILURE
        }
    }
}

fn default_source() -> PathBuf {
    let base = std::env::var("LOCALAPPDATA").unwrap_or_else(|_| String::from("."));
    PathBuf::from(base).join("Shelfy").join("shelfy.db")
}

fn run(source: &Path, target: &Path) -> Result<(usize, usize), String> {
    if !source.exists() {
        return Err(format!("{} が見つかりません", source.display()));
    }

    // 読むだけで開く。元のファイルは決して変更しない。
    let connection = Connection::open_with_flags(source, OpenFlags::SQLITE_OPEN_READ_ONLY)
        .map_err(|e| format!("{} を開けません: {e}", source.display()))?;

    let shelves = read_shelves(&connection)?;
    let items = read_items(&connection)?;

    let document = json!({
        "version": EXCHANGE_VERSION,
        "exportedAt": now_rfc3339(),
        "shelves": shelves,
        "items": items,
    });

    let text = serde_json::to_string_pretty(&document)
        .map_err(|e| format!("JSON に変換できません: {e}"))?;
    std::fs::write(target, text).map_err(|e| format!("{} に書けません: {e}", target.display()))?;

    Ok((shelves.len(), items.len()))
}

fn read_shelves(connection: &Connection) -> Result<Vec<Value>, String> {
    let mut statement = connection
        .prepare("SELECT Id, Name, ParentId, SortOrder, IsPinned FROM Shelves")
        .map_err(|e| format!("Shelves を読めません: {e}"))?;

    let rows = statement
        .query_map([], |row| {
            let mut object = Map::new();
            object.insert("id".into(), json!(row.get::<_, String>(0)?));
            object.insert("name".into(), json!(row.get::<_, String>(1)?));
            if let Some(parent) = row.get::<_, Option<String>>(2)? {
                object.insert("parentId".into(), json!(parent));
            }
            object.insert("sortOrder".into(), json!(row.get::<_, i64>(3)?));
            // SQLite では真偽値を整数で持つ
            object.insert("isPinned".into(), json!(row.get::<_, i64>(4)? != 0));
            Ok(Value::Object(object))
        })
        .map_err(|e| format!("Shelves を読めません: {e}"))?;

    rows.collect::<Result<Vec<_>, _>>()
        .map_err(|e| format!("Shelves の行を読めません: {e}"))
}

fn read_items(connection: &Connection) -> Result<Vec<Value>, String> {
    let mut statement = connection
        .prepare(
            "SELECT Id, ShelfId, Type, Target, DisplayName, Memo, SortOrder, CreatedAt, LastAccessedAt
             FROM Items",
        )
        .map_err(|e| format!("Items を読めません: {e}"))?;

    let rows = statement
        .query_map([], |row| {
            let mut object = Map::new();
            object.insert("id".into(), json!(row.get::<_, String>(0)?));
            object.insert("shelfId".into(), json!(row.get::<_, String>(1)?));
            object.insert("type".into(), json!(row.get::<_, i64>(2)?));
            object.insert("target".into(), json!(row.get::<_, String>(3)?));
            object.insert("displayName".into(), json!(row.get::<_, String>(4)?));
            if let Some(memo) = row.get::<_, Option<String>>(5)? {
                object.insert("memo".into(), json!(memo));
            }
            object.insert("sortOrder".into(), json!(row.get::<_, i64>(6)?));
            object.insert("createdAt".into(), json!(row.get::<_, String>(7)?));
            if let Some(accessed) = row.get::<_, Option<String>>(8)? {
                object.insert("lastAccessedAt".into(), json!(accessed));
            }
            Ok(Value::Object(object))
        })
        .map_err(|e| format!("Items を読めません: {e}"))?;

    rows.collect::<Result<Vec<_>, _>>()
        .map_err(|e| format!("Items の行を読めません: {e}"))
}

/// 書き出し時刻。道具のためだけに日時ライブラリを足さず、SQLite に尋ねる。
fn now_rfc3339() -> String {
    Connection::open_in_memory()
        .and_then(|c| {
            c.query_row("SELECT strftime('%Y-%m-%dT%H:%M:%SZ', 'now')", [], |r| {
                r.get::<_, String>(0)
            })
        })
        .unwrap_or_else(|_| String::from("1970-01-01T00:00:00Z"))
}
