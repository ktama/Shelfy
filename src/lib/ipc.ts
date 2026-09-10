// バックエンドとの境界。ここだけが Tauri の API を知る。
// コマンドの一覧は doc/ARCHITECTURE.md 第 6.2 節に対応する。

import { invoke } from "@tauri-apps/api/core";
import { listen } from "@tauri-apps/api/event";

export type ItemKind = "file" | "folder" | "url";

export type ItemView = {
  id: string;
  shelfId: string;
  shelfName: string;
  kind: ItemKind;
  target: string;
  displayName: string;
  memo: string | null;
  sortOrder: number;
  lastAccessedAt: string | null;
};

export type ShelfView = {
  id: string;
  name: string;
  parentId: string | null;
  sortOrder: number;
  isPinned: boolean;
};

export type SettingsView = {
  globalHotkey: string;
  windowWidth: number;
  windowHeight: number;
  startMinimized: boolean;
  recentItemsCount: number;
};

export type StartupInfo = {
  storage: "fresh" | "loaded" | "recovered" | "empty" | "readOnly";
  notice: string | null;
  readOnly: boolean;
  settings: SettingsView;
};

export type LaunchOutcome =
  | { kind: "launched"; hide: boolean }
  | { kind: "failed"; message: string };

/** 更新系の返し方。message があれば利用者に見せる。 */
export type ShelfResult = { shelf: ShelfView | null; message: string | null };
export type ItemResult = { item: ItemView | null; message: string | null };
export type SimpleResult = { ok: boolean; message: string | null };
export type AddItemsResult = { added: ItemView[]; skipped: string[] };

export const ipc = {
  startupInfo: () => invoke<StartupInfo>("startup_info"),
  loadShelves: () => invoke<ShelfView[]>("load_shelves"),
  loadItems: (shelfId: string) => invoke<ItemView[]>("load_items", { shelfId }),
  search: (query: string) => invoke<ItemView[]>("search", { query }),
  recentItems: () => invoke<ItemView[]>("recent_items"),
  missingItems: () => invoke<ItemView[]>("missing_items"),
  launch: (itemId: string) => invoke<LaunchOutcome>("launch", { itemId }),
  openParent: (itemId: string) => invoke<string | null>("open_parent", { itemId }),
  hideWindow: () => invoke<void>("hide_window"),

  // 棚
  createShelf: (name: string, parentId: string | null) =>
    invoke<ShelfResult>("create_shelf", { name, parentId }),
  renameShelf: (shelfId: string, name: string) =>
    invoke<ShelfResult>("rename_shelf", { shelfId, name }),
  moveShelf: (shelfId: string, parentId: string | null) =>
    invoke<SimpleResult>("move_shelf", { shelfId, parentId }),
  deleteShelf: (shelfId: string) => invoke<SimpleResult>("delete_shelf", { shelfId }),
  togglePinShelf: (shelfId: string) => invoke<ShelfResult>("toggle_pin_shelf", { shelfId }),
  reorderShelves: (orderedIds: string[]) =>
    invoke<SimpleResult>("reorder_shelves", { orderedIds }),

  // 項目
  addItems: (shelfId: string, targets: string[]) =>
    invoke<AddItemsResult>("add_items", { shelfId, targets }),
  addUrl: (shelfId: string, url: string, displayName: string) =>
    invoke<ItemResult>("add_url", { shelfId, url, displayName }),
  removeItem: (itemId: string) => invoke<SimpleResult>("remove_item", { itemId }),
  renameItem: (itemId: string, name: string) =>
    invoke<ItemResult>("rename_item", { itemId, name }),
  updateItemMemo: (itemId: string, memo: string | null) =>
    invoke<ItemResult>("update_item_memo", { itemId, memo }),
  moveItemToShelf: (itemId: string, shelfId: string) =>
    invoke<SimpleResult>("move_item_to_shelf", { itemId, shelfId }),
  reorderItems: (orderedIds: string[]) => invoke<SimpleResult>("reorder_items", { orderedIds }),

  // データと設定
  exportData: (path: string) => invoke<SimpleResult>("export_data", { path }),
  importData: (path: string, replaceAll: boolean) =>
    invoke<SimpleResult>("import_data", { path, replaceAll }),
  saveSettings: (settings: SettingsView) => invoke<SimpleResult>("save_settings", { settings }),

  /** 存在確認を背景で頼む。結果は existence-updated で届く。 */
  checkExistence: (itemIds: string[]) => invoke<void>("check_existence", { itemIds }),
};

export type ExistenceEntry = { id: string; exists: boolean };

/// ホットキーやトレイからの表示をフロントへ伝える通知
export const onWindowShown = (handler: () => void) => listen("window-shown", handler);
export const onHotkeyUnavailable = (handler: (message: string) => void) =>
  listen<string>("hotkey-unavailable", (event) => handler(event.payload));
export const onExistenceUpdated = (handler: (entries: ExistenceEntry[]) => void) =>
  listen<ExistenceEntry[]>("existence-updated", (event) => handler(event.payload));
