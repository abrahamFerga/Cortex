import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],

  build: {
    lib: {
      // Library entry: exports all public components and hooks.
      // The dev server continues to use index.html for local development.
      entry: "src/index.ts",
      name: "CortexUI",
      fileName: (format) => `cortex-ui.${format}.js`,
    },
    rollupOptions: {
      // Peer dependencies — consuming apps supply these.
      external: [
        "react",
        "react-dom",
        "react-router-dom",
        "@microsoft/signalr",
        "@tanstack/react-query",
      ],
      output: {
        globals: {
          react: "React",
          "react-dom": "ReactDOM",
          "react-router-dom": "ReactRouterDOM",
          "@microsoft/signalr": "signalR",
          "@tanstack/react-query": "ReactQuery",
        },
      },
    },
  },
});
