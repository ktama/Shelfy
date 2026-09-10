//! エクスポートとインポート。
//! 書式は doc/rebuild/04-DATA_MIGRATION.md 第 3 節に対応する。

use serde::{Deserialize, Serialize};
use time::format_description::well_known::Rfc3339;
use time::OffsetDateTime;

use crate::domain::{Item, ItemId, ItemType, Shelf, ShelfId};
use crate::ports::{Clock, ItemRepository, ShelfRepository};

/// 交換形式の書式バージョン
pub const EXCHANGE_VERSION: &str = "1.0";

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ExportData {
    pub version: String,
    pub exported_at: String,
    pub shelves: Vec<ShelfData>,
    pub items: Vec<ItemData>,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ShelfData {
    pub id: String,
    pub name: String,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub parent_id: Option<String>,
    pub sort_order: i32,
    pub is_pinned: bool,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ItemData {
    pub id: String,
    pub shelf_id: String,
    /// 0=File、1=Folder、2=Url
    #[serde(rename = "type")]
    pub item_type: i32,
    pub target: String,
    pub display_name: String,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub memo: Option<String>,
    pub sort_order: i32,
    pub created_at: String,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub last_accessed_at: Option<String>,
}

/// 日時を往復可能な書式で書き出す
pub fn format_time(t: OffsetDateTime) -> String {
    t.format(&Rfc3339)
        .unwrap_or_else(|_| String::from("1970-01-01T00:00:00Z"))
}

/// 往復可能な書式の日時を読む。小数部の桁数は問わない。
pub fn parse_time(s: &str) -> Option<OffsetDateTime> {
    OffsetDateTime::parse(s, &Rfc3339).ok()
}

// ------------------------------------------------ ドメインと書式の変換
// 保存用のスナップショットも同じ要素の形を使うため、変換はここに集める。

pub fn shelf_to_data(shelf: &Shelf) -> ShelfData {
    ShelfData {
        id: shelf.id().to_string(),
        name: shelf.name().to_string(),
        parent_id: shelf.parent_id().map(|p| p.to_string()),
        sort_order: shelf.sort_order(),
        is_pinned: shelf.is_pinned(),
    }
}

pub fn item_to_data(item: &Item) -> ItemData {
    ItemData {
        id: item.id().to_string(),
        shelf_id: item.shelf_id().to_string(),
        item_type: item.item_type().to_code(),
        target: item.target().to_string(),
        display_name: item.display_name().to_string(),
        memo: item.memo().map(|m| m.to_string()),
        sort_order: item.sort_order(),
        created_at: format_time(item.created_at()),
        last_accessed_at: item.last_accessed_at().map(format_time),
    }
}

/// 読めないレコードは `None` を返す。呼び出し側はそのレコードだけを飛ばす。
pub fn shelf_from_data(data: &ShelfData) -> Option<Shelf> {
    let id = ShelfId::parse(&data.id)?;
    let parent_id = match &data.parent_id {
        None => None,
        Some(p) => Some(ShelfId::parse(p)?),
    };
    Shelf::new(
        id,
        data.name.clone(),
        parent_id,
        data.sort_order,
        data.is_pinned,
    )
    .ok()
}

/// 読めないレコードは `None` を返す。識別子、種別の数値、日時のいずれかが壊れていれば読めない。
pub fn item_from_data(data: &ItemData) -> Option<Item> {
    let id = ItemId::parse(&data.id)?;
    let shelf_id = ShelfId::parse(&data.shelf_id)?;
    let item_type = ItemType::from_code(data.item_type)?;
    let created_at = parse_time(&data.created_at)?;
    let last_accessed_at = match &data.last_accessed_at {
        None => None,
        Some(s) => Some(parse_time(s)?),
    };
    Item::new(
        id,
        shelf_id,
        item_type,
        data.target.clone(),
        data.display_name.clone(),
        created_at,
        data.memo.clone(),
        data.sort_order,
        last_accessed_at,
    )
    .ok()
}

/// 全 Shelf と全 Item を交換形式にまとめる
pub fn export_data(
    shelves: &dyn ShelfRepository,
    items: &dyn ItemRepository,
    clock: &dyn Clock,
) -> ExportData {
    ExportData {
        version: EXCHANGE_VERSION.to_string(),
        exported_at: format_time(clock.now_utc()),
        shelves: shelves.all().iter().map(shelf_to_data).collect(),
        items: items.all().iter().map(item_to_data).collect(),
    }
}

/// 取り込みの結果。飛ばしたレコードの件数も返す。
#[derive(Debug, Clone, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ImportSummary {
    pub shelves_imported: usize,
    pub items_imported: usize,
    pub shelves_skipped: usize,
    pub items_skipped: usize,
}

/// 取り込みの方式
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ImportMode {
    /// 既存を全て消してから取り込む
    ReplaceAll,
    /// 既存に足す
    Merge,
}

/// 交換形式のデータを取り込む。
/// 条件を満たさないレコードは、そのレコードだけを飛ばして続ける。
pub fn import_data(
    shelves: &dyn ShelfRepository,
    items: &dyn ItemRepository,
    data: &ExportData,
    mode: ImportMode,
) -> ImportSummary {
    if mode == ImportMode::ReplaceAll {
        for item in items.all() {
            items.delete(item.id());
        }
        for shelf in shelves.all() {
            shelves.delete(shelf.id());
        }
    }

    let mut summary = ImportSummary {
        shelves_imported: 0,
        items_imported: 0,
        shelves_skipped: 0,
        items_skipped: 0,
    };

    // 親が先になるように並べ替えてから取り込む
    for shelf_data in order_parents_first(&data.shelves) {
        if import_shelf(shelves, shelf_data) {
            summary.shelves_imported += 1;
        } else {
            summary.shelves_skipped += 1;
        }
    }

    for item_data in &data.items {
        if import_item(shelves, items, item_data) {
            summary.items_imported += 1;
        } else {
            summary.items_skipped += 1;
        }
    }

    summary
}

/// 親を先に並べる。親がファイル内にない、または輪になっている場合も取りこぼさない。
fn order_parents_first(shelves: &[ShelfData]) -> Vec<&ShelfData> {
    let mut ordered: Vec<&ShelfData> = Vec::with_capacity(shelves.len());
    let mut placed: Vec<&str> = Vec::with_capacity(shelves.len());
    let mut remaining: Vec<&ShelfData> = shelves.iter().collect();

    loop {
        let before = remaining.len();
        remaining.retain(|s| {
            let parent_ready = match &s.parent_id {
                None => true,
                Some(p) => placed.iter().any(|id| id == p) || !shelves.iter().any(|o| &o.id == p),
            };
            if parent_ready {
                ordered.push(s);
                placed.push(&s.id);
                false
            } else {
                true
            }
        });
        if remaining.is_empty() || remaining.len() == before {
            break;
        }
    }

    // 親子が輪になっているものは、そのままの順で末尾に置く
    ordered.extend(remaining);
    ordered
}

fn import_shelf(shelves: &dyn ShelfRepository, data: &ShelfData) -> bool {
    let Some(shelf) = shelf_from_data(data) else {
        return false;
    };
    // 既にある識別子は取り込まない
    if shelves.get(shelf.id()).is_some() {
        return false;
    }
    shelves.add(shelf);
    true
}

fn import_item(shelves: &dyn ShelfRepository, items: &dyn ItemRepository, data: &ItemData) -> bool {
    // 識別子、種別の数値、日時のいずれかが読めないレコードは飛ばす（仕様変更 2 番）
    let Some(item) = item_from_data(data) else {
        return false;
    };
    // 所属先が無いものは取り込まない
    if shelves.get(item.shelf_id()).is_none() {
        return false;
    }
    // 既にある識別子は取り込まない
    if items.get(item.id()).is_some() {
        return false;
    }
    // 同一 Shelf 内で参照が重複するものは取り込まない（仕様変更 1 番）
    if items
        .by_shelf(item.shelf_id())
        .iter()
        .any(|i| i.is_same_reference(item.item_type(), item.target()))
    {
        return false;
    }

    items.add(item);
    true
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::testing::{FixedClock, InMemoryItemRepository, InMemoryShelfRepository};
    use crate::usecases::items::{add_item, AddItemResult};
    use crate::usecases::shelves::{create_shelf, CreateShelfResult};
    use time::macros::datetime;

    struct Fixture {
        shelves: InMemoryShelfRepository,
        items: InMemoryItemRepository,
        clock: FixedClock,
        work: ShelfId,
    }

    fn fixture() -> Fixture {
        let shelves = InMemoryShelfRepository::new();
        let items = InMemoryItemRepository::new();
        let clock = FixedClock::new(datetime!(2026-01-05 12:00:00 UTC));
        let work = match create_shelf(&shelves, "仕事", None) {
            CreateShelfResult::Success { shelf } => shelf.id(),
            o => panic!("{o:?}"),
        };
        Fixture {
            shelves,
            items,
            clock,
            work,
        }
    }

    fn add(f: &Fixture, shelf: ShelfId, t: ItemType, target: &str, name: &str) -> Item {
        match add_item(&f.items, &f.shelves, &f.clock, shelf, t, target, name) {
            AddItemResult::Success { item } => item,
            o => panic!("{o:?}"),
        }
    }

    fn empty() -> (InMemoryShelfRepository, InMemoryItemRepository) {
        (
            InMemoryShelfRepository::new(),
            InMemoryItemRepository::new(),
        )
    }

    // --- 日時の書式 ---

    #[test]
    fn time_round_trips_through_the_exchange_format() {
        let t = datetime!(2026-02-20 03:04:05.678901 UTC);
        let text = format_time(t);
        assert_eq!(parse_time(&text), Some(t));
    }

    #[test]
    fn time_parser_accepts_the_seven_digit_form_written_by_the_current_version() {
        // C# の "O" 書式は小数部が 7 桁になる
        let parsed = parse_time("2026-01-05T12:00:00.1234567Z");
        assert!(parsed.is_some());
    }

    #[test]
    fn time_parser_rejects_broken_values() {
        assert_eq!(parse_time("not a date"), None);
        assert_eq!(parse_time(""), None);
        assert_eq!(parse_time("2026-13-45T99:99:99Z"), None);
    }

    // --- Export ---

    #[test]
    fn export_carries_every_field() {
        let f = fixture();
        let child = match create_shelf(&f.shelves, "月次", Some(f.work)) {
            CreateShelfResult::Success { shelf } => shelf,
            o => panic!("{o:?}"),
        };
        let mut item = add(&f, f.work, ItemType::File, "C:\\report.xlsx", "報告書");
        item.update_memo(Some("経理向け".into()));
        item.mark_accessed(datetime!(2026-02-19 09:30:00 UTC));
        f.items.update(item.clone());

        let data = export_data(&f.shelves, &f.items, &f.clock);

        assert_eq!(data.version, "1.0");
        assert_eq!(
            data.exported_at,
            format_time(datetime!(2026-01-05 12:00:00 UTC))
        );
        assert_eq!(data.shelves.len(), 2);
        assert_eq!(data.items.len(), 1);

        let exported_child = data.shelves.iter().find(|s| s.name == "月次").unwrap();
        assert_eq!(
            exported_child.parent_id.as_deref(),
            Some(f.work.to_string().as_str())
        );
        assert_eq!(exported_child.id, child.id().to_string());

        let exported = &data.items[0];
        assert_eq!(exported.item_type, 0);
        assert_eq!(exported.target, "C:\\report.xlsx");
        assert_eq!(exported.display_name, "報告書");
        assert_eq!(exported.memo.as_deref(), Some("経理向け"));
        assert_eq!(
            exported.created_at,
            format_time(datetime!(2026-01-05 12:00:00 UTC))
        );
        assert_eq!(
            exported.last_accessed_at.as_deref(),
            Some(format_time(datetime!(2026-02-19 09:30:00 UTC)).as_str())
        );
    }

    #[test]
    fn export_uses_camel_case_keys_and_omits_unset_values() {
        let f = fixture();
        add(&f, f.work, ItemType::Url, "https://example.com", "例");
        let data = export_data(&f.shelves, &f.items, &f.clock);
        let json = serde_json::to_string(&data).unwrap();

        assert!(json.contains("\"exportedAt\""));
        assert!(json.contains("\"displayName\""));
        assert!(json.contains("\"sortOrder\""));
        assert!(json.contains("\"isPinned\""));
        assert!(json.contains("\"type\":2"));
        // 未設定の値は出力しない
        assert!(!json.contains("\"memo\""));
        assert!(!json.contains("\"lastAccessedAt\""));
        assert!(!json.contains("\"parentId\""));
    }

    // --- 往復 ---

    #[test]
    fn exported_data_can_be_imported_into_an_empty_store() {
        let f = fixture();
        let child = match create_shelf(&f.shelves, "月次", Some(f.work)) {
            CreateShelfResult::Success { shelf } => shelf.id(),
            o => panic!("{o:?}"),
        };
        add(&f, f.work, ItemType::File, "C:\\1.txt", "1");
        add(&f, child, ItemType::Url, "https://example.com", "2");

        let data = export_data(&f.shelves, &f.items, &f.clock);
        let text = serde_json::to_string(&data).unwrap();
        let restored: ExportData = serde_json::from_str(&text).unwrap();

        let (shelves, items) = empty();
        let summary = import_data(&shelves, &items, &restored, ImportMode::Merge);

        assert_eq!(summary.shelves_imported, 2);
        assert_eq!(summary.items_imported, 2);
        assert_eq!(summary.items_skipped, 0);

        let again = export_data(&shelves, &items, &f.clock);
        let mut before = data.items.clone();
        let mut after = again.items.clone();
        before.sort_by(|a, b| a.id.cmp(&b.id));
        after.sort_by(|a, b| a.id.cmp(&b.id));
        assert_eq!(before, after);
    }

    #[test]
    fn import_orders_parents_before_children_regardless_of_file_order() {
        let f = fixture();
        let child = match create_shelf(&f.shelves, "月次", Some(f.work)) {
            CreateShelfResult::Success { shelf } => shelf.id(),
            o => panic!("{o:?}"),
        };
        let mut data = export_data(&f.shelves, &f.items, &f.clock);
        // 子が先に並んだファイルを模す
        data.shelves.sort_by_key(|s| s.parent_id.is_none());

        let (shelves, items) = empty();
        let summary = import_data(&shelves, &items, &data, ImportMode::Merge);

        assert_eq!(summary.shelves_imported, 2);
        assert_eq!(shelves.get(child).unwrap().parent_id(), Some(f.work));
    }

    // --- 取り込みの方式 ---

    #[test]
    fn replace_all_clears_existing_data_first() {
        let f = fixture();
        add(&f, f.work, ItemType::File, "C:\\1.txt", "1");
        let data = export_data(&f.shelves, &f.items, &f.clock);

        let (shelves, items) = empty();
        let other = match create_shelf(&shelves, "消える棚", None) {
            CreateShelfResult::Success { shelf } => shelf.id(),
            o => panic!("{o:?}"),
        };
        let clock = FixedClock::new(datetime!(2026-01-05 12:00:00 UTC));
        add_item(
            &items,
            &shelves,
            &clock,
            other,
            ItemType::File,
            "C:\\old.txt",
            "old",
        );

        import_data(&shelves, &items, &data, ImportMode::ReplaceAll);

        assert_eq!(shelves.count(), 1);
        assert_eq!(items.count(), 1);
        assert!(shelves.get(other).is_none());
    }

    #[test]
    fn merge_keeps_existing_data() {
        let f = fixture();
        add(&f, f.work, ItemType::File, "C:\\1.txt", "1");
        let data = export_data(&f.shelves, &f.items, &f.clock);

        let (shelves, items) = empty();
        let other = match create_shelf(&shelves, "残る棚", None) {
            CreateShelfResult::Success { shelf } => shelf.id(),
            o => panic!("{o:?}"),
        };
        let clock = FixedClock::new(datetime!(2026-01-05 12:00:00 UTC));
        add_item(
            &items,
            &shelves,
            &clock,
            other,
            ItemType::File,
            "C:\\old.txt",
            "old",
        );

        import_data(&shelves, &items, &data, ImportMode::Merge);

        assert_eq!(shelves.count(), 2);
        assert_eq!(items.count(), 2);
        assert!(shelves.get(other).is_some());
    }

    #[test]
    fn importing_the_same_file_twice_adds_nothing() {
        let f = fixture();
        add(&f, f.work, ItemType::File, "C:\\1.txt", "1");
        let data = export_data(&f.shelves, &f.items, &f.clock);

        let (shelves, items) = empty();
        import_data(&shelves, &items, &data, ImportMode::Merge);
        let second = import_data(&shelves, &items, &data, ImportMode::Merge);

        assert_eq!(second.shelves_imported, 0);
        assert_eq!(second.items_imported, 0);
        assert_eq!(second.shelves_skipped, 1);
        assert_eq!(second.items_skipped, 1);
        assert_eq!(items.count(), 1);
    }

    // --- 除外の規則 ---

    #[test]
    fn items_without_their_shelf_are_skipped() {
        let f = fixture();
        add(&f, f.work, ItemType::File, "C:\\1.txt", "1");
        let mut data = export_data(&f.shelves, &f.items, &f.clock);
        data.shelves.clear();

        let (shelves, items) = empty();
        let summary = import_data(&shelves, &items, &data, ImportMode::Merge);

        assert_eq!(summary.items_imported, 0);
        assert_eq!(summary.items_skipped, 1);
        assert_eq!(items.count(), 0);
    }

    #[test]
    fn items_with_an_unknown_type_code_are_skipped() {
        let f = fixture();
        add(&f, f.work, ItemType::File, "C:\\1.txt", "1");
        let mut data = export_data(&f.shelves, &f.items, &f.clock);
        data.items[0].item_type = 9;

        let (shelves, items) = empty();
        let summary = import_data(&shelves, &items, &data, ImportMode::Merge);

        assert_eq!(summary.items_imported, 0);
        assert_eq!(summary.items_skipped, 1);
    }

    #[test]
    fn a_broken_timestamp_skips_only_that_record() {
        // 仕様変更 2 番: 現行は処理全体を中断していた
        let f = fixture();
        add(&f, f.work, ItemType::File, "C:\\1.txt", "1");
        add(&f, f.work, ItemType::File, "C:\\2.txt", "2");
        add(&f, f.work, ItemType::File, "C:\\3.txt", "3");
        let mut data = export_data(&f.shelves, &f.items, &f.clock);
        data.items[1].created_at = "壊れた日時".into();

        let (shelves, items) = empty();
        let summary = import_data(&shelves, &items, &data, ImportMode::Merge);

        assert_eq!(summary.items_imported, 2);
        assert_eq!(summary.items_skipped, 1);
        assert_eq!(items.count(), 2);
    }

    #[test]
    fn duplicate_references_within_a_shelf_are_skipped() {
        // 仕様変更 1 番: 現行は検査していなかった
        let f = fixture();
        add(&f, f.work, ItemType::File, "C:\\Work\\A.txt", "A");
        let mut data = export_data(&f.shelves, &f.items, &f.clock);

        // 識別子だけ違う、同じ参照のレコードを足す
        let mut duplicate = data.items[0].clone();
        duplicate.id = ItemId::new().to_string();
        duplicate.target = "c:\\work\\a.txt".into();
        duplicate.display_name = "同じもの".into();
        data.items.push(duplicate);

        let (shelves, items) = empty();
        let summary = import_data(&shelves, &items, &data, ImportMode::Merge);

        assert_eq!(summary.items_imported, 1);
        assert_eq!(summary.items_skipped, 1);
        assert_eq!(items.count(), 1);
    }

    #[test]
    fn shelves_with_a_blank_name_are_skipped() {
        let f = fixture();
        let mut data = export_data(&f.shelves, &f.items, &f.clock);
        data.shelves[0].name = "   ".into();

        let (shelves, items) = empty();
        let summary = import_data(&shelves, &items, &data, ImportMode::Merge);

        assert_eq!(summary.shelves_imported, 0);
        assert_eq!(summary.shelves_skipped, 1);
    }

    #[test]
    fn records_with_an_unparsable_id_are_skipped() {
        let f = fixture();
        add(&f, f.work, ItemType::File, "C:\\1.txt", "1");
        let mut data = export_data(&f.shelves, &f.items, &f.clock);
        data.items[0].id = "not-a-uuid".into();

        let (shelves, items) = empty();
        let summary = import_data(&shelves, &items, &data, ImportMode::Merge);

        assert_eq!(summary.items_imported, 0);
        assert_eq!(summary.items_skipped, 1);
    }

    #[test]
    fn a_file_written_by_the_current_version_can_be_read() {
        // 現行の C# 版が書き出す形（小数部 7 桁、camelCase）
        let json = r#"{
          "version": "1.0",
          "exportedAt": "2026-02-20T03:04:05.6789012Z",
          "shelves": [
            { "id": "9f1c2d3e-4a5b-6c7d-8e9f-0a1b2c3d4e5f", "name": "仕事", "parentId": null, "sortOrder": 0, "isPinned": true }
          ],
          "items": [
            {
              "id": "1a2b3c4d-5e6f-7a8b-9c0d-1e2f3a4b5c6d",
              "shelfId": "9f1c2d3e-4a5b-6c7d-8e9f-0a1b2c3d4e5f",
              "type": 0,
              "target": "C:\\work\\report.xlsx",
              "displayName": "report.xlsx",
              "memo": "月次",
              "sortOrder": 3,
              "createdAt": "2026-01-05T12:00:00.0000000Z",
              "lastAccessedAt": "2026-02-19T09:30:00.0000000Z"
            }
          ]
        }"#;

        let data: ExportData = serde_json::from_str(json).unwrap();
        let (shelves, items) = empty();
        let summary = import_data(&shelves, &items, &data, ImportMode::Merge);

        assert_eq!(summary.shelves_imported, 1);
        assert_eq!(summary.items_imported, 1);

        let shelf = shelves.all().into_iter().next().unwrap();
        assert_eq!(shelf.name(), "仕事");
        assert!(shelf.is_pinned());

        let item = items.all().into_iter().next().unwrap();
        assert_eq!(item.target(), "C:\\work\\report.xlsx");
        assert_eq!(item.memo(), Some("月次"));
        assert_eq!(item.sort_order(), 3);
        assert_eq!(item.created_at(), datetime!(2026-01-05 12:00:00 UTC));
    }
}
