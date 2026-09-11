# 開発

## 1. この文書の位置づけ

Shelfy を手元でビルドし、検査し、計測し、配るための手順を示す。

何を作るかは [SPECIFICATION.md](SPECIFICATION.md)、どう作ってあるかは [ARCHITECTURE.md](ARCHITECTURE.md) にある。
この文書はその 2 つを前提に、実際に手を動かすときの段取りだけを扱う。

## 2. 環境

| 道具     | 版数                         | 備考                             |
| -------- | ---------------------------- | -------------------------------- |
| Rust     | `rust-toolchain.toml` で固定 | 現在 1.96.0、MSVC 向け           |
| Node.js  | 22                           | フロントエンドのビルドにだけ使う |
| WebView2 | Windows 11 は標準搭載        | 実行時に要る。ビルドには要らない |

Rust の版数を固定するのは、更新でビルド結果が変わるのを避けるためである。
`rustup` は `rust-toolchain.toml` を見て自動で切り替える。
リポジトリ直下に置いてあるので、`src-tauri` と `tools/shelfy-migrate` のどちらで作業しても同じ版数になる。

### 2.1 手順

```bash
npm install

# 開発中の起動（フロントは Vite の開発サーバから読む）
npm run tauri dev

# 実行ファイルを作る（インストーラは作らない）
npm run tauri build -- --no-bundle

# フロントエンドの型検査とビルド
npm run build
```

バックエンド側は `src-tauri` で作業する。

```bash
cd src-tauri
cargo fmt --check
cargo clippy --all-targets -- -D warnings
cargo test --all-targets
```

移行ツールは別の crate なので、そちらでも同じ検査を走らせる。

```bash
cd tools/shelfy-migrate
cargo fmt --check
cargo clippy --all-targets -- -D warnings
cargo test
```

Rust のビルドは初回が長い。
`target/` を消さずに済ませ、リリース以外では最適化を効かせない既定の設定でビルドする。
道具は `rusqlite` を bundled で使うため、初回は SQLite 本体の C コードをコンパイルする。

### 2.2 実データに触れずに動かす

環境変数 `SHELFY_DATA_DIR` を指定すると、保存先を差し替えられる。

```powershell
$env:SHELFY_DATA_DIR = "C:\temp\shelfy-test"
```

指定しない場合は `%LOCALAPPDATA%\Shelfy` を使う。
動作確認や実験では必ず差し替える。自分の棚を壊さないためである。

## 3. テスト

### 3.1 現状

src-tauri で 166 個、移行ツールで 3 個、合わせて 169 個。

| 置き場所                       | 件数 | 対象                                     |
| ------------------------------ | ---- | ---------------------------------------- |
| `src/domain.rs`                | 11   | 不変条件、識別子、種別、参照の同一判定   |
| `src/hotkey.rs`                | 10   | ホットキー文字列の解析と正規化           |
| `src/usecases/items.rs`        | 21   | Item のユースケース                      |
| `src/usecases/shelves.rs`      | 16   | Shelf のユースケース                     |
| `src/usecases/search.rs`       | 21   | クエリの解析と一致規則                   |
| `src/usecases/launch.rs`       | 8    | 起動、親フォルダ、起動後の画面制御       |
| `src/usecases/transfer.rs`     | 17   | Export と Import、日時の往復             |
| `src/usecases/settings.rs`     | 7    | 設定の既定値と、解析できない値の扱い     |
| `src/adapters/store.rs`        | 14   | 読み書き、原子的置き換え、破損からの回復 |
| `src/adapters/existence.rs`    | 5    | 存在確認のキャッシュと失効               |
| `src/adapters/windows.rs`      | 6    | Win32 連携の薄い部分                     |
| `src/commands.rs`              | 8    | 画面へ渡す形の契約と識別子の読み取り     |
| `src/mutations.rs`             | 10   | 取り込み時の種別判定と URL の検査        |
| `tests/usecases_over_files.rs` | 6    | 実物のストアを使ったファイル越しの往復   |
| `tests/performance.rs`         | 4    | 1,000 件での検索応答（第 5.3 節）        |
| `tests/migration.rs`           | 2    | 移行ツールの出力を取り込めること         |

