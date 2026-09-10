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
  const ROW_PLAIN = 46;
  const ROW_WITH_MEMO = 62;
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
            {item.displayName}
            {#if missing.has(item.id)}
              <span class="icon warn" title="参照先が見つかりません">{ICON.missing}</span>
            {/if}
          </span>
          <span class="sub">
            {#if showShelfName}<span class="shelf">{item.shelfName}</span>{/if}
            <span class="target">{item.target}</span>
          </span>
          {#if item.memo}
            <span class="memo">{item.memo}</span>
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
    padding: 4px 8px 8px;
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
    height: 46px;
    display: grid;
    grid-template-columns: 24px 1fr auto;
    align-items: center;
    gap: 10px;
    padding: 0 10px;
    border-radius: var(--radius-sm);
    cursor: default;
    touch-action: none;
  }

  .row.tall {
    height: 62px;
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
    box-shadow: inset 0 2px 0 var(--accent);
  }

  .kind {
    font-size: 16px;
    color: var(--fg-sec);
  }

  .text {
    display: block;
    min-width: 0;
  }

  .name {
    display: flex;
    align-items: center;
    gap: 6px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .warn {
    font-size: 12px;
    color: var(--warn);
  }

  .sub,
  .memo {
    display: block;
    font-size: 11px;
    color: var(--fg-sec);
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .shelf {
    padding: 0 6px 0 0;
    color: var(--accent);
  }

  .date {
    font-size: 11px;
    color: var(--fg-sec);
    font-variant-numeric: tabular-nums;
  }
</style>
