# Shelfy – Design (Clean Architecture)

## 1. 設計方針
- Clean Architecture（Ports & Adapters）を採用
- 依存方向は常に「外 → 内」
- Core は UI / DB / OS を一切知らない

---

## 2. レイヤ構成

```

┌──────────────────────────┐
│ Frameworks / UI (WPF)    │
├──────────────────────────┤
│ Adapters (Infrastructure)│
├──────────────────────────┤
│ Ports (Interfaces)       │
├──────────────────────────┤
│ Use Cases                │
├──────────────────────────┤
│ Domain                   │
└──────────────────────────┘

```

---

## 3. Shelfy.Core 構成

```

Shelfy.Core
├─ Domain
│  ├─ Shelf
│  ├─ Item
│  └─ ValueObjects
├─ UseCases
│  ├─ Shelves
│  ├─ Items
│  ├─ Launch
│  └─ Search
└─ Ports
├─ Persistence
└─ System

```

---

## 4. Domain（依存ゼロ）

### Entities
- Shelf
- Item

### Value Objects
- ShelfId
- ItemId
- ItemType

### 責務
- 構造と制約の表現
- ビジネスルールの保持
- 技術要素を含まない

---

## 5. Use Cases（アプリケーションルール）

### 代表的な Use Case

#### Shelves
- CreateShelf
- RenameShelf
- MoveShelf（階層移動）
- DeleteShelf
- TogglePinShelf
- ReorderShelves（並び順変更）

#### Items
- AddItem
- RemoveItem
- RenameItem
- UpdateItemMemo（メモ更新）
- MoveItemToShelf（別Shelfへ移動）

#### Launch
- LaunchItem
- OpenParentFolder（親フォルダを開く）

#### Search
- SearchItems

#### Utilities
- GetRecentItems
- GetMissingItems

### 特徴
- 1 ユースケース = 1 クラス
- Ports のみを通じて外界に依存
- UI 状態を直接操作しない

### LaunchItem の設計
- 入力：ItemId
- 出力：
  - 起動成功 / 失敗
  - PostAction（HideWindow / KeepWindow）

### OpenParentFolder の設計
- 入力：ItemId
- 出力：成功 / 失敗
- 対象：File / Folder のみ（URL は非対応）

---

## 6. Ports（Core が要求する能力）

### Persistence
- IShelfRepository
- IItemRepository
- ISettingsRepository（ホットキー設定、表示設定等）

### System
- IItemLauncher
- IHotkeyHoldState
- IClock
- IExistenceChecker

---

## 7. Adapters（Infrastructure）

### SQLite
- SqliteShelfRepository
- SqliteItemRepository

### Win32 / OS
- Win32ItemLauncher
- Win32HotkeyHoldState
- FileExistenceChecker
- SystemClock

※ Infrastructure は Core を参照するが逆は禁止

---

## 8. App（WPF）

### 役割
- 表示
- 入力受付
- UseCase 呼び出し
- 結果反映

### 禁止事項
- ビジネスロジックを持たない
- DB / Win32 を直接触らない
