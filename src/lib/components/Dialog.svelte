<script lang="ts">
  import type { Snippet } from "svelte";

  type Props = {
    open: boolean;
    title: string;
    /** 実行ボタンの文言 */
    confirmLabel?: string;
    /** 実行を押せるか */
    canConfirm?: boolean;
    onconfirm: () => void;
    oncancel: () => void;
    children: Snippet;
  };

  let {
    open,
    title,
    confirmLabel = "OK",
    canConfirm = true,
    onconfirm,
    oncancel,
    children,
  }: Props = $props();

  let panel: HTMLDivElement | null = $state(null);

  // 開いたら中の最初の入力へ移す
  $effect(() => {
    if (open && panel) {
      const target = panel.querySelector<HTMLElement>("input, textarea, button.primary");
      target?.focus();
      if (target instanceof HTMLInputElement) target.select();
    }
  });

  function onKeydown(event: KeyboardEvent) {
    // 別ウィンドウを開かず重ねて出すので、キーはここで受け止める
    event.stopPropagation();
    if (event.key === "Escape") {
      event.preventDefault();
      oncancel();
    }
    if (event.key === "Enter" && !(event.target instanceof HTMLTextAreaElement)) {
      event.preventDefault();
      if (canConfirm) onconfirm();
    }
  }
</script>

{#if open}
  <div
    class="scrim"
    role="presentation"
    onclick={(e) => {
      if (e.target === e.currentTarget) oncancel();
    }}
  >
    <div
      class="panel"
      bind:this={panel}
      role="dialog"
      aria-modal="true"
      aria-label={title}
      tabindex="-1"
      onkeydown={onKeydown}
    >
      <h2>{title}</h2>
      <div class="content">{@render children()}</div>
      <div class="actions">
        <button onclick={oncancel}>キャンセル</button>
        <button class="primary" disabled={!canConfirm} onclick={onconfirm}>{confirmLabel}</button>
      </div>
    </div>
  </div>
{/if}

<style>
  .scrim {
    position: fixed;
    inset: 0;
    display: flex;
    align-items: center;
    justify-content: center;
    background: rgba(0, 0, 0, 0.4);
    animation: fade 120ms ease-out;
  }

  @keyframes fade {
    from {
      opacity: 0;
    }
  }

  .panel {
    width: min(420px, calc(100% - 48px));
    padding: 20px;
    border: 1px solid var(--stroke);
    border-radius: var(--radius-md);
    background: var(--layer-solid);
    box-shadow: 0 8px 24px rgba(0, 0, 0, 0.3);
    outline: none;
  }

  h2 {
    margin: 0 0 12px;
    font-size: 15px;
    font-weight: 600;
  }

  .content {
    display: flex;
    flex-direction: column;
    gap: 10px;
    margin-bottom: 18px;
  }

  .actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
  }

  .actions button {
    min-width: 92px;
    height: 30px;
    padding: 0 12px;
    border: 1px solid var(--stroke);
    border-radius: var(--radius-sm);
    background: var(--layer);
  }

  .actions button:hover {
    background: var(--hover);
  }

  .actions .primary {
    border-color: transparent;
    background: var(--accent);
    color: #fff;
  }

  .actions .primary:disabled {
    opacity: 0.5;
  }
</style>
