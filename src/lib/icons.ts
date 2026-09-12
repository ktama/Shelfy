// Segoe Fluent Icons のグリフ。
// Windows 11 に標準搭載されたフォントを使うため、アイコンを同梱しない。
// 私用領域の文字は目で見分けられないので、コードポイントで書く。
// 大きさと色の決まりは doc/DESIGN.md 第 10 節にある。

import type { ItemKind } from "./ipc";

export const ICON = {
  shelf: "", // Folder
  pinned: "", // Pin
  file: "", // Document
  folder: "", // FolderOpen
  url: "", // Globe
  recent: "", // History
  missing: "", // Warning
  search: "", // Search
  minimize: "", // ChromeMinimize
  more: "", // More
  chevronRight: "",
  chevronDown: "",
  add: "",
  folderAdd: "",
  edit: "",
  delete: "",
  memo: "",
  moveTo: "",
  play: "",
  up: "",
  down: "",
  settings: "",
  export: "",
  import: "",
} as const;

export function iconFor(kind: ItemKind): string {
  switch (kind) {
    case "folder":
      return ICON.folder;
    case "url":
      return ICON.url;
    default:
      return ICON.file;
  }
}
