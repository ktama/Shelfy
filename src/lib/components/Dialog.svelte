<script lang="ts">
  import type { Snippet } from "svelte";

  type Props = {
    open: boolean;
    title: string;
    /** 実行ボタンの文言 */
    confirmLabel?: string;
    /** 取り消しボタンの文言 */
    cancelLabel?: string;
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
    cancelLabel = "キャンセル",
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
      // 文字の入力欄だけ、中身を選んでおく
      if (target instanceof HTMLInputElement && (target.type === "text" || target.type === "number")) {
        target.select();
      }
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
        <button class="btn cancel" onclick={oncancel}>{cancelLabel}</button>
        <button class="btn primary" disabled={!canConfirm} onclick={onconfirm}>{confirmLabel}</button>
      </div>
    </div>
  </div>
{/if}

<style>
  .scrim {
    position: fixed;
    inset: 0;
    z-index: 20;
    display: grid;
    place-items: center;
    background: var(--scrim);
    animation: fade var(--dur-fast) var(--ease-out);
  }

  @keyframes fade {
    from {
      opacity: 0;
    }
  }

  .panel {
    width: min(460px, calc(100% - 48px));
    padding: var(--space-5);
    border: 1px solid var(--stroke);
    border-radius: var(--radius-md);
    background: var(--layer-solid);
    box-shadow: var(--elevation);
    outline: none;
  }

  h2 {
    margin: 0 0 var(--space-3);
    font-size: var(--text-subtitle);
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
    gap: var(--space-2);
  }

  .cancel {
    min-width: 92px;
  }
</style>
