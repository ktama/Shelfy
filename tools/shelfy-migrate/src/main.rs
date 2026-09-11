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
    let source = args
        .next()
        .map(PathBuf::from)
        .unwrap_or_else(default_source);
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

#[cfg(test)]
mod tests {
    use super::*;
    use tempfile::TempDir;

    /// v1.0.0 が作る形の `shelfy.db` を組み立てる
    /// （スキーマは doc/DATA_MIGRATION.md 第 2 節）。
    fn build_v1_db(path: &Path) {
        let c = Connection::open(path).unwrap();
        c.execute_batch(
            "CREATE TABLE Shelves (
                 Id TEXT PRIMARY KEY,
                 Name TEXT NOT NULL,
                 ParentId TEXT NULL REFERENCES Shelves(Id) ON DELETE CASCADE,
                 SortOrder INTEGER NOT NULL DEFAULT 0,
                 IsPinned INTEGER NOT NULL DEFAULT 0
             );
             CREATE TABLE Items (
                 Id TEXT PRIMARY KEY,
                 ShelfId TEXT NOT NULL REFERENCES Shelves(Id) ON DELETE CASCADE,
                 Type INTEGER NOT NULL,
                 Target TEXT NOT NULL,
                 DisplayName TEXT NOT NULL,
                 Memo TEXT NULL,
                 SortOrder INTEGER NOT NULL DEFAULT 0,
                 CreatedAt TEXT NOT NULL,
                 LastAccessedAt TEXT NULL
             );
             -- 親を持つ棚、ピン留めされた棚、NULL を含む項目をそれぞれ 1 件ずつ置く
             INSERT INTO Shelves VALUES ('s-1', '仕事', NULL, 0, 1);
             INSERT INTO Shelves VALUES ('s-2', '月次', 's-1', 3, 0);
             INSERT INTO Items VALUES
                 ('i-1', 's-1', 0, 'C:\\work\\report.xlsx', 'report.xlsx',
                  '月次', 3, '2026-01-05T12:00:00.0000000Z', '2026-02-19T09:30:00.0000000Z');
             INSERT INTO Items VALUES
                 ('i-2', 's-2', 2, 'https://example.com', 'example.com',
                  NULL, 0, '2026-01-06T12:00:00.0000000Z', NULL);",
        )
        .unwrap();
    }

    fn migrate(dir: &TempDir) -> Value {
        let source = dir.path().join("shelfy.db");
        let target = dir.path().join("out.json");
        build_v1_db(&source);

        let (shelves, items) = run(&source, &target).expect("書き出せること");
        assert_eq!((shelves, items), (2, 2));

        serde_json::from_str(&std::fs::read_to_string(&target).unwrap()).unwrap()
    }

    #[test]
    fn it_writes_the_exchange_format() {
        let dir = TempDir::new().unwrap();
        let out = migrate(&dir);

        assert_eq!(out["version"], "1.0");
        assert!(out["exportedAt"].is_string());

        let work = &out["shelves"][0];
        assert_eq!(work["id"], "s-1");
        assert_eq!(work["name"], "仕事");
        // SQLite の整数が真偽値になる
        assert_eq!(work["isPinned"], true);
        // NULL の親は鍵ごと落ちる
        assert!(work.get("parentId").is_none());

        let monthly = &out["shelves"][1];
        assert_eq!(monthly["parentId"], "s-1", "階層が保たれる");
        assert_eq!(monthly["sortOrder"], 3, "並び順が保たれる");
        assert_eq!(monthly["isPinned"], false);

        let report = &out["items"][0];
        assert_eq!(report["shelfId"], "s-1");
        assert_eq!(report["type"], 0);
        assert_eq!(report["target"], "C:\\work\\report.xlsx");
        assert_eq!(report["memo"], "月次");
        // 日時は v1.0.0 が書いた小数部 7 桁のまま渡す
        assert_eq!(report["createdAt"], "2026-01-05T12:00:00.0000000Z");
        assert_eq!(report["lastAccessedAt"], "2026-02-19T09:30:00.0000000Z");

        let site = &out["items"][1];
        assert_eq!(site["type"], 2, "種別が保たれる");
        // NULL の鍵は落ちる
        assert!(site.get("memo").is_none());
        assert!(site.get("lastAccessedAt").is_none());
    }

    /// 利用者の唯一のデータを預かるため、元のファイルに触れないことを確かめる。
    #[test]
    fn it_leaves_the_source_untouched() {
        let dir = TempDir::new().unwrap();
        let source = dir.path().join("shelfy.db");
        build_v1_db(&source);

        let before = std::fs::read(&source).unwrap();
        run(&source, &dir.path().join("out.json")).unwrap();
        let after = std::fs::read(&source).unwrap();

        assert_eq!(before, after, "読み取り専用で開くので 1 バイトも変わらない");
    }

    #[test]
    fn it_reports_a_missing_source() {
        let dir = TempDir::new().unwrap();
        let missing = dir.path().join("nowhere.db");

        let message = run(&missing, &dir.path().join("out.json")).unwrap_err();

        assert!(message.contains("見つかりません"), "{message}");
    }
}
