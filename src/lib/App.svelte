<script lang="ts">
  import { onMount } from "svelte";
  import { getCurrentWindow } from "@tauri-apps/api/window";
  import { getCurrentWebview } from "@tauri-apps/api/webview";
  import { open as openFile, save as saveFile } from "@tauri-apps/plugin-dialog";

  import { ipc, onExistenceUpdated, onHotkeyUnavailable, onWindowShown } from "./ipc";
  import type { ItemView, SettingsView, ShelfView } from "./ipc";
  import { ICON } from "./icons";
  import appIcon from "../../src-tauri/icons/source-16.svg";
  import ShelfTree from "./components/ShelfTree.svelte";
  import type { ShelfNode } from "./components/ShelfTree.svelte";
  import ItemList from "./components/ItemList.svelte";
  import EmptyState from "./components/EmptyState.svelte";
  import Dialog from "./components/Dialog.svelte";
  import ContextMenu from "./components/ContextMenu.svelte";
  import type { MenuEntry } from "./components/ContextMenu.svelte";

  type ViewMode = "normal" | "search" | "recent" | "missing";

  const SEARCH_DELAY_MS = 100;

  let shelves = $state<ShelfView[]>([]);
  let items = $state<ItemView[]>([]);
  let selectedShelfId = $state<string | null>(null);
  let selectedItemId = $state<string | null>(null);
  let mode = $state<ViewMode>("normal");
  let query = $state("");
  let status = $state("");
  let readOnly = $state(false);
  /** 最初の読み込みが済んだか。済むまでは空状態を出さない。 */
  let loaded = $state(false);
  /** 参照先が見つからない項目。背景での確認が届くたびに更新される。 */
  let missing = $state(new Set<string>());
  /** エクスプローラーからドラッグ中の件数。ドラッグしていなければ null。 */
  let dropping = $state<number | null>(null);

  /** 一覧を出したあとに、参照先の確認を背景で頼む（第 7.5 節） */
  function checkExistenceOf(list: ItemView[]) {
    if (list.length === 0) return;
    void ipc.checkExistence(list.map((i) => i.id));
  }
  let settings = $state<SettingsView | null>(null);

  let searchBox: HTMLInputElement | null = $state(null);
  let moreButton: HTMLButtonElement | null = $state(null);
  let searchTimer: number | undefined;

  const tree: ShelfNode[] = $derived(buildTree(shelves));
  const selectedShelf = $derived(shelves.find((s) => s.id === selectedShelfId) ?? null);
  const selectedItem = $derived(items.find((i) => i.id === selectedItemId) ?? null);
  const title = $derived(
    mode === "search"
      ? `検索: ${query}`
      : mode === "recent"
        ? "最近使ったもの"
        : mode === "missing"
          ? "見つからない項目"
          : (selectedShelf?.name ?? ""),
  );
  /** 一覧の見出しを出すか。棚が 1 つも無い通常の表示では出さない。 */
  const hasHeading = $derived(mode !== "normal" || selectedShelf !== null);

  // ------------------------------------------------------------ ダイアログ

  type Prompt = {
    open: boolean;
    title: string;
    label: string;
    value: string;
    multiline: boolean;
    confirmLabel: string;
    allowEmpty: boolean;
    ondone: (value: string) => void;
  };

  const closedPrompt: Prompt = {
    open: false,
    title: "",
    label: "",
    value: "",
    multiline: false,
    confirmLabel: "OK",
    allowEmpty: false,
    ondone: () => {},
  };

  let prompt = $state<Prompt>({ ...closedPrompt });
  let confirmBox = $state({ open: false, title: "", body: "", onyes: () => {} });
  let picker = $state({ open: false, title: "", allowRoot: false, onpick: (_: string | null) => {} });
  let pickerChoice = $state<string | null>(null);
  let importBox = $state({ open: false, path: "", replaceAll: false });
  let settingsBox = $state({ open: false });
  let settingsDraft = $state<SettingsView>({
    globalHotkey: "Ctrl+Shift+Space",
    windowWidth: 800,
    windowHeight: 500,
    startMinimized: false,
    recentItemsCount: 20,
  });
  let menu = $state<{ x: number; y: number; entries: MenuEntry[] } | null>(null);
  /** 「⋯」から開いたメニューか。同じボタンをもう一度押したら閉じるために使う。 */
  let menuFromMore = $state(false);

  function ask(options: Partial<Prompt> & { title: string; ondone: (value: string) => void }) {
    prompt = { ...closedPrompt, ...options, open: true };
  }

  function confirmThen(title: string, body: string, onyes: () => void) {
    confirmBox = { open: true, title, body, onyes };
  }

  function pickShelf(title: string, allowRoot: boolean, onpick: (id: string | null) => void) {
    pickerChoice = allowRoot ? null : (shelves[0]?.id ?? null);
    picker = { open: true, title, allowRoot, onpick };
  }

  function closeMenu() {
    menu = null;
    menuFromMore = false;
  }

  // ------------------------------------------------------------ 読み込み

  function buildTree(list: ShelfView[]): ShelfNode[] {
    const nodes = new Map<string, ShelfNode>();
    for (const shelf of list) nodes.set(shelf.id, { shelf, children: [] });

    const roots: ShelfNode[] = [];
    for (const shelf of list) {
      const node = nodes.get(shelf.id)!;
      const parent = shelf.parentId ? nodes.get(shelf.parentId) : undefined;
      if (parent) parent.children.push(node);
      else roots.push(node);
    }
    return roots;
  }

  async function reloadShelves() {
    shelves = await ipc.loadShelves();
    if (!selectedShelfId || !shelves.some((s) => s.id === selectedShelfId)) {
      selectedShelfId = shelves[0]?.id ?? null;
    }
  }

  // 一覧を読み込む関数はステータスに触らない。
  // 件数は見出しに、空であることは空状態に出すので、直前の操作の結果を上書きしないようにする。

  async function loadCurrentShelf() {
    if (!selectedShelfId) {
      items = [];
      return;
    }
    items = await ipc.loadItems(selectedShelfId);
    if (!items.some((i) => i.id === selectedItemId)) selectedItemId = null;
    checkExistenceOf(items);
  }

  /** いま見ている一覧を作り直す */
  async function refreshView() {
    if (mode === "search") await runSearch();
    else if (mode === "recent") await showRecent();
    else if (mode === "missing") await showMissing();
    else await loadCurrentShelf();
  }

  async function showShelf(shelf: ShelfView) {
    // 検索や一覧を見ている途中で棚を選んだら、通常の表示に戻す
    // （doc/SPECIFICATION.md 第 12 節の 5 番）
    selectedShelfId = shelf.id;
    query = "";
    mode = "normal";
    await loadCurrentShelf();
  }

  async function runSearch() {
    const text = query.trim();
    if (text === "") {
      mode = "normal";
      await loadCurrentShelf();
      return;
    }
    mode = "search";
    items = await ipc.search(query);
    checkExistenceOf(items);
  }

  function onQueryInput() {
    window.clearTimeout(searchTimer);
    // 連続入力に対しては待ってから実行する（第 5.4 節）
    searchTimer = window.setTimeout(runSearch, SEARCH_DELAY_MS);
  }

  async function showRecent() {
    mode = "recent";
    query = "";
    items = await ipc.recentItems();
    checkExistenceOf(items);
  }

  async function showMissing() {
    mode = "missing";
    query = "";
    items = await ipc.missingItems();
    // この一覧はすべて欠損している。背景の確認が届けば、そちらで上書きされる。
    missing = new Set(items.map((i) => i.id));
  }

  // ------------------------------------------------------------ 起動

  async function launch(item: ItemView) {
    const outcome = await ipc.launch(item.id);
    status = outcome.kind === "failed" ? outcome.message : `開きました: ${item.displayName}`;
  }

  async function openParent(item: ItemView) {
    const message = await ipc.openParent(item.id);
    if (message) status = message;
  }

  // ------------------------------------------------------------ 棚の操作

  function newShelf(parent: ShelfView | null) {
    ask({
      title: parent ? `「${parent.name}」の中に棚を作る` : "棚を作る",
      label: "棚の名前",
      confirmLabel: "作る",
      ondone: async (name) => {
        const result = await ipc.createShelf(name, parent?.id ?? null);
        if (result.message) status = result.message;
        await reloadShelves();
        if (result.shelf) {
          selectedShelfId = result.shelf.id;
          mode = "normal";
          query = "";
          await loadCurrentShelf();
          status = `棚を作りました: ${result.shelf.name}`;
        }
      },
    });
  }

  function renameShelf(shelf: ShelfView) {
    ask({
      title: "棚の名前を変える",
      label: "棚の名前",
      value: shelf.name,
      confirmLabel: "変える",
      ondone: async (name) => {
        const result = await ipc.renameShelf(shelf.id, name);
        status = result.message ?? `名前を変えました: ${name}`;
        await reloadShelves();
      },
    });
  }

  function moveShelf(shelf: ShelfView) {
    pickShelf(`「${shelf.name}」の移動先`, true, async (parentId) => {
      const result = await ipc.moveShelf(shelf.id, parentId);
      if (result.message) status = result.message;
      else status = "棚を移動しました";
      await reloadShelves();
    });
  }

  function deleteShelf(shelf: ShelfView) {
    confirmThen(
      "棚を削除しますか",
      `「${shelf.name}」を、その中の棚と項目ごと削除します。参照先のファイルは消えません。`,
      async () => {
        const result = await ipc.deleteShelf(shelf.id);
        status = result.message ?? "削除しました";
        if (selectedShelfId === shelf.id) selectedShelfId = null;
        await reloadShelves();
        await refreshView();
      },
    );
  }

  async function togglePin(shelf: ShelfView) {
    const result = await ipc.togglePinShelf(shelf.id);
    if (result.message) status = result.message;
    else status = result.shelf?.isPinned ? "ピン留めしました" : "ピン留めを外しました";
    await reloadShelves();
  }

  /** 同じ階層の中で 1 つ動かす */
  async function nudgeShelf(shelf: ShelfView, step: number) {
    const siblings = shelves.filter((s) => s.parentId === shelf.parentId);
    const from = siblings.findIndex((s) => s.id === shelf.id);
    const to = from + step;
    if (from < 0 || to < 0 || to >= siblings.length) return;
    const ordered = [...siblings];
    const [moved] = ordered.splice(from, 1);
    ordered.splice(to, 0, moved);
    const result = await ipc.reorderShelves(ordered.map((s) => s.id));
    if (result.message) status = result.message;
    await reloadShelves();
  }

  // ------------------------------------------------------------ 項目の操作

  function addUrl() {
    if (!selectedShelfId) {
      status = "先に棚を選んでください。";
      return;
    }
    const shelfId = selectedShelfId;
    ask({
      title: "URL を追加する",
      label: "URL（http または https）",
      value: "https://",
      confirmLabel: "次へ",
      ondone: (url) => {
        ask({
          title: "表示名",
          label: "一覧に出す名前",
          value: "",
          allowEmpty: true,
          confirmLabel: "追加",
          ondone: async (name) => {
            const result = await ipc.addUrl(shelfId, url, name);
            status = result.message ?? `追加しました: ${result.item?.displayName}`;
            await refreshView();
          },
        });
      },
    });
  }

  function renameItem(item: ItemView) {
    ask({
      title: "表示名を変える",
      label: "一覧に出す名前",
      value: item.displayName,
      confirmLabel: "変える",
      ondone: async (name) => {
        const result = await ipc.renameItem(item.id, name);
        status = result.message ?? `名前を変えました: ${name}`;
        await refreshView();
      },
    });
  }

  function editMemo(item: ItemView) {
    ask({
      title: "メモ",
      label: `${item.displayName} のメモ`,
      value: item.memo ?? "",
      multiline: true,
      allowEmpty: true,
      confirmLabel: "保存",
      ondone: async (memo) => {
        const result = await ipc.updateItemMemo(item.id, memo);
        status = result.message ?? "メモを保存しました";
        await refreshView();
      },
    });
  }

  function moveItem(item: ItemView) {
    pickShelf(`「${item.displayName}」の移動先`, false, async (shelfId) => {
      if (!shelfId) return;
      const result = await ipc.moveItemToShelf(item.id, shelfId);
      status = result.message ?? "項目を移動しました";
      await refreshView();
    });
  }

  function removeItem(item: ItemView) {
    confirmThen(
      "項目を削除しますか",
      `「${item.displayName}」を棚から外します。参照先のファイルは消えません。`,
      async () => {
        const result = await ipc.removeItem(item.id);
        status = result.message ?? "削除しました";
        selectedItemId = null;
        await refreshView();
      },
    );
  }

  /** 一覧の中で 1 つ動かす。通常の表示のときだけ効く。 */
  async function nudgeItem(item: ItemView, step: number) {
    if (mode !== "normal") {
      status = "並び替えは棚の表示中に行えます。";
      return;
    }
    const from = items.findIndex((i) => i.id === item.id);
    const to = from + step;
    if (from < 0 || to < 0 || to >= items.length) return;
    const ordered = [...items];
    const [moved] = ordered.splice(from, 1);
    ordered.splice(to, 0, moved);
    items = ordered;
    const result = await ipc.reorderItems(ordered.map((i) => i.id));
    if (result.message) status = result.message;
  }

  /** ポインタでの並び替え（doc/ARCHITECTURE.md 第 9 節） */
  async function reorderByDrag(fromIndex: number, toIndex: number) {
    if (mode !== "normal") {
      status = "並び替えは棚の表示中に行えます。";
      return;
    }
    const ordered = [...items];
    const [moved] = ordered.splice(fromIndex, 1);
    ordered.splice(toIndex, 0, moved);
    items = ordered;
    const result = await ipc.reorderItems(ordered.map((i) => i.id));
    status = result.message ?? "並び替えました";
  }

  // ------------------------------------------------------------ データ

  async function exportData() {
    const stamp = new Date()
      .toISOString()
      .replace(/[-:]/g, "")
      .replace("T", "_")
      .slice(0, 15);
    const path = await saveFile({
      title: "Shelfy のデータを書き出す",
      defaultPath: `shelfy_export_${stamp}.json`,
      filters: [{ name: "JSON", extensions: ["json"] }],
    });
    if (!path) return;
    const result = await ipc.exportData(path);
    status = result.message ?? "書き出しました";
  }

  /** 取り込むファイルを選び、取り込み方をダイアログで選ばせる（doc/DESIGN.md 第 9 節） */
  async function importData() {
    const path = await openFile({
      title: "Shelfy のデータを取り込む",
      multiple: false,
      filters: [{ name: "JSON", extensions: ["json"] }],
    });
    if (typeof path !== "string") return;
    // 既定は、いまのデータを消さない「足す」
    importBox = { open: true, path, replaceAll: false };
  }

  async function runImport(path: string, replaceAll: boolean) {
    const result = await ipc.importData(path, replaceAll);
    status = result.message ?? "取り込みました";
    selectedShelfId = null;
    await reloadShelves();
    mode = "normal";
    query = "";
    await loadCurrentShelf();
  }

  function fileNameOf(path: string): string {
    return path.split(/[\\/]/).pop() ?? path;
  }

  function openSettings() {
    if (!settings) return;
    settingsDraft = { ...settings };
    settingsBox = { open: true };
  }

  async function saveSettings() {
    const result = await ipc.saveSettings(settingsDraft);
    status = result.message ?? "設定を保存しました";
    if (result.ok) {
      settings = { ...settingsDraft };
      settingsBox = { open: false };
    }
  }

  // ------------------------------------------------------------ メニュー

  /** タイトルバーの「⋯」。取り込み、書き出し、設定を入れる（doc/DESIGN.md 第 7 節）。 */
  function toggleMoreMenu() {
    if (menuFromMore) {
      closeMenu();
      return;
    }
    if (!moreButton) return;
    const rect = moreButton.getBoundingClientRect();
    menu = {
      // メニューの右端をボタンの右端に揃える。はみ出す分はメニューの側で寄せる。
      x: rect.right - 200,
      y: rect.bottom + 2,
      entries: [
        { kind: "action", label: "取り込む…", icon: ICON.import, run: importData },
        { kind: "action", label: "書き出す…", icon: ICON.export, run: exportData },
        { kind: "separator" },
        { kind: "action", label: "設定", icon: ICON.settings, run: openSettings },
      ],
    };
    menuFromMore = true;
  }

  function shelfMenu(event: MouseEvent, shelf: ShelfView) {
    event.preventDefault();
    const siblings = shelves.filter((s) => s.parentId === shelf.parentId);
    const index = siblings.findIndex((s) => s.id === shelf.id);
    menuFromMore = false;
    menu = {
      x: event.clientX,
      y: event.clientY,
      entries: [
        { kind: "action", label: "棚を作る", icon: ICON.add, run: () => newShelf(null) },
        {
          kind: "action",
          label: "この中に棚を作る",
          icon: ICON.folderAdd,
          run: () => newShelf(shelf),
        },
        { kind: "separator" },
        {
          kind: "action",
          label: shelf.isPinned ? "ピン留めを外す" : "ピン留めする",
          icon: ICON.pinned,
          run: () => togglePin(shelf),
        },
        { kind: "action", label: "移動…", icon: ICON.folder, run: () => moveShelf(shelf) },
        {
          kind: "action",
          label: "上へ",
          icon: ICON.up,
          disabled: index <= 0,
          run: () => nudgeShelf(shelf, -1),
        },
        {
          kind: "action",
          label: "下へ",
          icon: ICON.down,
          disabled: index < 0 || index >= siblings.length - 1,
          run: () => nudgeShelf(shelf, 1),
        },
        { kind: "separator" },
        { kind: "action", label: "名前を変える", icon: ICON.edit, run: () => renameShelf(shelf) },
        { kind: "action", label: "削除", icon: ICON.delete, run: () => deleteShelf(shelf) },
      ],
    };
  }

  function itemMenu(event: MouseEvent, item: ItemView) {
    event.preventDefault();
    selectedItemId = item.id;
    const index = items.findIndex((i) => i.id === item.id);
    menuFromMore = false;
    menu = {
      x: event.clientX,
      y: event.clientY,
      entries: [
        { kind: "action", label: "開く", icon: ICON.play, run: () => launch(item) },
        {
          kind: "action",
          label: "親フォルダを開く",
          icon: ICON.folder,
          disabled: item.kind === "url",
          run: () => openParent(item),
        },
        { kind: "separator" },
        { kind: "action", label: "名前を変える", icon: ICON.edit, run: () => renameItem(item) },
        { kind: "action", label: "メモ", icon: ICON.memo, run: () => editMemo(item) },
        { kind: "action", label: "別の棚へ移動…", icon: ICON.moveTo, run: () => moveItem(item) },
        {
          kind: "action",
          label: "上へ",
          icon: ICON.up,
          disabled: mode !== "normal" || index <= 0,
          run: () => nudgeItem(item, -1),
        },
        {
          kind: "action",
          label: "下へ",
          icon: ICON.down,
          disabled: mode !== "normal" || index >= items.length - 1,
          run: () => nudgeItem(item, 1),
        },
        { kind: "separator" },
        { kind: "action", label: "削除", icon: ICON.delete, run: () => removeItem(item) },
      ],
    };
  }

  // ------------------------------------------------------------ キー操作

  function moveSelection(step: number) {
    if (items.length === 0) return;
    const current = items.findIndex((i) => i.id === selectedItemId);
    const next = Math.min(Math.max(current + step, 0), items.length - 1);
    selectedItemId = items[current < 0 ? 0 : next].id;
  }

  const dialogOpen = $derived(
    prompt.open ||
      confirmBox.open ||
      picker.open ||
      importBox.open ||
      settingsBox.open ||
      menu !== null,
  );

  async function onKeydown(event: KeyboardEvent) {
    // 重ねて出している間は、下のキー操作を効かせない
    if (dialogOpen) return;

    if (event.ctrlKey && event.key.toLowerCase() === "n") {
      event.preventDefault();
      newShelf(null);
      return;
    }
    if (event.ctrlKey && event.key.toLowerCase() === "r") {
      event.preventDefault();
      await reloadShelves();
      await refreshView();
      return;
    }
    if (event.key === "Escape") {
      event.preventDefault();
      await ipc.hideWindow();
      return;
    }
    if (event.key === "ArrowDown") {
      event.preventDefault();
      moveSelection(1);
      return;
    }
    if (event.key === "ArrowUp") {
      event.preventDefault();
      moveSelection(-1);
      return;
    }
    if (event.key === "Enter" && selectedItem) {
      event.preventDefault();
      await launch(selectedItem);
      return;
    }
    if (event.key === "F2" && selectedItem) {
      event.preventDefault();
      renameItem(selectedItem);
      return;
    }
    if (event.key === "Delete" && selectedItem && document.activeElement !== searchBox) {
      event.preventDefault();
      removeItem(selectedItem);
    }
  }

  // ------------------------------------------------------------ 起動時

  onMount(async () => {
    // 存在確認の結果は、最初の一覧を読み込むより先に受け取れるようにしておく。
    // ローカルの確認はすぐ終わるので、後から購読すると最初の結果を取りこぼす。
    await onExistenceUpdated((entries) => {
      const next = new Set(missing);
      for (const entry of entries) {
        if (entry.exists) next.delete(entry.id);
        else next.add(entry.id);
      }
      missing = next;
    });

    const info = await ipc.startupInfo();
    readOnly = info.readOnly;
    settings = info.settings;
    // Mica が効いた環境でだけ背景を透かす。効いていない環境で透かすと下が透けて読めない。
    document.documentElement.classList.toggle("mica", info.mica);

    await reloadShelves();
    await loadCurrentShelf();
    loaded = true;
    if (info.notice) status = info.notice;

    // エクスプローラからのドロップ。
    // Tauri の仕組みで受けるため、画面の中では HTML5 のドラッグを使わない。
    await getCurrentWebview().onDragDropEvent(async (event) => {
      const payload = event.payload;
      if (payload.type === "enter") {
        dropping = payload.paths.length > 0 ? payload.paths.length : null;
        return;
      }
      if (payload.type === "leave") {
        dropping = null;
        return;
      }
      if (payload.type !== "drop") return;

      dropping = null;
      if (!selectedShelfId) {
        status = "先に棚を選んでください。";
        return;
      }
      const result = await ipc.addItems(selectedShelfId, payload.paths);
      await refreshView();
      status =
        result.skipped.length === 0
          ? `${result.added.length} 件を追加しました`
          : `${result.added.length} 件を追加、${result.skipped.length} 件は追加しませんでした（${result.skipped[0]}）`;
    });

    await onWindowShown(() => searchBox?.focus());
    await onHotkeyUnavailable((message) => (status = message));

    searchBox?.focus();
  });
