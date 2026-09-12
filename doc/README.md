# ドキュメント

Shelfy の仕様と設計をまとめてある。

| 文書                                   | 内容                                                       | 読むとき                           |
| -------------------------------------- | ---------------------------------------------------------- | ---------------------------------- |
| [SPECIFICATION.md](SPECIFICATION.md)   | 振る舞いの規則。ドメイン、ユースケース、検索、画面、非機能 | 何がどう動くべきかを知りたい       |
| [ARCHITECTURE.md](ARCHITECTURE.md)     | 実現の仕方。層、IPC、永続化、Windows 連携、ビルド          | コードを触る                       |
| [DESIGN.md](DESIGN.md)                 | 見た目の決まり。色、文字、寸法、状態、配置、アイコン       | 画面やアイコンを変える             |
| [DATA_MIGRATION.md](DATA_MIGRATION.md) | 保存形式、交換形式、v1.0.0 からの移行                      | データ形式を変える、移行を支援する |
| [DEVELOPMENT.md](DEVELOPMENT.md)       | 環境、テスト、手動確認、計測、CI                           | 手を動かす                         |

振る舞いを変えるときは [SPECIFICATION.md](SPECIFICATION.md) を先に直す。
実装とこの文書が食い違う場合、文書を正とする。

## 前提

- 対象は Windows のみで、他 OS への展開予定はない
- 主な対象は Windows 11。Windows 10 も動作対象に含める
- 想定データ量は Shelf 数百件、Item 数千件
- ネットワーク通信は行わない
- 実行には WebView2 ランタイムが要る（[ARCHITECTURE.md](ARCHITECTURE.md) 第 15.2 節）

想定データ量を超える規模になった場合、永続化の方式（全件をメモリに載せる JSON スナップショット）は見直しの対象になる。
判断の条件は [ARCHITECTURE.md](ARCHITECTURE.md) 第 7.5 節にある。

## v1.0.0 について

Shelfy は 2026 年 9 月に、C# と WPF と SQLite から Rust と Tauri v2 へ全面的に書き直した。
配布物は 174 MiB から 3.48 MiB になっている。

旧実装は `main` には無い。タグ `v1.0.0` から参照できる。

```bash
git checkout v1.0.0
```

選定と移行の経緯は git の履歴に残してある。
現在も効いている判断のうち、知っておく必要があるものは各文書の中に取り込んである。
