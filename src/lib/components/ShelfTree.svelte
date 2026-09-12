<script lang="ts">
  import type { ShelfView } from "../ipc";
  import { ICON } from "../icons";
  import Self from "./ShelfTree.svelte";

  export type ShelfNode = { shelf: ShelfView; children: ShelfNode[] };

  type Props = {
    nodes: ShelfNode[];
    /** 選択の印を付ける棚。通常の表示以外では null を渡して印を外す。 */
    selectedId: string | null;
    depth?: number;
    onselect: (shelf: ShelfView) => void;
    oncontext: (event: MouseEvent, shelf: ShelfView) => void;
  };

  let { nodes, selectedId, depth = 0, onselect, oncontext }: Props = $props();
</script>

{#each nodes as node (node.shelf.id)}
  <button
    class="navrow"
    class:selected={node.shelf.id === selectedId}
    style="--depth: {depth}"
    title={node.shelf.name}
    aria-current={node.shelf.id === selectedId ? "true" : undefined}
    onclick={() => onselect(node.shelf)}
    oncontextmenu={(e) => oncontext(e, node.shelf)}
  >
    <span class="icon">{node.shelf.isPinned ? ICON.pinned : ICON.shelf}</span>
    <span class="label">{node.shelf.name}</span>
  </button>
  {#if node.children.length > 0}
    <!-- 階層は parentId から組み立てる。Shelf の入れ子はそのまま入れ子で描く。 -->
    <Self nodes={node.children} {selectedId} depth={depth + 1} {onselect} {oncontext} />
  {/if}
{/each}
