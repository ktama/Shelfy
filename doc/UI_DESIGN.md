# Shelfy – UI Design (Fluent Design)

## 1. 方針

### 採用ライブラリ
- **WPF UI (`Wpf.Ui`)** — Windows 11 Fluent Design System 準拠
- NuGet パッケージ: `WPF-UI`

### カラーテーマ
- **システム連動**（Windows のライト/ダーク設定に自動追従）
- Mica / Acrylic 背景を適用し、OS との一体感を実現

### デザイン原則
- Windows 11 ネイティブアプリと遜色ない見た目
- ランチャーとしての即時性・軽量感を維持
- 絵文字アイコン → **Segoe Fluent Icons（SymbolRegular）** に統一
- 角丸・適切な余白・コントラストの確保

---

## 2. ウィンドウ構成

### メインウィンドウ

```
┌─────────────────────────────────────────────────────────┐
│  [Mica/Acrylic 背景]  FluentWindow                      │
│  ─ TitleBar (カスタム、検索ボックス統合)  ──────────────  │
├─────────────────────────────────────────────────────────┤
│  NavigationView (Left compact)  │  Content Area          │
│  ┌───────────────────────┐     │  ┌───────────────────┐ │
│  │ 📌 Pinned Shelves     │     │  │ Item Cards        │ │
│  │ ─────────────────     │     │  │ (CardControl)     │ │
│  │ 📁 Shelf Tree         │     │  │                   │ │
│  │   ├─ Shelf A          │     │  │ ┌───────────────┐ │ │
│  │   │  ├─ Child 1       │     │  │ │ 🗎  File.txt  │ │ │
│  │   │  └─ Child 2       │     │  │ │ C:\path\...   │ │ │
│  │   └─ Shelf B          │     │  │ │ memo text     │ │ │
│  │                       │     │  │ └───────────────┘ │ │
│  │ ─────────────────     │     │  │                   │ │
│  │ ⏱ Recent              │     │  │ ┌───────────────┐ │ │
│  │ ⚠ Missing             │     │  │ │ 🌐  Google    │ │ │
│  │ ⚙ Settings            │     │  │ │ https://...   │ │ │
│  └───────────────────────┘     │  │ └───────────────┘ │ │
│                                │  └───────────────────┘ │
├─────────────────────────────────────────────────────────┤
│  InfoBar (ステータスメッセージ表示)                       │
└─────────────────────────────────────────────────────────┘
```

### レイアウト詳細

| 領域         | WPF UI コントロール              | 説明                             |
| ------------ | -------------------------------- | -------------------------------- |
| ウィンドウ   | `FluentWindow`                   | Mica 背景、カスタムタイトルバー  |
| タイトルバー | `TitleBar`                       | アプリ名 + 検索 `AutoSuggestBox` |
| 左パネル     | `NavigationView` (Left, Compact) | Shelf ツリー + 固定メニュー      |
| Shelf ツリー | `TreeView` (Wpf.Ui テーマ適用)   | 角丸選択ハイライト               |
| アイテム一覧 | `ListView` + `CardControl`       | カード型レイアウト               |
| ステータス   | `InfoBar`                        | 操作結果の通知                   |

---

## 3. コンポーネント変更マッピング

### メインウィンドウ

| 現在                     | 変更後                                   | 備考                                     |
| ------------------------ | ---------------------------------------- | ---------------------------------------- |
| `Window`                 | `FluentWindow`                           | Mica 背景自動適用                        |
| `ToolBar`                | **廃止** → `TitleBar` + `NavigationView` | ツールバーボタンを再配置                 |
| `TreeView`               | `TreeView`（Fluent テーマ適用）          | スタイルのみ変更                         |
| `ListView`               | `ListView` + `CardControl`               | カード型アイテム表示                     |
| `StatusBar`              | `InfoBar` / `Snackbar`                   | 一時通知はSnackbar                       |
| `GridSplitter`           | `Grid` 固定幅 or `NavigationView` 内蔵   | 左右分割を NavigationView に統合         |
| 絵文字アイコン（📁🌐🕐⚠️等） | `SymbolIcon` (`SymbolRegular`)           | Segoe Fluent Icons                       |
| `TextBox`（検索）        | `AutoSuggestBox`                         | タイトルバーに統合、インクリメンタル検索 |

### ダイアログ

| 現在                         | 変更後                                       | 備考                      |
| ---------------------------- | -------------------------------------------- | ------------------------- |
| `InputDialog` (Window)       | `ContentDialog`                              | モーダルオーバーレイ      |
| `MemoEditDialog` (Window)    | `ContentDialog`                              | 複数行テキスト、モーダル  |
| `SettingsDialog` (Window)    | `NavigationView` 内ページ or `ContentDialog` | Settings ページとして統合 |
| `ShelfPickerDialog` (Window) | `ContentDialog` + `TreeView`                 | モーダルオーバーレイ      |

### ツールバーボタン再配置

