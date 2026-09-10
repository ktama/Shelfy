//! ドメイン。UI も OS も永続化技術も知らない。
//! 規則は doc/SPECIFICATION.md 第 3 節に対応する。

use std::fmt;

use serde::{Deserialize, Serialize};
use time::OffsetDateTime;
use uuid::Uuid;

/// ドメインの不変条件に反したときの理由
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum DomainError {
    /// Shelf の名前が空、または空白のみ
    EmptyShelfName,
    /// Item の参照先が空、または空白のみ
    EmptyTarget,
    /// Item の表示名が空、または空白のみ
    EmptyDisplayName,
}

impl fmt::Display for DomainError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        let s = match self {
            DomainError::EmptyShelfName => "shelf name cannot be empty",
            DomainError::EmptyTarget => "target cannot be empty",
            DomainError::EmptyDisplayName => "display name cannot be empty",
        };
        f.write_str(s)
    }
}

impl std::error::Error for DomainError {}

fn is_blank(s: &str) -> bool {
    s.trim().is_empty()
}

// ---------------------------------------------------------------- 識別子

/// Shelf の識別子
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, PartialOrd, Ord, Serialize, Deserialize)]
#[serde(transparent)]
pub struct ShelfId(pub Uuid);

impl ShelfId {
    pub fn new() -> Self {
        Self(Uuid::new_v4())
    }

    pub fn parse(s: &str) -> Option<Self> {
        Uuid::parse_str(s).ok().map(Self)
    }
}

impl Default for ShelfId {
    fn default() -> Self {
        Self::new()
    }
}

impl fmt::Display for ShelfId {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}", self.0)
    }
}

/// Item の識別子
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, PartialOrd, Ord, Serialize, Deserialize)]
#[serde(transparent)]
pub struct ItemId(pub Uuid);

impl ItemId {
    pub fn new() -> Self {
        Self(Uuid::new_v4())
    }

    pub fn parse(s: &str) -> Option<Self> {
        Uuid::parse_str(s).ok().map(Self)
    }
}

impl Default for ItemId {
    fn default() -> Self {
        Self::new()
    }
}

impl fmt::Display for ItemId {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}", self.0)
    }
}

// ---------------------------------------------------------------- 種別

/// Item の種別
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub enum ItemType {
    File,
    Folder,
    Url,
}

impl ItemType {
    /// 永続化形式での数値。入出力ファイルの互換性に直結するため変更しない。
    pub fn to_code(self) -> i32 {
        match self {
            ItemType::File => 0,
            ItemType::Folder => 1,
            ItemType::Url => 2,
        }
    }

    pub fn from_code(code: i32) -> Option<Self> {
        match code {
            0 => Some(ItemType::File),
            1 => Some(ItemType::Folder),
            2 => Some(ItemType::Url),
            _ => None,
        }
    }

    /// 検索の `type:` 指定に使う名前
    pub fn from_filter(name: &str) -> Option<Self> {
        match name.to_ascii_lowercase().as_str() {
            "file" => Some(ItemType::File),
            "folder" => Some(ItemType::Folder),
            "url" => Some(ItemType::Url),
            _ => None,
        }
    }
}

// ---------------------------------------------------------------- Shelf

/// 参照を格納する論理的な棚
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Shelf {
    id: ShelfId,
    name: String,
    parent_id: Option<ShelfId>,
    sort_order: i32,
    is_pinned: bool,
}

impl Shelf {
    pub fn new(
        id: ShelfId,
        name: impl Into<String>,
        parent_id: Option<ShelfId>,
        sort_order: i32,
        is_pinned: bool,
    ) -> Result<Self, DomainError> {
        let name = name.into();
        if is_blank(&name) {
            return Err(DomainError::EmptyShelfName);
        }
        Ok(Self {
            id,
            name,
            parent_id,
            sort_order,
            is_pinned,
        })
    }

    pub fn id(&self) -> ShelfId {
        self.id
    }

    pub fn name(&self) -> &str {
        &self.name
    }

    pub fn parent_id(&self) -> Option<ShelfId> {
        self.parent_id
    }

    pub fn sort_order(&self) -> i32 {
        self.sort_order
    }

    pub fn is_pinned(&self) -> bool {
        self.is_pinned
    }

    pub fn rename(&mut self, new_name: impl Into<String>) -> Result<(), DomainError> {
        let new_name = new_name.into();
        if is_blank(&new_name) {
            return Err(DomainError::EmptyShelfName);
        }
        self.name = new_name;
        Ok(())
    }

    /// 親を付け替える。`None` はルート直下を表す。
    /// 循環の禁止はユースケース側で確かめる（祖先をたどる必要があるため）。
    pub fn move_to(&mut self, new_parent: Option<ShelfId>) {
        self.parent_id = new_parent;
    }

    pub fn set_sort_order(&mut self, order: i32) {
        self.sort_order = order;
    }