ドメインとユースケースのテストは、ポートの trait を `testing.rs` の試験用実装に差し替えて走る。
Tauri も WebView2 も起動しないため速い。

移行ツールは別 crate で、`tools/shelfy-migrate` で `cargo test` すると 3 件走る。
v1.0.0 の形の SQLite を組み立てて変換し、交換形式になることと、元のファイルが 1 バイトも変わらないことを確かめる。
`src-tauri/tests/migration.rs` が「その形を取り込める」側を見ているので、両方で移行経路が閉じる。

### 3.2 網羅すべき観点

振る舞いを変えたり足したりするときは、次の領域に対応するテストがあることを確かめる。
仕様の記録として機能させるための一覧である。

| 領域               | 主に確かめること                                                         |
| ------------------ | ------------------------------------------------------------------------ |
| Item の生成と変更  | 空の参照先と表示名を拒む、改名、メモ更新、アクセス日時、並び順、所属変更 |
| Shelf の生成と変更 | 空の名前を拒む、改名、親の付け替え、並び順、ピン留めの反転               |
| 識別子と種別       | 等価性、文字列表現、種別の数値対応                                       |
| CreateShelf        | 入力検査、親の存在確認、兄弟の最大値 + 1 の並び順                        |
| RenameShelf        | 入力検査、対象の存在確認                                                 |
| MoveShelf          | 自分自身への移動の拒否、子孫への移動の拒否、ルートへの移動               |
| DeleteShelf        | 配下の再帰削除、所属 Item の削除                                         |
| TogglePinShelf     | 反転と結果の返却                                                         |
| ReorderShelves     | 0 から始まる連番の割り当て、空入力、存在しない識別子                     |
| AddItem            | 入力検査、Shelf の存在確認、重複判定の大文字小文字規則、並び順           |
| RemoveItem         | 参照だけを消すこと、対象の存在確認                                       |
| RenameItem         | 入力検査                                                                 |
| UpdateItemMemo     | 未設定への更新を含む                                                     |
| MoveItemToShelf    | 移動先の存在確認、移動先での重複判定、並び順の付け替え                   |
| ReorderItems       | ReorderShelves と同じ規則                                                |
| LaunchItem         | 対象の存在確認、失敗時の扱い、アクセス日時の更新、起動後の画面制御       |
| OpenParentFolder   | URL の非対応、対象の存在確認、失敗時の扱い                               |
| GetRecentItems     | 未アクセスの除外、降順、件数の上限                                       |
| GetMissingItems    | 存在しない参照の抽出、URL の扱い                                         |
| 検索               | クエリの解析、4 つの照合対象、3 種の絞り込み、空クエリ、不正な値         |
| Export と Import   | 書式の往復、全置換とマージ、除外規則                                     |
| 取り込み時の種別   | URL とフォルダとファイルの判別、既定の表示名、空にならないこと           |
| URL の検査         | `http` と `https` の絶対 URL だけを通す                                  |
| 設定               | 既定値、解析できない値の置き換え、復元できない大きさの排除               |
| 画面へ渡す形       | `ItemView` などの鍵の集合と種別の綴りが ipc.ts と一致すること            |

「起動後の画面制御」は、ホットキーの修飾キーが押されたままならウィンドウを残す規則を指す。
これはユースケースの責務であり、画面側で判断しない。

### 3.3 フロントエンド

`npm run build` が `svelte-check` を先に走らせる。
型検査だけで、UI の自動テストは持たない。

表示の分岐は画面での確認に任せる。
分岐を増やすなら、その判断をバックエンドのユースケースへ寄せられないかを先に考える。

## 4. 手動確認

自動テストで担保できない項目を挙げる。
OS との対話が本体である部分は、自動化の費用が見合わない。

release ビルドの実行ファイルを、`SHELFY_DATA_DIR` を差し替えたうえで起動して確認する。

### 4.1 常駐と呼び出し

