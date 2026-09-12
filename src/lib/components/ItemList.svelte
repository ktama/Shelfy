<script lang="ts">
  import type { ItemView } from "../ipc";
  import { ICON, iconFor } from "../icons";

  type Props = {
    items: ItemView[];
    selectedId: string | null;
    /** 検索、最近、欠損では所属 Shelf を併記する */
    showShelfName: boolean;
    missing: Set<string>;
    /** 並び替えができるのは棚の表示中だけ */
    reorderable: boolean;
    onselect: (item: ItemView) => void;
    onlaunch: (item: ItemView) => void;
    oncontext: (event: MouseEvent, item: ItemView) => void;
    onreorder: (from: number, to: number) => void;
  };

  let {
    items,
    selectedId,
    showShelfName,
    missing,
    reorderable,
    onselect,
    onlaunch,
    oncontext,
    onreorder,
  }: Props = $props();

  // 見えている範囲だけを描く（doc/ARCHITECTURE.md 第 11 節）。
  // 1,000 件を素直に並べると描画に 73 ms かかり、切り替えが引っかかる。
  // 行の高さはメモの有無で 2 種類しかないので、累積和で正確な位置が出せる。
  // 値は doc/DESIGN.md 第 4 節。CSS の .row と .row.tall の高さと必ず揃える。
  const ROW_PLAIN = 44;
  const ROW_WITH_MEMO = 60;
  /** 上下に余分に描いておく行数。速く弾いたときの空白を防ぐ。 */
  const OVERSCAN = 6;

  let scrollTop = $state(0);
  let viewport = $state(400);

  const offsets = $derived.by(() => {
    const out = new Array<number>(items.length + 1);
    let acc = 0;
    for (let i = 0; i < items.length; i++) {
      out[i] = acc;
      acc += items[i].memo ? ROW_WITH_MEMO : ROW_PLAIN;
    }
    out[items.length] = acc;
    return out;
  });

  const totalHeight = $derived(offsets[items.length] ?? 0);

  /** `y` を含む行の番号を、二分探索で求める */
  function indexAt(y: number): number {
    let low = 0;
    let high = items.length;
    while (low < high) {
      const mid = (low + high) >> 1;
      if (offsets[mid + 1] <= y) low = mid + 1;
      else high = mid;
    }
    return Math.min(low, Math.max(0, items.length - 1));
  }

  const first = $derived(Math.max(0, indexAt(scrollTop) - OVERSCAN));
  const last = $derived(Math.min(items.length, indexAt(scrollTop + viewport) + 1 + OVERSCAN));
  const visible = $derived(items.slice(first, last));

  // Tauri のドラッグアンドドロップを有効にしていると、画面の中では
  // HTML5 の drag イベントが動かない。並び替えはポインタで自前に行う。
  // （doc/ARCHITECTURE.md 第 9 節）
  let dragFrom = $state<number | null>(null);
  let dragOver = $state<number | null>(null);
  let armed = $state(false);
  let startY = 0;

  function onPointerDown(event: PointerEvent, index: number) {
    if (event.button !== 0 || !reorderable) return;
    dragFrom = index;
    startY = event.clientY;
    armed = false;
  }

  function onPointerMove(event: PointerEvent) {
    if (dragFrom === null) return;
    if (!armed) {
      // 少し動かすまでは、ただのクリックとして扱う
      if (Math.abs(event.clientY - startY) < 4) return;
      armed = true;
      (event.currentTarget as HTMLElement).setPointerCapture(event.pointerId);
    }
    const element = document.elementFromPoint(event.clientX, event.clientY);
    const row = element?.closest<HTMLElement>(".row");
    dragOver = row?.dataset.index ? Number(row.dataset.index) : null;
  }

  function onPointerUp(event: PointerEvent) {
    const element = event.currentTarget as HTMLElement;
    if (element.hasPointerCapture(event.pointerId)) {
      element.releasePointerCapture(event.pointerId);
    }
    if (armed && dragFrom !== null && dragOver !== null && dragFrom !== dragOver) {
      onreorder(dragFrom, dragOver);
    }
    dragFrom = null;
    dragOver = null;
    armed = false;
  }

  function formatDate(value: string | null): string {
    if (!value) return "";
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return "";
    return date.toLocaleDateString();
  }
</script>

