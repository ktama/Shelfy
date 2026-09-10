//! 検索（doc/SPECIFICATION.md 第 5 節）

use std::collections::HashSet;

use crate::domain::{Item, ItemId, ItemType, ShelfId};
use crate::ports::{ItemRepository, ShelfRepository};
use crate::usecases::items::{with_shelf_names, ItemWithShelf};

/// 検索文字列を解析した結果
#[derive(Debug, Clone, Default, PartialEq, Eq)]
pub struct SearchQuery {
    /// プレフィックスを持たないトークンを空白 1 個で連結したもの
    pub free_text: String,
    /// `box:` Shelf 名の部分一致
    pub shelf_filter: Option<String>,
    /// `type:` 種別の完全一致
    pub type_filter: Option<String>,
    /// `in:` Shelf 名の完全一致
    pub in_shelf_filter: Option<String>,
}

impl SearchQuery {
    /// 条件が 1 つも指定されていない
    pub fn is_empty(&self) -> bool {
        self.free_text.trim().is_empty()
            && self.shelf_filter.is_none()
            && self.type_filter.is_none()
            && self.in_shelf_filter.is_none()
    }

    /// 空白で区切ったトークン列として解析する。
    /// 同じプレフィックスが複数あれば最後の指定が有効になる。
    /// プレフィックスの後ろが空のトークンは無視する。
    pub fn parse(text: &str) -> Self {
        let mut free = Vec::new();
        let mut query = SearchQuery::default();

        for token in text.split_whitespace() {
            if let Some(value) = strip_prefix_ci(token, "box:") {
                if !value.is_empty() {
                    query.shelf_filter = Some(value.to_string());
                }
            } else if let Some(value) = strip_prefix_ci(token, "type:") {
                if !value.is_empty() {
                    query.type_filter = Some(value.to_string());
                }
            } else if let Some(value) = strip_prefix_ci(token, "in:") {
                if !value.is_empty() {
                    query.in_shelf_filter = Some(value.to_string());
                }
            } else {
                free.push(token);
            }
        }

        query.free_text = free.join(" ");
        query
    }
}

/// 大文字小文字を区別せずにプレフィックスを剥がす。
/// 先頭が多バイト文字のときに文字の途中で切らないよう、`get` で境界を確かめる。
fn strip_prefix_ci<'a>(token: &'a str, prefix: &str) -> Option<&'a str> {
    let head = token.get(..prefix.len())?;
    if head.eq_ignore_ascii_case(prefix) {
        Some(token[prefix.len()..].trim())
    } else {
        None
    }
}

/// 検索する。
/// フリーテキストは表示名、参照先、メモ、所属 Shelf 名のいずれかに部分一致すれば該当とする。
/// 絞り込みは、その結果に対して全て満たす形で適用する。
pub fn search_items(
    items: &dyn ItemRepository,
    shelves: &dyn ShelfRepository,
    text: &str,
) -> Vec<ItemWithShelf> {
    let query = SearchQuery::parse(text);
    if query.is_empty() {
        return Vec::new();
    }

    let all_shelves = shelves.all();

    // フリーテキストが空なら全件を対象にし、絞り込みだけを適用する
    let mut found: Vec<Item> = if query.free_text.is_empty() {
        items.all()
    } else {
        let mut hits = items.search(&query.free_text);
        let mut seen: HashSet<ItemId> = hits.iter().map(|i| i.id()).collect();

        // Shelf 名に一致した場合、その Shelf の Item をまとめて加える
        let matching_shelves: Vec<ShelfId> = all_shelves
            .iter()
            .filter(|s| contains_ci(s.name(), &query.free_text))
            .map(|s| s.id())
            .collect();

        for shelf_id in matching_shelves {
            for item in items.by_shelf(shelf_id) {
                if seen.insert(item.id()) {
                    hits.push(item);
                }
            }
        }
        hits
    };

    if let Some(type_name) = &query.type_filter {
        // 既知の 3 種以外は絞り込みに使わない
        if let Some(wanted) = ItemType::from_filter(type_name) {
            found.retain(|i| i.item_type() == wanted);
        }
    }

    if let Some(name) = &query.shelf_filter {
        let matching: HashSet<ShelfId> = all_shelves
            .iter()
            .filter(|s| contains_ci(s.name(), name))
            .map(|s| s.id())
            .collect();
        found.retain(|i| matching.contains(&i.shelf_id()));
    }

    if let Some(name) = &query.in_shelf_filter {
        let matching: HashSet<ShelfId> = all_shelves
            .iter()
            .filter(|s| s.name().eq_ignore_ascii_case(name) || s.name() == name)
            .map(|s| s.id())
            .collect();
        found.retain(|i| matching.contains(&i.shelf_id()));
    }

    with_shelf_names(shelves, found)
}

