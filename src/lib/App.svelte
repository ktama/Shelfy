<script lang="ts">
  import { onMount } from "svelte";
  import { getCurrentWindow } from "@tauri-apps/api/window";
  import { getCurrentWebview } from "@tauri-apps/api/webview";
  import { open as openFile, save as saveFile } from "@tauri-apps/plugin-dialog";

  import { ipc, onExistenceUpdated, onHotkeyUnavailable, onWindowShown } from "./ipc";
  import type { ItemView, SettingsView, ShelfView } from "./ipc";
  import { ICON } from "./icons";
  import ShelfTree from "./components/ShelfTree.svelte";
  import type { ShelfNode } from "./components/ShelfTree.svelte";
  import ItemList from "./components/ItemList.svelte";
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
  /** 参照先が見つからない項目。背景での確認が届くたびに更新される。 */
  let missing = $state(new Set<string>());

  /** 一覧を出したあとに、参照先の確認を背景で頼む（第 7.5 節） */
  function checkExistenceOf(list: ItemView[]) {
    if (list.length === 0) return;
    void ipc.checkExistence(list.map((i) => i.id));
  }
  let settings = $state<SettingsView | null>(null);

  let searchBox: HTMLInputElement | null = $state(null);
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
          : (selectedShelf?.name ?? "項目"),
  );

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
  let settingsBox = $state({ open: false });
  let settingsDraft = $state<SettingsView>({
    globalHotkey: "Ctrl+Shift+Space",
    windowWidth: 800,
    windowHeight: 500,
    startMinimized: false,
    recentItemsCount: 20,
  });
  let menu = $state<{ x: number; y: number; entries: MenuEntry[] } | null>(null);

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

  async function loadCurrentShelf() {
    if (!selectedShelfId) {
      items = [];
      status = "棚がまだありません";
      return;
    }
    items = await ipc.loadItems(selectedShelfId);
    if (!items.some((i) => i.id === selectedItemId)) selectedItemId = null;
    status = items.length === 0 ? "この棚は空です" : `${items.length} 件`;
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
    status = `${items.length} 件が見つかりました`;
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
    status = items.length === 0 ? "まだ起動した項目がありません" : `${items.length} 件`;
    checkExistenceOf(items);
  }

  async function showMissing() {
    mode = "missing";
    query = "";
    items = await ipc.missingItems();
    // この一覧はすべて欠損している。背景の確認が届けば、そちらで上書きされる。
    missing = new Set(items.map((i) => i.id));
    status =
      items.length === 0 ? "見つからない項目はありません" : `${items.length} 件が見つかりません`;
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

  async function importData() {
    const path = await openFile({
      title: "Shelfy のデータを取り込む",
      multiple: false,
      filters: [{ name: "JSON", extensions: ["json"] }],
    });
    if (typeof path !== "string") return;

    confirmThen(
      "取り込み方を選んでください",
      "「置き換える」を選ぶと、いまのデータをすべて消してから取り込みます。キャンセルすると、いまのデータに足します。",
      async () => await runImport(path, true),
    );
    // 「キャンセル」でも取り込みたいので、確認の否定側に足す動作を割り当てる
    confirmBox = {
      ...confirmBox,
      onyes: async () => await runImport(path, true),
    };
    importFallback = () => runImport(path, false);
  }

  let importFallback: (() => void) | null = null;

  async function runImport(path: string, replaceAll: boolean) {
    const result = await ipc.importData(path, replaceAll);
    status = result.message ?? "取り込みました";
    selectedShelfId = null;
    await reloadShelves();
    mode = "normal";
    query = "";
    await loadCurrentShelf();
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

  function shelfMenu(event: MouseEvent, shelf: ShelfView) {
    event.preventDefault();
    const siblings = shelves.filter((s) => s.parentId === shelf.parentId);
    const index = siblings.findIndex((s) => s.id === shelf.id);
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
        { kind: "action", label: "移動...", icon: ICON.folder, run: () => moveShelf(shelf) },
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
        { kind: "action", label: "別の棚へ移動...", icon: ICON.moveTo, run: () => moveItem(item) },
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
    prompt.open || confirmBox.open || picker.open || settingsBox.open || menu !== null,
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
    const info = await ipc.startupInfo();
    readOnly = info.readOnly;
    settings = info.settings;
    if (info.notice) status = info.notice;

    await reloadShelves();
    await loadCurrentShelf();
    if (info.notice) status = info.notice;

    // エクスプローラからのドロップ。
    // Tauri の仕組みで受けるため、画面の中では HTML5 のドラッグを使わない。
    await getCurrentWebview().onDragDropEvent(async (event) => {
      if (event.payload.type !== "drop") return;
      if (!selectedShelfId) {
        status = "先に棚を選んでください。";
        return;
      }
      const paths = event.payload.paths;
      const result = await ipc.addItems(selectedShelfId, paths);
      await refreshView();
      status =
        result.skipped.length === 0
          ? `${result.added.length} 件を追加しました`
          : `${result.added.length} 件を追加、${result.skipped.length} 件は追加しませんでした（${result.skipped[0]}）`;
    });

    await onExistenceUpdated((entries) => {
      const next = new Set(missing);
      for (const entry of entries) {
        if (entry.exists) next.delete(entry.id);
        else next.add(entry.id);
      }
      missing = next;
    });

    await onWindowShown(() => searchBox?.focus());
    await onHotkeyUnavailable((message) => (status = message));

    searchBox?.focus();
  });
</script>

<svelte:window onkeydown={onKeydown} />

<div class="app">
  <header class="titlebar" data-tauri-drag-region>
    <span class="brand" data-tauri-drag-region>Shelfy</span>
    <label class="search">
      <span class="icon">{ICON.search}</span>
      <input
        bind:this={searchBox}
        bind:value={query}
        oninput={onQueryInput}
        type="text"
        placeholder="検索（box: type: in: で絞り込み）"
        autocomplete="off"
        spellcheck="false"
      />
    </label>
    <button class="winbtn icon" title="非表示 (Esc)" onclick={() => getCurrentWindow().hide()}>
      {ICON.minimize}
    </button>
  </header>

  <main class="body">
    <nav class="pane tree">
      <div class="treetools">
        <button title="棚を作る (Ctrl+N)" onclick={() => newShelf(null)}>
          <span class="icon">{ICON.add}</span>棚
        </button>
        <button
          title="選んだ棚の中に作る"
          disabled={!selectedShelf}
          onclick={() => selectedShelf && newShelf(selectedShelf)}
        >
          <span class="icon">{ICON.folderAdd}</span>子
        </button>
      </div>
      <div class="scroll" oncontextmenu={(e) => e.preventDefault()} role="presentation">
        <ShelfTree
          nodes={tree}
          selectedId={selectedShelfId}
          onselect={showShelf}
          oncontext={shelfMenu}
        />
      </div>
      <div class="views">
        <button class:active={mode === "recent"} onclick={showRecent} title="最近使ったもの">
          <span class="icon">{ICON.recent}</span>最近
        </button>
        <button class:active={mode === "missing"} onclick={showMissing} title="見つからない項目">
          <span class="icon">{ICON.missing}</span>欠損
        </button>
      </div>
      <div class="views">
        <button onclick={addUrl} title="URL を追加する">
          <span class="icon">{ICON.url}</span>URL
        </button>
        <button onclick={openSettings} title="設定">
          <span class="icon">{ICON.settings}</span>設定
        </button>
      </div>
      <div class="views">
        <button onclick={exportData} title="データを書き出す">
          <span class="icon">{ICON.export}</span>書出
        </button>
        <button onclick={importData} title="データを取り込む">
          <span class="icon">{ICON.import}</span>取込
        </button>
      </div>
    </nav>

    <section class="pane content">
      <div class="listhead">
        <span class="title">{title}</span>
        {#if readOnly}
          <span class="badge">読み取り専用</span>
        {/if}
      </div>
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
    </section>
  </main>

  <footer class="status">{status}</footer>
</div>

{#if menu}
  <ContextMenu x={menu.x} y={menu.y} entries={menu.entries} onclose={() => (menu = null)} />
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
    importFallback = null;
    yes();
  }}
  oncancel={() => {
    const fallback = importFallback;
    confirmBox = { ...confirmBox, open: false };
    importFallback = null;
    if (fallback) fallback();
  }}
>
  <p class="body">{confirmBox.body}</p>
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

  .titlebar {
    display: flex;
    align-items: center;
    gap: 12px;
    height: 44px;
    padding-left: 14px;
    flex: 0 0 auto;
  }

  .brand {
    font-size: 12px;
    font-weight: 600;
    color: var(--fg-sec);
  }

  .search {
    display: flex;
    align-items: center;
    gap: 8px;
    flex: 1 1 auto;
    max-width: 420px;
    height: 28px;
    padding: 0 10px;
    border: 1px solid var(--stroke);
    border-bottom-color: var(--fg-sec);
    border-radius: var(--radius-sm);
    background: var(--layer-solid);
  }

  .search:focus-within {
    border-bottom: 2px solid var(--accent);
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

  .winbtn {
    margin-left: auto;
    width: 46px;
    height: 44px;
    font-size: 10px;
  }

  .winbtn:hover {
    background: var(--hover);
  }

  .body {
    flex: 1 1 auto;
    display: grid;
    grid-template-columns: 220px 1fr;
    gap: 8px;
    padding: 0 8px 8px;
    min-height: 0;
  }

  .pane {
    background: var(--layer);
    border: 1px solid var(--stroke);
    border-radius: var(--radius-md);
    min-height: 0;
    overflow: hidden;
  }

  .tree {
    display: flex;
    flex-direction: column;
  }

  .treetools {
    display: flex;
    gap: 4px;
    padding: 6px 6px 0;
  }

  .tree .scroll {
    flex: 1 1 auto;
    overflow-y: auto;
    padding: 6px;
  }

  .views {
    flex: 0 0 auto;
    display: flex;
    gap: 4px;
    padding: 6px 6px 0;
  }

  .views:last-of-type {
    padding-bottom: 6px;
  }

  .treetools button,
  .views button {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 6px;
    flex: 1 1 0;
    height: 28px;
    padding: 0 8px;
    border-radius: var(--radius-sm);
    font-size: 12px;
  }

  .treetools button:hover,
  .views button:hover {
    background: var(--hover);
  }

  .treetools button:disabled {
    opacity: 0.4;
  }

  .views button.active {
    background: var(--selected);
  }

  .treetools .icon,
  .views .icon {
    font-size: 13px;
    color: var(--fg-sec);
  }

  .content {
    display: flex;
    flex-direction: column;
  }

  .listhead {
    display: flex;
    align-items: baseline;
    gap: 10px;
    padding: 10px 12px 6px;
  }

  .listhead .title {
    font-weight: 600;
  }

  .badge {
    padding: 1px 6px;
    border-radius: 10px;
    background: var(--selected);
    font-size: 11px;
    color: var(--fg-sec);
  }

  .status {
    flex: 0 0 auto;
    display: flex;
    align-items: center;
    height: 30px;
    padding: 0 14px;
    border-top: 1px solid var(--stroke);
    font-size: 11px;
    color: var(--fg-sec);
  }

  /* ダイアログの中身 */
  .field {
    display: flex;
    flex-direction: column;
    gap: 4px;
    font-size: 12px;
    color: var(--fg-sec);
  }

  .field input,
  .field textarea {
    padding: 6px 8px;
    border: 1px solid var(--stroke);
    border-bottom-color: var(--fg-sec);
    border-radius: var(--radius-sm);
    background: var(--bg-opaque);
    color: var(--fg);
    outline: none;
    resize: vertical;
  }

  .field input:focus,
  .field textarea:focus {
    border-bottom: 2px solid var(--accent);
  }

  .row {
    display: flex;
    gap: 8px;
  }

  .row .field {
    flex: 1 1 0;
  }

  .check {
    display: flex;
    align-items: center;
    gap: 8px;
    font-size: 12px;
  }

  .body {
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
    gap: 8px;
    padding: 5px 6px;
    border-radius: var(--radius-sm);
  }

  .choice:hover {
    background: var(--hover);
  }
</style>