    pub fn set_pinned(&mut self, pinned: bool) {
        self.is_pinned = pinned;
    }

    /// ピン留めを反転し、反転後の状態を返す
    pub fn toggle_pin(&mut self) -> bool {
        self.is_pinned = !self.is_pinned;
        self.is_pinned
    }
}

// ---------------------------------------------------------------- Item

/// Shelf に属する参照
#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct Item {
    id: ItemId,
    shelf_id: ShelfId,
    item_type: ItemType,
    target: String,
    display_name: String,
    memo: Option<String>,
    sort_order: i32,
    #[serde(with = "time::serde::rfc3339")]
    created_at: OffsetDateTime,
    #[serde(with = "time::serde::rfc3339::option")]
    last_accessed_at: Option<OffsetDateTime>,
}

impl Item {
    #[allow(clippy::too_many_arguments)]
    pub fn new(
        id: ItemId,
        shelf_id: ShelfId,
        item_type: ItemType,
        target: impl Into<String>,
        display_name: impl Into<String>,
        created_at: OffsetDateTime,
        memo: Option<String>,
        sort_order: i32,
        last_accessed_at: Option<OffsetDateTime>,
    ) -> Result<Self, DomainError> {
        let target = target.into();
        let display_name = display_name.into();
        if is_blank(&target) {
            return Err(DomainError::EmptyTarget);
        }
        if is_blank(&display_name) {
            return Err(DomainError::EmptyDisplayName);
        }
        Ok(Self {
            id,
            shelf_id,
            item_type,
            target,
            display_name,
            memo,
            sort_order,
            created_at,
            last_accessed_at,
        })
    }

    pub fn id(&self) -> ItemId {
        self.id
    }

    pub fn shelf_id(&self) -> ShelfId {
        self.shelf_id
    }

    pub fn item_type(&self) -> ItemType {
        self.item_type
    }

    pub fn target(&self) -> &str {
        &self.target
    }

    pub fn display_name(&self) -> &str {
        &self.display_name
    }

    pub fn memo(&self) -> Option<&str> {
        self.memo.as_deref()
    }

    pub fn sort_order(&self) -> i32 {
        self.sort_order
    }

    pub fn created_at(&self) -> OffsetDateTime {
        self.created_at
    }

    pub fn last_accessed_at(&self) -> Option<OffsetDateTime> {
        self.last_accessed_at
    }

    pub fn rename(&mut self, new_display_name: impl Into<String>) -> Result<(), DomainError> {
        let new_display_name = new_display_name.into();
        if is_blank(&new_display_name) {
            return Err(DomainError::EmptyDisplayName);
        }
        self.display_name = new_display_name;
        Ok(())
    }

    pub fn update_memo(&mut self, memo: Option<String>) {
        self.memo = memo;
    }

    pub fn mark_accessed(&mut self, at: OffsetDateTime) {
        self.last_accessed_at = Some(at);
    }

    pub fn set_sort_order(&mut self, order: i32) {
        self.sort_order = order;
    }

    pub fn move_to_shelf(&mut self, new_shelf: ShelfId) {
        self.shelf_id = new_shelf;
    }