- [ ] 起動するとトレイに常駐する
- [ ] `Ctrl+Shift+Space` で表示と非表示が切り替わる
- [ ] 表示時にウィンドウが前面に出て、検索入力に文字を打てる
- [ ] `Escape` で非表示になる
- [ ] ウィンドウを閉じても終了せず、トレイに残る
- [ ] トレイの二重クリックで表示される
- [ ] トレイメニューの終了で、関連プロセスが残らずに終了する
- [ ] 二重に起動しようとすると、既存のウィンドウが表示されて新しいプロセスが終わる
- [ ] ホットキーが他アプリと衝突している場合、その旨が表示され、トレイからは使える
- [ ] 非表示のあいだ、CPU 使用率が 0% と見なせる水準にある

### 4.2 Shelf と Item

- [ ] Shelf を作成、改名、削除できる。削除は配下ごと消え、事前に確認が出る
- [ ] Shelf を別の親へ移動でき、自分自身と自分の子孫へは移動できない
- [ ] ピン留めした Shelf が同一階層の先頭に並ぶ
- [ ] エクスプローラからのドロップでファイルとフォルダが追加される
- [ ] 複数ファイルをまとめて落として、一度に追加できる
- [ ] URL を追加でき、`http` と `https` 以外は拒否される
- [ ] 同一 Shelf に同じ参照を追加すると重複として拒否される
- [ ] Item の改名、メモ編集、別 Shelf への移動、削除ができる
- [ ] 削除しても参照先の実体が残っている
- [ ] ドラッグで Shelf と Item を並び替えられ、再起動後も順序が保たれる

### 4.3 起動と検索

- [ ] 二重クリックと `Enter` で Item が起動し、ウィンドウが隠れる
- [ ] ホットキーの修飾キーを押したまま起動すると、ウィンドウが表示されたまま残る
- [ ] 親フォルダを開く操作で、対象が選択された状態のエクスプローラが開く。URL では選べない
- [ ] 検索が入力に追従し、表示名、パス、メモ、Shelf 名に一致する
- [ ] **検索ボックスで日本語を入力でき、変換候補が入力位置に出る**
- [ ] `box:`、`type:`、`in:` が仕様どおり効く
- [ ] `type:` に不正な値を入れても検索が壊れない
- [ ] 検索中に Shelf を選ぶと通常モードに戻り、その Shelf の内容が出る
- [ ] 参照先が存在しない Item に警告が付き、欠損一覧に出る
- [ ] 最近一覧が起動順に並び、表示件数の設定が効く

### 4.4 データ

- [ ] エクスポートした JSON を、v1.0.0 と現在の版の双方が読める
- [ ] 全置換インポートで既存データが置き換わる
- [ ] マージインポートで既存データが残り、重複は増えない
- [ ] 日時が壊れたレコードを含む JSON を取り込んでも、残りが取り込まれる
- [ ] アプリを強制終了させても、直前までの変更が失われない
- [ ] 保存ファイルを壊した状態で起動すると、バックアップから回復して退避先が知らされる

### 4.5 設定と表示

- [ ] ホットキーの変更が再起動なしで反映される
- [ ] ウィンドウサイズの変更が保存され、次の起動で復元される
- [ ] 起動時に表示しない設定が効く
- [ ] OS のライトとダークの切り替えに追従する
- [ ] Windows 11 で Mica が適用され、非対応環境でも背景が破綻しない
- [ ] 100% 以外の表示倍率でも配置が崩れない

### 4.6 実行環境

- [ ] WebView2 が無い環境で、何が足りないかと入手方法が示される
- [ ] Windows 10 と Windows 11 の両方で起動する

## 5. 計測手順

[SPECIFICATION.md](SPECIFICATION.md) 第 11 節の非機能要件を確かめる手順である。
結果は日付、コミット、環境（OS 版数、WebView2 の版数）とともに残す。

### 5.1 配布サイズ

release ビルドで生成した実行ファイルの大きさを測る。

```powershell
(Get-Item ./src-tauri/target/release/shelfy.exe).Length
Compress-Archive -Path ./src-tauri/target/release/shelfy.exe -DestinationPath ./shelfy.zip -Force
(Get-Item ./shelfy.zip).Length
```

### 5.2 起動時間とホットキー応答

