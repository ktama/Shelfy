<script lang="ts">
  export type MenuEntry =
    | { kind: "separator" }
    | { kind: "action"; label: string; icon?: string; disabled?: boolean; run: () => void };

  type Props = {
    x: number;
    y: number;
    entries: MenuEntry[];
    onclose: () => void;
  };

  let { x, y, entries, onclose }: Props = $props();

  let menu: HTMLDivElement | null = $state(null);
  let size = $state({ width: 0, height: 0 });

  // 画面からはみ出さない位置へ寄せる
  const position = $derived({
    left: Math.max(4, Math.min(x, window.innerWidth - size.width - 8)),
    top: Math.max(4, Math.min(y, window.innerHeight - size.height - 8)),
  });

  $effect(() => {
    if (!menu) return;
    const rect = menu.getBoundingClientRect();
    size = { width: rect.width, height: rect.height };
    // キーボードでも選べるよう、最初の項目にフォーカスを移す
    enabledEntries()[0]?.focus();
  });

  function enabledEntries(): HTMLButtonElement[] {
    return menu ? [...menu.querySelectorAll<HTMLButtonElement>("button.entry:not(:disabled)")] : [];
  }

  function onKeydown(event: KeyboardEvent) {
    if (event.key !== "ArrowDown" && event.key !== "ArrowUp") return;
    event.preventDefault();
    const list = enabledEntries();
    if (list.length === 0) return;
    const current = list.indexOf(document.activeElement as HTMLButtonElement);
    const step = event.key === "ArrowDown" ? 1 : -1;
    list[(current + step + list.length) % list.length].focus();
  }

  function choose(entry: MenuEntry) {
    if (entry.kind !== "action" || entry.disabled) return;
    onclose();
    entry.run();
  }
</script>

<svelte:window
  onpointerdown={onclose}
  onkeydown={(e) => {
    if (e.key === "Escape") onclose();
  }}
/>

<div
  class="menu"
  bind:this={menu}
  role="menu"
  tabindex="-1"
  style="left: {position.left}px; top: {position.top}px"
  onpointerdown={(e) => e.stopPropagation()}
  onkeydown={onKeydown}
>
  {#each entries as entry, index (index)}
    {#if entry.kind === "separator"}
      <div class="separator" role="separator"></div>
    {:else}
      <button
        class="entry"
        role="menuitem"
        disabled={entry.disabled}
        onclick={() => choose(entry)}
      >
        <span class="icon">{entry.icon ?? ""}</span>
        <span>{entry.label}</span>
      </button>
    {/if}
  {/each}
</div>

<style>
  .menu {
    position: fixed;
    z-index: 10;
    min-width: 200px;
    padding: var(--space-1);
    border: 1px solid var(--stroke);
    border-radius: var(--radius-md);
    background: var(--layer-solid);
    box-shadow: var(--elevation);
    outline: none;
    animation: fade var(--dur-fast) var(--ease-out);
  }

  @keyframes fade {
    from {
      opacity: 0;
    }
  }

  .entry {
    display: flex;
    align-items: center;
    gap: var(--space-3);
    width: 100%;
    height: var(--control);
    padding: 0 10px;
    border-radius: var(--radius-sm);
    text-align: left;
    white-space: nowrap;
  }

  .entry:hover:not(:disabled) {
    background: var(--hover);
  }

  .entry:active:not(:disabled) {
    background: var(--press);
  }

  .entry:disabled {
    opacity: 0.45;
  }

  .entry .icon {
    width: var(--icon-kind);
    font-size: var(--icon-control);
    color: var(--fg-sec);
  }

  .separator {
    height: 1px;
    margin: var(--space-1) 6px;
    background: var(--stroke);
  }
</style>