<div
  class="list"
  role="listbox"
  tabindex="-1"
  aria-label="項目"
  bind:clientHeight={viewport}
  onscroll={(e) => (scrollTop = e.currentTarget.scrollTop)}
  onpointermove={onPointerMove}
  onpointerup={onPointerUp}
  onpointercancel={onPointerUp}
>
  <div class="canvas" style="height: {totalHeight}px">
    {#each visible as item, offset (item.id)}
      {@const index = first + offset}
      <div
        class="row"
        class:selected={item.id === selectedId}
        class:dragging={armed && dragFrom === index}
        class:over={armed && dragOver === index && dragFrom !== index}
        class:tall={!!item.memo}
        data-index={index}
        style="top: {offsets[index]}px"
        role="option"
        aria-selected={item.id === selectedId}
        aria-posinset={index + 1}
        aria-setsize={items.length}
        tabindex="-1"
        title={item.target}
        onpointerdown={(e) => onPointerDown(e, index)}
        onclick={() => onselect(item)}
        ondblclick={() => onlaunch(item)}
        oncontextmenu={(e) => oncontext(e, item)}
        onkeydown={() => {}}
      >
        <span class="icon kind">{iconFor(item.kind)}</span>
        <span class="text">
          <span class="name">
            <span class="display">{item.displayName}</span>
            {#if missing.has(item.id)}
              <!-- 色だけに頼らず、アイコンと文言を並べる（doc/DESIGN.md 第 2 節） -->
              <span class="missing"><span class="icon">{ICON.missing}</span>見つかりません</span>
            {/if}
          </span>
          <span class="sub">
            {#if showShelfName}<span class="shelf">{item.shelfName}</span>{/if}
            <span class="target">{item.target}</span>
          </span>
          {#if item.memo}
            <span class="memo"><span class="icon">{ICON.memo}</span>{item.memo}</span>
          {/if}
        </span>
        <span class="date">{formatDate(item.lastAccessedAt)}</span>
      </div>
    {/each}
  </div>
</div>

<style>
  .list {
    flex: 1 1 auto;
    overflow-y: auto;
    padding: 0 var(--space-2) var(--space-2);
    outline: none;
  }

  .canvas {
    position: relative;
  }

  .row {
    position: absolute;
    left: 0;
    right: 0;
    /* 高さは 2 種類だけにして、位置の計算を正確に保つ */
    height: 44px;
    display: grid;
    grid-template-columns: var(--icon-kind) minmax(0, 1fr) auto;
    align-items: center;
    gap: var(--space-3);
    padding: 0 var(--space-3);
    border-radius: var(--radius-sm);
    cursor: default;
    touch-action: none;
  }

  .row.tall {
    height: 60px;
  }

  .row:hover {
    background: var(--hover);
  }

  .row.selected {
    background: var(--selected);
  }

  .row.dragging {
    opacity: 0.5;
  }

  .row.over {
    box-shadow: inset 0 2px 0 var(--accent-line);
  }

  .kind {
    font-size: var(--icon-kind);
    color: var(--fg-sec);
  }

  .text {
    display: flex;
    flex-direction: column;
    gap: 1px;
    min-width: 0;
  }

  .name {
    display: flex;
    align-items: center;
    gap: var(--space-2);
    overflow: hidden;
    white-space: nowrap;
  }

  .display {
    overflow: hidden;
    text-overflow: ellipsis;
  }

  .missing {
    flex: 0 0 auto;
    display: inline-flex;
    align-items: center;
    gap: var(--space-1);
    font-size: var(--text-caption);
    color: var(--warn);
  }

  .missing .icon {
    font-size: var(--text-caption);
  }

  .sub,
  .memo {
    display: block;
    font-size: var(--text-caption);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .sub {
    color: var(--fg-sec);
  }

  /* メモは利用者が書いた文なので、パスより濃くする */
  .memo {
    color: var(--fg);
  }

  .memo .icon {
    margin-right: 5px;
    font-size: 10px;
    color: var(--fg-sec);
    vertical-align: -1px;
  }

  .shelf {
    margin-right: 10px;
    color: var(--accent-text);
  }

  /* 行ごとに別の格子なので、幅を決めて右端と桁の位置を行をまたいで揃える */
  .date {
    min-width: 7em;
    text-align: right;
    font-size: var(--text-caption);
    color: var(--fg-sec);
    font-variant-numeric: tabular-nums;
  }
</style>