外部からの計測ではプロセス終了を待つ必要があり、常駐アプリには使えない。
計測用の構成で、アプリ自身に次の 2 区間を測らせてログへ出す。

- プロセス開始からトレイ常駐が完了するまで
- ホットキーの受信からウィンドウの表示完了まで

10 回試行し、中央値と最大値を記録する。
初回起動と 2 回目以降を分けて記録する。

ホットキーを自動で送る場合、`SendKeys` ではグローバルホットキーが反応しない。
`keybd_event` を使う。

### 5.3 検索応答

Shelf 100 件と Item 1,000 件を生成する `tests/performance.rs` で測る。
代表的なクエリ（フリーテキストのみ、`type:` のみ、`box:` との併用）で検索を 100 回実行し、1 回あたりの所要時間の中央値を記録する。

### 5.4 常駐メモリ

本体のプロセスに加えて WebView の子プロセスが立つ。
親子をたどって合計を取る。

```powershell
$root = (Get-Process shelfy).Id
$all = Get-CimInstance Win32_Process
$ids = @($root)
do {
  $next = $all | Where-Object { $ids -contains $_.ParentProcessId } | Select-Object -ExpandProperty ProcessId
  $new  = $next | Where-Object { $ids -notcontains $_ }
  $ids += $new
} while ($new)
Get-Process -Id $ids | Measure-Object WorkingSet64 -Sum | Select-Object -ExpandProperty Sum
```

起動して 5 分間放置したあと、一覧表示と検索を各 10 回行い、その後の合計を記録する。
表示中と非表示中の両方で測る。

目標に使うのはプライベート ワーキングセットである。
上の `WorkingSet64` の合計には、複数プロセスが共有するページが重複して入る。
両方を記録し、判定はプライベート側で行う。

### 5.5 非表示中の CPU

非表示にして 5 分間放置し、その間の CPU 時間の増加を測る。
`shelfy` と `msedgewebview2` を分けて記録する。
自前のコードが 0 秒であることと、合計が誤差の範囲に収まることを確認する。

## 6. CI とリリース

| 場面             | 走るもの                                                                       |
| ---------------- | ------------------------------------------------------------------------------ |
| push と PR       | `cargo fmt --check`、`cargo clippy -D warnings`、`cargo test`、`npm run build` |
| 同上             | 移行ツールにも同じ 3 つを走らせる                                              |
| 同上（別ジョブ） | `cargo audit`（本体と移行ツール）、`npm audit --audit-level=high`              |
| `v*` タグ        | テスト、release ビルド、サイズ検査、zip 化、GitHub Release の作成              |

定義は [.github/workflows/](../.github/workflows/) にある。

リリースの手順にはサイズの検査を入れてある。
実行ファイルが 10 MB を超えると失敗する。
依存を足したときに、配布サイズが静かに元へ戻るのを防ぐためである。

インストーラは作らない。
release ビルドの実行ファイルをそのまま zip にして資産にする。

## 7. 触るときに気をつけること

過去に時間を取られた箇所を挙げる。

**権限の宣言を忘れると、黙って届かなくなる。**
イベントの受信とドラッグアンドドロップの通知は、`src-tauri/capabilities/default.json` の宣言が要る。
足りなくても例外も警告も出ない。詳細は [ARCHITECTURE.md](ARCHITECTURE.md) 第 6.0 節。

**HTML5 のドラッグアンドドロップは使えない。**
Tauri の OS 水準のドロップを有効にしているため、WebView 内の HTML5 DnD は動かない。
内部の並び替えはポインタイベントで書く。[ARCHITECTURE.md](ARCHITECTURE.md) 第 9 節。

**`[hidden]` は `display:flex` に負ける。**
`app.css` で `[hidden]{display:none!important}` を宣言してある。これを外すと隠したい要素が出る。

**ウィンドウを外から隠すと状態がずれる。**
`ShowWindow(SW_HIDE)` などで外部から隠すと Tauri 側の認識と食い違い、以後の `show()` が効かなくなる。
確認はアプリ自身のホットキーやトレイから行う。

**マルチバイト文字の境界。**
文字列を長さで切る処理は、日本語で必ず踏む。バイト位置で `[..n]` を取らない。