</script>

<svelte:window onkeydown={onKeydown} />

<div class="app">
  <header class="titlebar" data-tauri-drag-region>
    <span class="brand" data-tauri-drag-region>
      <img src={appIcon} width="16" height="16" alt="" data-tauri-drag-region />Shelfy
    </span>
    <label class="search">
      <span class="icon" aria-hidden="true">{ICON.search}</span>
      <input
        bind:this={searchBox}
        bind:value={query}
        oninput={onQueryInput}
        type="text"
        aria-label="検索"
        placeholder="名前、パス、メモを検索（box: type: in:）"
        autocomplete="off"
        spellcheck="false"
      />
    </label>
    <span class="winbtns">
      <button
        bind:this={moreButton}
        class="winbtn"
        class:open={menuFromMore}
        title="メニュー"
        aria-haspopup="menu"
        aria-expanded={menuFromMore}
        onpointerdown={(e) => e.stopPropagation()}
        onclick={toggleMoreMenu}
      >
        <span class="icon">{ICON.more}</span>
      </button>
      <button class="winbtn minimize" title="隠す (Esc)" onclick={() => getCurrentWindow().hide()}>
        <span class="icon">{ICON.minimize}</span>
      </button>
    </span>
  </header>

  <main class="body">
    <nav class="pane tree" aria-label="棚">
      <div class="panehead">
        <span>棚</span>
        <button class="iconbtn" title="棚を作る (Ctrl+N)" onclick={() => newShelf(null)}>
          <span class="icon">{ICON.add}</span>
        </button>
      </div>
      <div class="scroll" oncontextmenu={(e) => e.preventDefault()} role="presentation">
        <ShelfTree
          nodes={tree}
          selectedId={mode === "normal" ? selectedShelfId : null}
          onselect={showShelf}
          oncontext={shelfMenu}
        />
      </div>
      <div class="views">
        <button
          class="navrow"
          class:selected={mode === "recent"}
          aria-current={mode === "recent" ? "true" : undefined}
          onclick={showRecent}
        >
          <span class="icon">{ICON.recent}</span><span class="label">最近使ったもの</span>
        </button>
        <button
          class="navrow"
          class:selected={mode === "missing"}
          aria-current={mode === "missing" ? "true" : undefined}
          onclick={showMissing}
        >
          <span class="icon">{ICON.missing}</span><span class="label">見つからない項目</span>
        </button>
      </div>
    </nav>

    <section class="pane content">
      {#if hasHeading}
        <div class="listhead">
          <span class="title">{title}</span>
          {#if loaded}<span class="count">{items.length} 件</span>{/if}
          {#if readOnly}
            <span class="badge">読み取り専用</span>
          {/if}
          <span class="grow"></span>
          {#if mode === "normal" && selectedShelf}
            <button class="btn" onclick={addUrl}>
              <span class="icon">{ICON.add}</span>URL を追加
            </button>
          {/if}
        </div>
      {/if}

      {#if items.length > 0}
        <ItemList
          {items}
          selectedId={selectedItemId}
          showShelfName={mode !== "normal"}
          {missing}
          reorderable={mode === "normal"}
          onselect={(item) => (selectedItemId = item.id)}
          onlaunch={launch}
          oncontext={itemMenu}
          onreorder={reorderByDrag}
        />
      {:else if !loaded}
        <div class="placeholder"></div>
      {:else if mode === "normal" && !selectedShelf}
        <EmptyState
          icon={ICON.shelf}
          title="棚がまだありません"
          body="棚を作ると、ファイルやフォルダや URL を置けます。"
        >
          <span class="acts">
            <button class="btn primary" onclick={() => newShelf(null)}>棚を作る</button>
            <kbd>Ctrl+N</kbd>
          </span>
        </EmptyState>
      {:else if mode === "normal"}
        <EmptyState
          icon={ICON.import}
          title="この棚は空です"
          body="エクスプローラーからファイルやフォルダをドロップすると、ここに並びます。"
        />
      {:else if mode === "search"}
        <EmptyState
          title={`「${query.trim()}」に一致する項目はありません`}
          body="名前、パス、メモ、棚の名前を探しました。先頭に次の書き方を付けると、範囲を変えられます。"
        >
          <dl class="prefixes">
            <dt>box:</dt>
            <dd>棚の名前で絞り込む</dd>
            <dt>type:</dt>
            <dd>file、folder、url のどれか</dd>
            <dt>in:</dt>
            <dd>その棚の中だけを探す</dd>
          </dl>
        </EmptyState>
      {:else if mode === "recent"}
        <EmptyState title="まだ開いた項目がありません" />
      {:else}
        <EmptyState title="見つからない項目はありません" />
      {/if}

      {#if dropping !== null}
        <!-- ドラッグ中の案内。ポインタは下の一覧に通す。 -->
        <div class="dropzone" aria-hidden="true">
          <span class="icon">{ICON.add}</span>
          {#if selectedShelf}
            <span class="say">「{selectedShelf.name}」に {dropping} 件を追加します</span>
            <span class="why">離すと追加されます。同じ参照がすでにあるものは飛ばします。</span>
          {:else}
            <span class="say">追加先の棚がありません</span>
            <span class="why">先に棚を作ってください。</span>
          {/if}
        </div>
      {/if}
    </section>
  </main>

  <footer class="status" role="status">{status}</footer>
</div>

{#if menu}
  <ContextMenu x={menu.x} y={menu.y} entries={menu.entries} onclose={closeMenu} />
{/if}

<Dialog
  open={prompt.open}
  title={prompt.title}
  confirmLabel={prompt.confirmLabel}
  canConfirm={prompt.allowEmpty || prompt.value.trim().length > 0}
  onconfirm={() => {
    const value = prompt.value;
    const done = prompt.ondone;
    prompt = { ...closedPrompt };
    done(value);
  }}
  oncancel={() => (prompt = { ...closedPrompt })}
>
  <label class="field">
    <span>{prompt.label}</span>
    {#if prompt.multiline}
      <textarea bind:value={prompt.value} rows="5"></textarea>
    {:else}
      <input type="text" bind:value={prompt.value} />
    {/if}
  </label>
</Dialog>

<Dialog
  open={confirmBox.open}
  title={confirmBox.title}
  confirmLabel="実行する"
  onconfirm={() => {
    const yes = confirmBox.onyes;
    confirmBox = { ...confirmBox, open: false };
    yes();
  }}
  oncancel={() => (confirmBox = { ...confirmBox, open: false })}
>
  <p class="message">{confirmBox.body}</p>
</Dialog>

<Dialog
  open={picker.open}
  title={picker.title}
  confirmLabel="移動する"
  canConfirm={picker.allowRoot || pickerChoice !== null}
  onconfirm={() => {
    const pick = picker.onpick;
    const choice = pickerChoice;
    picker = { ...picker, open: false };
    pick(choice);
  }}
  oncancel={() => (picker = { ...picker, open: false })}
>
  <div class="picker">
    {#if picker.allowRoot}
      <label class="choice">
        <input type="radio" bind:group={pickerChoice} value={null} />
        <span>いちばん上（親なし）</span>
      </label>
    {/if}
    {#each shelves as shelf (shelf.id)}
      <label class="choice">
        <input type="radio" bind:group={pickerChoice} value={shelf.id} />
        <span>{shelf.name}</span>
      </label>
    {/each}
  </div>
</Dialog>

<!-- 取り込み方はここで選ぶ。やめる、Esc、背景のクリックでは何も取り込まない。 -->
<Dialog
  open={importBox.open}
  title="データを取り込む"
  confirmLabel="取り込む"
  cancelLabel="やめる"
  onconfirm={() => {
    const { path, replaceAll } = importBox;
    importBox = { ...importBox, open: false };
    void runImport(path, replaceAll);
  }}
  oncancel={() => (importBox = { ...importBox, open: false })}
>
  <div class="file" title={importBox.path}>
    <span class="icon">{ICON.file}</span>{fileNameOf(importBox.path)}
  </div>
  <fieldset class="modes">
    <legend>取り込み方</legend>
    <label class="mode">
      <input type="radio" name="import-mode" bind:group={importBox.replaceAll} value={false} />
      <span class="what">足す</span>
      <span class="how">いまの棚と項目を残し、まだないものだけを加えます。</span>
    </label>
    <label class="mode">
      <input type="radio" name="import-mode" bind:group={importBox.replaceAll} value={true} />
      <span class="what">置き換える</span>
      <span class="how danger">
        <span class="icon">{ICON.missing}</span>
        <span>いまの棚と項目をすべて消してから取り込みます。元に戻せません。</span>
      </span>
    </label>
  </fieldset>
</Dialog>

<Dialog
  open={settingsBox.open}
  title="設定"
  confirmLabel="保存"
  onconfirm={saveSettings}
  oncancel={() => (settingsBox = { open: false })}
>
  <label class="field">
    <span>ホットキー</span>
    <input type="text" bind:value={settingsDraft.globalHotkey} placeholder="Ctrl+Shift+Space" />
  </label>
  <div class="row">
    <label class="field">
      <span>幅</span>
      <input type="number" min="400" max="4000" bind:value={settingsDraft.windowWidth} />
    </label>
    <label class="field">
      <span>高さ</span>
      <input type="number" min="300" max="4000" bind:value={settingsDraft.windowHeight} />
    </label>
    <label class="field">
      <span>最近の件数</span>
      <input type="number" min="1" max="200" bind:value={settingsDraft.recentItemsCount} />
    </label>
  </div>
  <label class="check">
    <input type="checkbox" bind:checked={settingsDraft.startMinimized} />
    <span>起動したときはウィンドウを出さない</span>
  </label>
</Dialog>

<style>
  .app {
    display: flex;
    flex-direction: column;
    height: 100%;
  }

  /* ------------------------------------------------------------ タイトルバー */

  .titlebar {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    height: var(--titlebar);
    padding-left: var(--space-3);
    flex: 0 0 auto;
  }

  /* 検索欄の左端を、一覧のペインの左端に揃える */
  .brand {
    flex: 0 0 auto;
    width: calc(var(--pane-width) + var(--space-2) * 2 - var(--space-3) * 2);
    display: flex;
    align-items: center;
    gap: var(--space-2);
    font-size: 12px;
    font-weight: 600;
    color: var(--fg-sec);
  }

  .brand img {
    display: block;
  }

  .search {
    display: flex;
    align-items: center;
    gap: 10px;
    flex: 1 1 auto;
    min-width: 0;
    max-width: 420px;
    height: var(--control);
    padding: 0 10px;
    border: 1px solid var(--stroke);
    border-radius: var(--radius-sm);
    background: var(--layer-solid);
    box-shadow: inset 0 -1px 0 var(--stroke-strong);
  }

  /* 枠の太さは変えず、下辺の線で示す */
  .search:focus-within {
    box-shadow: inset 0 -2px 0 var(--accent-line);
  }

  .search .icon {
    font-size: 12px;
    color: var(--fg-sec);
  }

  .search input {
    flex: 1 1 auto;
    min-width: 0;
    border: 0;
    background: transparent;
    outline: none;
  }

  .search input::placeholder {
    color: var(--fg-sec);
  }

  .winbtns {
    margin-left: auto;
    display: flex;
  }

  .winbtn {
    width: 44px;
    height: var(--titlebar);
    display: grid;
    place-items: center;
  }

  .winbtn .icon {
    font-size: var(--icon-control);
  }

  .winbtn.minimize {
    width: 46px;
  }

  .winbtn.minimize .icon {
    font-size: 10px;
  }

  .winbtn:hover,
  .winbtn.open {
    background: var(--hover);
  }

  .winbtn:active {
    background: var(--press);
  }

  .winbtn:focus-visible {
    outline-offset: -2px;
  }

  /* ------------------------------------------------------------ ペイン */

  .body {
    flex: 1 1 auto;
    display: grid;
    grid-template-columns: var(--pane-width) minmax(0, 1fr);
    gap: var(--space-2);
    padding: 0 var(--space-2) var(--space-2);
    min-height: 0;
  }

  .pane {
    position: relative;
    min-height: 0;
    overflow: hidden;
    background: var(--layer);
    border: 1px solid var(--stroke);
    border-radius: var(--radius-md);
  }

  .tree {
    display: flex;
    flex-direction: column;
  }

  .panehead {
    flex: 0 0 auto;
    height: var(--titlebar);
    display: flex;
    align-items: center;
    justify-content: space-between;
    padding: 0 6px 0 14px;
    font-size: 12px;
    font-weight: 600;
    color: var(--fg-sec);
  }

  .panehead .iconbtn {
    color: var(--fg);
  }

  .tree .scroll {
    flex: 1 1 auto;
    overflow-y: auto;
    display: flex;
    flex-direction: column;
    gap: 2px;
    padding: 0 6px 6px;
  }

  .views {
    flex: 0 0 auto;
    display: flex;
    flex-direction: column;
    gap: 2px;
    padding: 6px;
    border-top: 1px solid var(--stroke);
  }

  .content {
    display: flex;
    flex-direction: column;
  }

  .listhead {
    flex: 0 0 auto;
    min-height: 52px;
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 10px 10px var(--space-2) 18px;
  }

  .listhead .title {
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    font-family: var(--font-display);
    font-size: var(--text-title);
    font-weight: 600;
    line-height: 28px;
  }

  .count {
    flex: 0 0 auto;
    padding-top: 5px;
    font-size: var(--text-caption);
    color: var(--fg-sec);
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
  }

  .grow {
    flex: 1 1 auto;
  }

  .badge {
    flex: 0 0 auto;
    padding: 1px 6px;
    border-radius: 10px;
    background: var(--selected);
    font-size: var(--text-caption);
    color: var(--fg);
    white-space: nowrap;
  }

  .placeholder {
    flex: 1 1 auto;
  }

  .status {
    flex: 0 0 auto;
    display: flex;
    align-items: center;
    height: 28px;
    padding: 0 14px;
    border-top: 1px solid var(--stroke);
    font-size: var(--text-caption);
    color: var(--fg-sec);
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
  }

  /* ------------------------------------------------------------ 空状態とドロップ */

  .acts {
    display: flex;
    align-items: center;
    gap: var(--space-3);
  }

  kbd {
    padding: 1px 6px;
    border: 1px solid var(--stroke);
    border-radius: var(--radius-sm);
    background: var(--layer-solid);
    font-family: var(--font-ui);
    font-size: var(--text-caption);
    color: var(--fg-sec);
  }

  .prefixes {
    display: grid;
    grid-template-columns: auto 1fr;
    gap: var(--space-1) 14px;
    margin: 0;
    font-size: 12px;
  }

  .prefixes dt {
    color: var(--fg);
  }

  .prefixes dd {
    margin: 0;
    color: var(--fg-sec);
  }

  .dropzone {
    position: absolute;
    inset: var(--space-2);
    z-index: 5;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    gap: var(--space-2);
    border: 2px dashed var(--accent-line);
    border-radius: var(--radius-md);
    background: color-mix(in oklch, var(--layer-solid) 85%, transparent);
    pointer-events: none;
    animation: fade var(--dur-fast) var(--ease-out);
  }

  @keyframes fade {
    from {
      opacity: 0;
    }
  }

  .dropzone .icon {
    font-size: var(--text-title);
    color: var(--accent-line);
  }

  .dropzone .say {
    font-size: var(--text-subtitle);
    font-weight: 600;
  }

  .dropzone .why {
    font-size: 12px;
    color: var(--fg-sec);
  }

  /* ------------------------------------------------------------ ダイアログの中身 */

  .row {
    display: flex;
    gap: var(--space-2);
  }

  .row .field {
    flex: 1 1 0;
  }

  .check {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    font-size: 12px;
  }

  .message {
    margin: 0;
    line-height: 1.6;
  }

  .picker {
    max-height: 240px;
    overflow-y: auto;
    display: flex;
    flex-direction: column;
    gap: 2px;
  }

  .choice {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    min-height: var(--control);
    padding: 0 6px;
    border-radius: var(--radius-sm);
  }

  .choice:hover {
    background: var(--hover);
  }

  .file {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    font-size: 12px;
    color: var(--fg-sec);
    overflow-wrap: anywhere;
  }

  .file .icon {
    font-size: var(--icon-control);
  }

  .modes {
    display: flex;
    flex-direction: column;
    gap: var(--space-1);
    margin: 0;
    padding: 0;
    border: 0;
  }

  .modes legend {
    margin-bottom: var(--space-1);
    padding: 0;
    font-size: 12px;
    color: var(--fg-sec);
  }

  .mode {
    display: grid;
    grid-template-columns: 20px minmax(0, 1fr);
    gap: 2px var(--space-2);
    padding: var(--space-2) var(--space-2) var(--space-2) 6px;
    border-radius: var(--radius-sm);
  }

  .mode:hover {
    background: var(--hover);
  }

  .mode input {
    width: 16px;
    height: 16px;
    margin: 1px 0 0;
  }

  .what {
    font-weight: 600;
  }

  .how {
    grid-column: 2;
    font-size: 12px;
    line-height: 1.55;
    color: var(--fg-sec);
  }

  .how.danger {
    display: flex;
    align-items: baseline;
    gap: 6px;
    color: var(--warn);
  }

  .how.danger .icon {
    font-size: var(--text-caption);
  }
</style>