    /// 同一 Shelf 内での重複判定。
    /// URL のときだけ大文字小文字を区別し、ファイルとフォルダでは区別しない。
    pub fn is_same_reference(&self, item_type: ItemType, target: &str) -> bool {
        if self.item_type != item_type {
            return false;
        }
        match item_type {
            ItemType::Url => self.target == target,
            ItemType::File | ItemType::Folder => self.target.eq_ignore_ascii_case(target),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use time::macros::datetime;

    fn at() -> OffsetDateTime {
        datetime!(2026-01-05 12:00:00 UTC)
    }

    fn item(target: &str, t: ItemType) -> Item {
        Item::new(
            ItemId::new(),
            ShelfId::new(),
            t,
            target,
            "name",
            at(),
            None,
            0,
            None,
        )
        .unwrap()
    }

    // --- 識別子と種別 ---

    #[test]
    fn ids_are_unique_and_round_trip_through_text() {
        let a = ShelfId::new();
        let b = ShelfId::new();
        assert_ne!(a, b);
        assert_eq!(ShelfId::parse(&a.to_string()), Some(a));
        assert_eq!(ShelfId::parse("not a uuid"), None);

        let c = ItemId::new();
        assert_eq!(ItemId::parse(&c.to_string()), Some(c));
    }

    #[test]
    fn item_type_maps_to_stable_codes() {
        assert_eq!(ItemType::File.to_code(), 0);
        assert_eq!(ItemType::Folder.to_code(), 1);
        assert_eq!(ItemType::Url.to_code(), 2);
        assert_eq!(ItemType::from_code(0), Some(ItemType::File));
        assert_eq!(ItemType::from_code(2), Some(ItemType::Url));
        assert_eq!(ItemType::from_code(3), None);
        assert_eq!(ItemType::from_code(-1), None);
    }

    #[test]
    fn item_type_filter_names_ignore_case_and_reject_unknown() {
        assert_eq!(ItemType::from_filter("FILE"), Some(ItemType::File));
        assert_eq!(ItemType::from_filter("Folder"), Some(ItemType::Folder));
        assert_eq!(ItemType::from_filter("url"), Some(ItemType::Url));
        assert_eq!(ItemType::from_filter("shortcut"), None);
        assert_eq!(ItemType::from_filter(""), None);
    }

    // --- Shelf の生成と変更 ---

    #[test]
    fn shelf_rejects_blank_names() {
        assert_eq!(
            Shelf::new(ShelfId::new(), "", None, 0, false).unwrap_err(),
            DomainError::EmptyShelfName
        );
        assert_eq!(
            Shelf::new(ShelfId::new(), "   ", None, 0, false).unwrap_err(),
            DomainError::EmptyShelfName
        );
        assert!(Shelf::new(ShelfId::new(), "仕事", None, 0, false).is_ok());
    }

    #[test]
    fn shelf_rename_rejects_blank_and_keeps_previous_name() {
        let mut s = Shelf::new(ShelfId::new(), "仕事", None, 0, false).unwrap();
        assert_eq!(s.rename("  ").unwrap_err(), DomainError::EmptyShelfName);
        assert_eq!(s.name(), "仕事");
        s.rename("個人").unwrap();
        assert_eq!(s.name(), "個人");
    }

    #[test]
    fn shelf_move_sort_order_and_pin() {
        let parent = ShelfId::new();
        let mut s = Shelf::new(ShelfId::new(), "仕事", None, 0, false).unwrap();

        s.move_to(Some(parent));
        assert_eq!(s.parent_id(), Some(parent));
        s.move_to(None);
        assert_eq!(s.parent_id(), None);

        s.set_sort_order(7);
        assert_eq!(s.sort_order(), 7);

        assert!(!s.is_pinned());
        assert!(s.toggle_pin());
        assert!(s.is_pinned());
        assert!(!s.toggle_pin());
        assert!(!s.is_pinned());
    }

    // --- Item の生成と変更 ---

    #[test]
    fn item_rejects_blank_target_and_display_name() {
        let e = Item::new(
            ItemId::new(),
            ShelfId::new(),
            ItemType::File,
            "  ",
            "name",
            at(),
            None,
            0,
            None,
        )
        .unwrap_err();
        assert_eq!(e, DomainError::EmptyTarget);

        let e = Item::new(
            ItemId::new(),
            ShelfId::new(),
            ItemType::File,
            "C:\\a.txt",
            "",
            at(),
            None,
            0,
            None,
        )
        .unwrap_err();
        assert_eq!(e, DomainError::EmptyDisplayName);
    }

    #[test]
    fn item_rename_memo_access_order_and_shelf() {
        let mut i = item("C:\\a.txt", ItemType::File);

        assert_eq!(i.rename(" ").unwrap_err(), DomainError::EmptyDisplayName);
        assert_eq!(i.display_name(), "name");
        i.rename("報告書").unwrap();
        assert_eq!(i.display_name(), "報告書");

        assert_eq!(i.memo(), None);
        i.update_memo(Some("月次".into()));
        assert_eq!(i.memo(), Some("月次"));
        i.update_memo(None);
        assert_eq!(i.memo(), None);

        assert_eq!(i.last_accessed_at(), None);
        let t = datetime!(2026-02-20 09:30:00 UTC);
        i.mark_accessed(t);
        assert_eq!(i.last_accessed_at(), Some(t));

        i.set_sort_order(3);
        assert_eq!(i.sort_order(), 3);

        let other = ShelfId::new();
        i.move_to_shelf(other);
        assert_eq!(i.shelf_id(), other);
    }

    #[test]
    fn item_keeps_target_and_created_at_immutable() {
        let i = item("C:\\a.txt", ItemType::File);
        // target と created_at には変更手段を用意していない
        assert_eq!(i.target(), "C:\\a.txt");
        assert_eq!(i.created_at(), at());
    }

    #[test]
    fn reference_comparison_ignores_case_for_files_and_folders() {
        let f = item("C:\\Work\\A.txt", ItemType::File);
        assert!(f.is_same_reference(ItemType::File, "c:\\work\\a.txt"));
        assert!(!f.is_same_reference(ItemType::Folder, "c:\\work\\a.txt"));

        let d = item("C:\\Work", ItemType::Folder);
        assert!(d.is_same_reference(ItemType::Folder, "c:\\WORK"));
    }

    #[test]
    fn reference_comparison_is_case_sensitive_for_urls() {
        let u = item("https://example.com/Path", ItemType::Url);
        assert!(u.is_same_reference(ItemType::Url, "https://example.com/Path"));
        assert!(!u.is_same_reference(ItemType::Url, "https://example.com/path"));
    }
}
