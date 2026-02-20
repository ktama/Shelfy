# Shelfy

**Shelfy** は「棚（Shelf）」という概念でファイル・フォルダ・URL の参照を整理し、グローバルホットキーで即呼び出して起動できる **軽量ランチャー** です。

## ✨ 特徴

- 🗂️ **シンプルな整理** - タグを使わず「棚に置く」感覚で整理
- ⚡ **高速起動** - グローバルホットキーで即座に呼び出し
- 📁 **参照管理** - 実体ファイルは移動・変更せず、参照のみを管理
- 🔍 **即時検索** - Shelf名、表示名、パス、メモを横断検索
- 🖥️ **常駐型** - システムトレイに常駐し、いつでもアクセス可能

## 📦 対応アイテム

| 種別   | 説明              |
| ------ | ----------------- |
| File   | ファイルへの参照  |
| Folder | フォルダへの参照  |
| URL    | Webページへの参照 |

## 🏗️ アーキテクチャ

Clean Architecture（Ports & Adapters）を採用し、依存方向は常に「外 → 内」です。

```
┌──────────────────────────┐
│ Frameworks / UI (WPF)    │  ← Shelfy.App
├──────────────────────────┤
│ Adapters (Infrastructure)│  ← Shelfy.Infrastructure
├──────────────────────────┤
│ Ports / Use Cases        │  ← Shelfy.Core
├──────────────────────────┤
│ Domain                   │  ← Shelfy.Core
└──────────────────────────┘
```

### プロジェクト構成

| プロジェクト            | 説明                                     |
| ----------------------- | ---------------------------------------- |
| `Shelfy.Core`           | ドメインモデル、ユースケース、ポート定義 |
| `Shelfy.Infrastructure` | リポジトリ実装、システム連携             |
| `Shelfy.App`            | WPF アプリケーション                     |
| `Shelfy.Core.Tests`     | Core 層のユニットテスト                  |

## 🛠️ 開発環境

- **.NET 10** (Windows)
- **WPF** (Windows Presentation Foundation)
- **CommunityToolkit.Mvvm** - MVVM フレームワーク
- **Microsoft.Extensions.DependencyInjection** - DI コンテナ

## 🚀 ビルド方法

```bash
# リポジトリをクローン
git clone https://github.com/ktama/Shelfy.git
cd Shelfy

# ビルド
dotnet build

# テスト実行
dotnet test

# 実行
dotnet run --project src/Shelfy.App
```

## 📖 使い方

### 基本操作

1. **起動** - アプリはシステムトレイに常駐します
2. **呼び出し** - `Ctrl+Shift+Space` でウィンドウを表示/非表示
3. **Shelf 作成** - ツールバーの「New Shelf」または `Ctrl+N`
4. **アイテム追加** - ファイル・フォルダを Shelf 選択中のウィンドウにドラッグ＆ドロップ
5. **アイテム起動** - ダブルクリックまたは `Enter` キー
6. **閉じる** - `Escape` キーでウィンドウを非表示（トレイに常駐）

### 検索

検索ボックスに文字を入力すると即時検索が実行されます。以下のプレフィックスで検索対象を絞り込めます：

| プレフィックス | 説明                                        | 例          |
| -------------- | ------------------------------------------- | ----------- |
| `box:`         | Shelf 名で絞り込み                          | `box:仕事`  |
| `type:`        | アイテムの種別で絞り込み（file/folder/url） | `type:url`  |
| `in:`          | 指定 Shelf 内のアイテムに限定               | `in:ツール` |
| *(なし)*       | 表示名・パス・メモを横断検索                | `report`    |

### 並び替え

- **コンテキストメニュー** - Shelf / Item を右クリック →「⬆ Move Up」「⬇ Move Down」
- **ドラッグ＆ドロップ** - Shelf ツリーや Item リスト内でドラッグして並び替え

### データ管理

- **エクスポート** - ツールバーの「📤 Export」で全データを JSON ファイルに保存
- **インポート** - ツールバーの「📥 Import」で JSON ファイルからデータを復元（全置換 or マージ）
- **設定** - ツールバーの「⚙ Settings」でホットキー、起動時最小化、ウィンドウサイズなどを変更

### キーボードショートカット

| キー               | 操作               |
| ------------------ | ------------------ |
| `Ctrl+Shift+Space` | グローバル呼び出し |
| `Ctrl+N`           | 新規 Shelf 作成    |
| `Enter`            | アイテム起動       |
| `F2`               | アイテム名変更     |
| `Delete`           | アイテム削除       |
| `Escape`           | ウィンドウ非表示   |

## 📖 ドキュメント

詳細なドキュメントは [doc/](doc/) フォルダを参照してください。

| ドキュメント                                         | 説明                 |
| ---------------------------------------------------- | -------------------- |
| [SPECIFICATION.md](doc/SPECIFICATION.md)             | 機能仕様書           |
| [DESIGN.md](doc/DESIGN.md)                           | アーキテクチャ設計書 |
| [IMPLEMENTATION_PLAN.md](doc/IMPLEMENTATION_PLAN.md) | 実装計画             |

## 📝 ライセンス

[MIT License](LICENSE)

Copyright (c) 2025 ktama
