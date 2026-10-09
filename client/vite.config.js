import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import path from "node:path";
import { fileURLToPath } from "node:url";

const root = path.dirname(fileURLToPath(import.meta.url));

function listenOnAllInterfaces() {
  return {
    name: "listen-on-all-interfaces",
    configureServer(server) {
      const httpServer = server.httpServer;
      if (!httpServer) return;
      const listen = httpServer.listen.bind(httpServer);
      httpServer.listen = (...args) => {
        const cb = typeof args.at(-1) === "function" ? args.pop() : undefined;
        const port = typeof args[0] === "number" ? args[0] : args[0]?.port ?? 5173;
        return listen({ port, host: "::", ipv6Only: false }, cb);
      };
    },
  };
}

export default defineConfig({
  root,
  plugins: [react(), listenOnAllInterfaces()],
  server: {
    port: 5173,
    host: "::",
    allowedHosts: [".trycloudflare.com"],
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
