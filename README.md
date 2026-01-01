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
