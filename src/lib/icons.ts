// Segoe Fluent Icons のグリフ。
// Windows 11 に標準搭載されたフォントを使うため、アイコンを同梱しない。
// 対応は doc/UI_DESIGN.md 第 4 節を引き継ぐ。

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