| 現ボタン              | 移動先                                   | コントロール         |
| --------------------- | ---------------------------------------- | -------------------- |
| New Shelf / New Child | Shelf ツリー上部 or コンテキストメニュー | `Button` (Fluent)    |
| Add URL               | アイテム一覧上部 or コンテキストメニュー | `Button` (Fluent)    |
| Recent                | `NavigationView` フッター                | `NavigationViewItem` |
| Missing               | `NavigationView` フッター                | `NavigationViewItem` |
| Refresh               | コマンドバー or Ctrl+R                   | キーバインドのみ     |
| Export / Import       | Settings ページ内                        | `Button` (Fluent)    |
| Settings              | `NavigationView` フッター                | `NavigationViewItem` |
| Search                | `TitleBar` 内 `AutoSuggestBox`           | 常時表示             |

---

## 4. アイコン体系

絵文字を廃止し、Segoe Fluent Icons (`SymbolRegular`) に統一する。

| 用途              | 現在 | 変更後 (SymbolRegular) |
| ----------------- | ---- | ---------------------- |
| Shelf（通常）     | 📁    | `Folder24`             |
| Shelf（ピン留め） | 📌    | `Pin24`                |
| File              | 🗎    | `Document24`           |
| Folder            | 📂    | `FolderOpen24`         |
| URL               | 🌐    | `Globe24`              |
| Recent            | 🕐    | `Clock24`              |
| Missing           | ⚠️    | `Warning24`            |
| Settings          | ⚙    | `Settings24`           |
| Search            | —    | `Search24`             |
| Export            | 📤    | `ArrowUpload24`        |
| Import            | 📥    | `ArrowDownload24`      |
| Add               | —    | `Add24`                |
| Delete            | —    | `Delete24`             |
| Rename            | —    | `Edit24`               |
| Memo              | 📝    | `Notepad24`            |
| Move              | 📂    | `FolderArrowRight24`   |
| Refresh           | 🔄    | `ArrowSync24`          |

---

## 5. アイテムカードデザイン

```
┌──────────────────────────────────────────────┐
│  [Icon]   Display Name                 [⚠]  │
│           C:\path\to\file.txt                │
│           memo: some note here...            │
│                              2026/02/20      │
└──────────────────────────────────────────────┘
```

- `CardControl` or カスタム `DataTemplate` でカード風に
- 角丸（CornerRadius 4-8px）
- ホバー時にサブトルなハイライト
- 欠損アイテムは `Warning` アイコン + `Caution` カラー
- ダークモード時はカード背景を `ControlFillColorDefault` で統一

---

## 6. テーマ設定

### App.xaml 構成

```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ui:ThemesDictionary Theme="Dark" />
            <ui:ControlsDictionary />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

- `ThemesDictionary` の初期値は `"Dark"` だが、`App.xaml.cs` で `ApplicationThemeManager` + `SystemThemeWatcher` を用いてシステム設定に自動追従する
- `FluentWindow.WindowBackdropType="Mica"` でウィンドウ背景
- Mica 非対応環境では自動フォールバック

---

## 7. ダイアログのモダン化

### ContentDialog パターン

従来の別ウィンドウダイアログを `ContentDialog`（オーバーレイ型）に置き換える。

**利点**:
- 画面遷移なし（ウィンドウ内にオーバーレイ表示）
- Fluent Design 統一
- アニメーション付きの表示/非表示

**対象**:
- InputDialog → ContentDialog（TextBox 1 つ）
- MemoEditDialog → ContentDialog（TextBox 複数行）
- ShelfPickerDialog → ContentDialog（TreeView 埋め込み）
- SettingsDialog → Settings ページ（NavigationView 内）or ContentDialog

---

## 8. コンテキストメニュー

WPF UI のテーマ適用済み `ContextMenu` を使用。

- 角丸メニュー
- Fluent アイコン付きメニュー項目
- アクセラレータキー表示

---

## 9. アニメーション・トランジション

- ページ遷移: `NavigationView` 組み込みアニメーション
- ContentDialog: フェードイン/アウト（WPF UI 標準）
- リスト操作: 追加/削除時のサブトルアニメーション（オプション）
- 過度なアニメーションは避け、ランチャーの即時性を損なわない

---

## 10. 実装概要

### パッケージ追加
```xml
<PackageReference Include="WPF-UI" Version="3.*" />
```

### 主要変更ファイル

| ファイル               | 変更内容                            |
| ---------------------- | ----------------------------------- |
| `App.xaml`             | テーマリソース追加                  |
| `App.xaml.cs`          | テーマ初期化                        |
| `MainWindow.xaml`      | FluentWindow 化、レイアウト全面刷新 |
| `MainWindow.xaml.cs`   | コードビハインド調整                |
| `InputDialog.cs`       | ContentDialog に置き換え            |
| `MemoEditDialog.cs`    | ContentDialog に置き換え            |
| `ShelfPickerDialog.cs` | ContentDialog に置き換え            |
| `SettingsDialog.cs`    | Settings ページ化 or ContentDialog  |
| `Converters.cs`        | 不要コンバーター削除、新規追加      |

### 新規ファイル（想定）

| ファイル                  | 内容                       |
| ------------------------- | -------------------------- |
| `Styles/AppStyles.xaml`   | アプリ固有スタイル         |
| `Pages/SettingsPage.xaml` | 設定ページ                 |
| `Controls/ItemCard.xaml`  | アイテムカードテンプレート |