/// 大文字小文字を区別しない部分一致。パターン文字は普通の文字として扱う。
fn contains_ci(haystack: &str, needle: &str) -> bool {
    haystack.to_lowercase().contains(&needle.to_lowercase())
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::testing::{FixedClock, InMemoryItemRepository, InMemoryShelfRepository};
    use crate::usecases::items::{add_item_with_memo, AddItemResult};
    use crate::usecases::shelves::{create_shelf, CreateShelfResult};
    use time::macros::datetime;

    // --- クエリの解析 ---

    #[test]
    fn parse_collects_free_text_tokens_with_single_spaces() {
        let q = SearchQuery::parse("  報告  書類 ");
        assert_eq!(q.free_text, "報告 書類");
        assert_eq!(q.shelf_filter, None);
    }

    #[test]
    fn parse_reads_the_three_prefixes() {
        let q = SearchQuery::parse("box:仕事 type:url in:ツール report");
        assert_eq!(q.shelf_filter.as_deref(), Some("仕事"));
        assert_eq!(q.type_filter.as_deref(), Some("url"));
        assert_eq!(q.in_shelf_filter.as_deref(), Some("ツール"));
        assert_eq!(q.free_text, "report");
    }

    #[test]
    fn parse_ignores_case_of_the_prefix_itself() {
        let q = SearchQuery::parse("BOX:仕事 Type:File IN:ツール");
        assert_eq!(q.shelf_filter.as_deref(), Some("仕事"));
        assert_eq!(q.type_filter.as_deref(), Some("File"));
        assert_eq!(q.in_shelf_filter.as_deref(), Some("ツール"));
    }

    #[test]
    fn parse_keeps_the_last_value_when_a_prefix_repeats() {
        let q = SearchQuery::parse("box:A box:B");
        assert_eq!(q.shelf_filter.as_deref(), Some("B"));
    }

    #[test]
    fn parse_ignores_prefixes_without_a_value() {
        let q = SearchQuery::parse("box: type: in:");
        assert_eq!(q.shelf_filter, None);
        assert_eq!(q.type_filter, None);
        assert_eq!(q.in_shelf_filter, None);
        assert_eq!(q.free_text, "");
        assert!(q.is_empty());
    }

    #[test]
    fn parse_treats_unknown_prefixes_as_free_text() {
        let q = SearchQuery::parse("tag:重要");
        assert_eq!(q.free_text, "tag:重要");
    }

    #[test]
    fn parse_handles_tokens_shorter_than_a_prefix_and_multibyte_heads() {
        // 先頭が多バイト文字でも、文字の途中で切らずに扱えること
        let q = SearchQuery::parse("報 ツール 仕事");
        assert_eq!(q.free_text, "報 ツール 仕事");
        assert_eq!(q.shelf_filter, None);

        let q = SearchQuery::parse("a");
        assert_eq!(q.free_text, "a");
    }

    #[test]
    fn empty_query_is_reported_as_empty() {
        assert!(SearchQuery::parse("").is_empty());
        assert!(SearchQuery::parse("    ").is_empty());
        assert!(!SearchQuery::parse("a").is_empty());
        assert!(!SearchQuery::parse("type:url").is_empty());
    }

    // --- 検索の実行 ---

    struct Fixture {
        items: InMemoryItemRepository,
        shelves: InMemoryShelfRepository,
        work: ShelfId,
        tools: ShelfId,
    }

    fn fixture() -> Fixture {
        let shelves = InMemoryShelfRepository::new();
        let clock = FixedClock::new(datetime!(2026-01-05 12:00:00 UTC));
        let items = InMemoryItemRepository::new();

        let work = match create_shelf(&shelves, "仕事", None) {
            CreateShelfResult::Success { shelf } => shelf.id(),
            o => panic!("{o:?}"),
        };
        let tools = match create_shelf(&shelves, "ツール", None) {
            CreateShelfResult::Success { shelf } => shelf.id(),
            o => panic!("{o:?}"),
        };

        let add = |shelf, t, target: &str, name: &str, memo: Option<&str>| match add_item_with_memo(
            &items,
            &shelves,
            &clock,
            shelf,
            t,
            target,
            name,
            memo.map(|m| m.to_string()),
        ) {
            AddItemResult::Success { item } => item.id(),
            o => panic!("{o:?}"),
        };

        add(
            work,
            ItemType::File,
            "C:\\work\\report.xlsx",
            "月次レポート",
            Some("経理向け"),
        );
        add(work, ItemType::Folder, "C:\\work\\design", "設計資料", None);
        add(
            tools,
            ItemType::Url,
            "https://example.com/docs",
            "ドキュメント",
            None,
        );
        add(
            tools,
            ItemType::File,
            "C:\\tools\\report-viewer.exe",
            "ビューア",
            None,
        );

        Fixture {
            items,
            shelves,
            work,
            tools,
        }
    }

    fn names(found: &[ItemWithShelf]) -> Vec<String> {
        let mut n: Vec<String> = found
            .iter()
            .map(|r| r.item.display_name().to_string())
            .collect();
        n.sort();
        n
    }

    #[test]
    fn empty_query_returns_nothing() {
        let f = fixture();
        assert!(search_items(&f.items, &f.shelves, "").is_empty());
        assert!(search_items(&f.items, &f.shelves, "   ").is_empty());
    }

    #[test]
    fn free_text_matches_display_name_target_and_memo() {
        let f = fixture();

        assert_eq!(
            names(&search_items(&f.items, &f.shelves, "レポート")),
            vec!["月次レポート"]
        );
        assert_eq!(
            names(&search_items(&f.items, &f.shelves, "design")),
            vec!["設計資料"]
        );
        assert_eq!(
            names(&search_items(&f.items, &f.shelves, "経理")),
            vec!["月次レポート"]
        );
    }

    #[test]
    fn free_text_is_case_insensitive() {
        let f = fixture();
        assert_eq!(
            names(&search_items(&f.items, &f.shelves, "REPORT")).len(),
            2
        );
    }

    #[test]
    fn free_text_matching_a_shelf_name_returns_the_whole_shelf() {
        let f = fixture();
        let found = search_items(&f.items, &f.shelves, "ツール");
        assert_eq!(names(&found), vec!["ドキュメント", "ビューア"]);
    }

    #[test]
    fn shelf_name_hits_are_not_duplicated_with_direct_hits() {
        let f = fixture();
        // 「仕事」は Shelf 名にのみ一致する。所属 2 件がそれぞれ 1 度ずつ出る。
        let found = search_items(&f.items, &f.shelves, "仕事");
        assert_eq!(found.len(), 2);
    }

    #[test]
    fn type_filter_narrows_by_kind() {
        let f = fixture();
        let found = search_items(&f.items, &f.shelves, "type:url");
        assert_eq!(names(&found), vec!["ドキュメント"]);

        let found = search_items(&f.items, &f.shelves, "type:FOLDER");
        assert_eq!(names(&found), vec!["設計資料"]);
    }

    #[test]
    fn unknown_type_filter_is_ignored_rather_than_breaking_the_search() {
        let f = fixture();
        // 不正な値の条件は使わないので、全件が対象になる
        let found = search_items(&f.items, &f.shelves, "type:shortcut");
        assert_eq!(found.len(), 4);
    }

    #[test]
    fn box_filter_matches_shelf_names_partially() {
        let f = fixture();
        let found = search_items(&f.items, &f.shelves, "box:ツー");
        assert_eq!(names(&found), vec!["ドキュメント", "ビューア"]);
    }

    #[test]
    fn in_filter_requires_an_exact_shelf_name() {
        let f = fixture();
        assert_eq!(search_items(&f.items, &f.shelves, "in:ツー").len(), 0);
        assert_eq!(search_items(&f.items, &f.shelves, "in:ツール").len(), 2);
    }

    #[test]
    fn filters_combine_with_free_text() {
        let f = fixture();
        let found = search_items(&f.items, &f.shelves, "report type:file");
        assert_eq!(names(&found), vec!["ビューア", "月次レポート"]);

        let found = search_items(&f.items, &f.shelves, "report type:file in:ツール");
        assert_eq!(names(&found), vec!["ビューア"]);
    }

    #[test]
    fn filters_alone_apply_to_every_item() {
        let f = fixture();
        let found = search_items(&f.items, &f.shelves, "box:仕事");
        assert_eq!(found.len(), 2);
        assert!(found.iter().all(|r| r.item.shelf_id() == f.work));
    }

    #[test]
    fn pattern_characters_are_matched_literally() {
        let f = fixture();
        // SQL のパターン文字が万能記号として働かないこと
        assert!(search_items(&f.items, &f.shelves, "%").is_empty());
        assert!(search_items(&f.items, &f.shelves, "_").is_empty());
    }

    #[test]
    fn results_carry_the_owning_shelf_name() {
        let f = fixture();
        let found = search_items(&f.items, &f.shelves, "ドキュメント");
        assert_eq!(found.len(), 1);
        assert_eq!(found[0].shelf_name, "ツール");
        assert_eq!(found[0].item.shelf_id(), f.tools);
    }
}
