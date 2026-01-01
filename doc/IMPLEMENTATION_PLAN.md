# Shelfy – Implementation Plan

## フェーズ0：骨格作成
- ソリューション作成
- Shelfy.Core / Infrastructure / App 分離
- DI 基盤構築

---

## フェーズ1：Core 最小実装
目標：UI なしでも振る舞いが成立する状態

- Domain 実装（Shelf / Item）
- Ports 定義
- UseCases：
  - CreateShelf
  - RenameShelf
  - DeleteShelf
  - AddItem
  - RemoveItem
  - RenameItem
  - LaunchItem
- Unit Test（Core のみ）

---

## フェーズ1.5：Core 追加実装
目標：SPECIFICATION.md 記載の全ユースケースを網羅

- UseCases：
  - MoveShelf（階層移動）
  - ReorderShelves（並び順変更）
  - TogglePinShelf（ピン留め）
  - UpdateItemMemo（メモ更新）
  - MoveItemToShelf（別Shelfへ移動）
  - OpenParentFolder（親フォルダを開く）
- Unit Test

---

## フェーズ2：Infrastructure 実装
- SQLite Repository 実装
  - ShelfRepository
  - ItemRepository
  - SettingsRepository
- Win32 ItemLauncher 実装
- HotkeyHoldState 実装
- ExistenceChecker 実装

---

## フェーズ3：App（WPF）
- MainWindow 作成
- ViewModel 作成
- UseCase 接続
- ホットキー表示制御

---

## フェーズ4：操作性強化
- 検索（プレフィックス検索：box:, type:, in:）
- 最近使った
- ピン留め
- 欠損表示
- Shelf 階層移動 UI
- Shelf / Item 並び順変更 UI（ドラッグ＆ドロップ）
- Item のメモ編集 UI

---

## フェーズ5：安定化
- Export / Import（データバックアップ・復元）
- 設定画面（ホットキー設定、表示設定）
- ISettingsRepository 実装
- ログ整備
- README 整備

---

## 技術的優先順位
1. Core の独立性
2. 起動体験
3. 保守性
4. UI 完成度

---

## 完成条件（MVP）
- Core が UI / DB なしでテスト可能
- Use Case が仕様通り振る舞う
- App は単なる Adapter になっている
