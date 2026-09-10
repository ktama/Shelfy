<script lang="ts">
  import type { ShelfView } from "../ipc";
  import { ICON } from "../icons";
  import Self from "./ShelfTree.svelte";

  export type ShelfNode = { shelf: ShelfView; children: ShelfNode[] };

  type Props = {
    nodes: ShelfNode[];
    selectedId: string | null;
    depth?: number;
    onselect: (shelf: ShelfView) => void;
    oncontext: (event: MouseEvent, shelf: ShelfView) => void;
  };

  let { nodes, selectedId, depth = 0, onselect, oncontext }: Props = $props();
</script>

{#each nodes as node (node.shelf.id)}
  <button
    class="node"
    class:selected={node.shelf.id === selectedId}
    style="padding-left: {8 + depth * 16}px"
    title={node.shelf.name}
    onclick={() => onselect(node.shelf)}
    oncontextmenu={(e) => oncontext(e, node.shelf)}
  >
    <span class="icon">{node.shelf.isPinned ? ICON.pinned : ICON.shelf}</span>
    <span class="name">{node.shelf.name}</span>
  </button>
  {#if node.children.length > 0}
    <!-- 階層は parentId から組み立てる。Shelf の入れ子はそのまま入れ子で描く。 -->
    <Self nodes={node.children} {selectedId} depth={depth + 1} {onselect} {oncontext} />
  {/if}
{/each}

<style>
  .node {
    display: flex;
    align-items: center;
    gap: 8px;
    width: 100%;
    height: 30px;
    padding-right: 8px;
    border-radius: var(--radius-sm);
    text-align: left;
    white-space: nowrap;
    overflow: hidden;
  }

  .node:hover {
    background: var(--hover);
  }

  .node.selected {
    background: var(--selected);
  }

  .node .icon {
    flex: 0 0 auto;
    font-size: 14px;
    color: var(--fg-sec);
  }

  .name {
    overflow: hidden;
    text-overflow: ellipsis;
  }
</style>
