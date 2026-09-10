import { defineConfig } from "vite";
import { svelte } from "@sveltejs/vite-plugin-svelte";

export default defineConfig({
  plugins: [svelte()],
  clearScreen: false,
  server: { port: 1420, strictPort: true },
  build: {
    target: "esnext",
    minify: "esbuild",
    // 1 つの HTML に束ね、分割読み込みを行わない（起動時の読み込み回数を減らす）
    rollupOptions: { output: { manualChunks: undefined } },
  },
});
