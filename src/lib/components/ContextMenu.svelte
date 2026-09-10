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
  });

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
>
  {#each entries as entry, index (index)}
    {#if entry.kind === "separator"}
      <div class="separator"></div>
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
    min-width: 180px;
    padding: 4px;
    border: 1px solid var(--stroke);
    border-radius: var(--radius-md);
    background: var(--layer-solid);
    box-shadow: 0 6px 18px rgba(0, 0, 0, 0.28);
  }

  .entry {
    display: flex;
    align-items: center;
    gap: 10px;
    width: 100%;
    height: 30px;
    padding: 0 10px;
    border-radius: var(--radius-sm);
    text-align: left;
  }

  .entry:hover:not(:disabled) {
    background: var(--hover);
  }

  .entry:disabled {
    opacity: 0.4;
  }

  .entry .icon {
    width: 16px;
    font-size: 13px;
    color: var(--fg-sec);
  }

  .separator {
    height: 1px;
    margin: 4px 6px;
    background: var(--stroke);
  }
</style>
