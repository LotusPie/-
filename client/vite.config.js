import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.dirname(fileURLToPath(import.meta.url));

export default defineConfig({
  root,
  plugins: [react()],
  server: {
    port: 5173,
    host: "::",
    proxy: {
      "/ws": {
        target: "http://127.0.0.1:3001",
        ws: true,
      },
    },
  },
  build: {
    outDir: path.join(root, "dist"),
    emptyOutDir: true,
  },
});
