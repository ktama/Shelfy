<script lang="ts">
  import type { Snippet } from "svelte";

  // 一覧が空のときに、理由と次の操作を一覧の中に出す（doc/DESIGN.md 第 8 節）

  type Props = {
    /** 先頭に置くグリフ。無ければ出さない。 */
    icon?: string;
    title: string;
    body?: string;
    children?: Snippet;
  };

  let { icon, title, body, children }: Props = $props();
</script>

<div class="empty" role="status">
  {#if icon}<span class="icon lead" aria-hidden="true">{icon}</span>{/if}
  <span class="say">{title}</span>
  {#if body}<span class="why">{body}</span>{/if}
  {#if children}<div class="more">{@render children()}</div>{/if}
</div>

<style>
  .empty {
    flex: 1 1 auto;
    display: flex;
    flex-direction: column;
    align-items: flex-start;
    justify-content: center;
    gap: var(--space-2);
    padding: 0 28px var(--space-6);
  }

  .lead {
    margin-bottom: var(--space-1);
    font-size: 28px;
    color: var(--fg-sec);
  }

  .say {
    font-size: var(--text-subtitle);
    font-weight: 600;
  }

  .why {
    max-width: 38ch;
    font-size: 12px;
    line-height: 1.6;
    color: var(--fg-sec);
  }

  .more {
    margin-top: var(--space-2);
  }
</style>
