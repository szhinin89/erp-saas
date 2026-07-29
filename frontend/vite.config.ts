import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

// https://vite.dev/config/
const apiProxy = {
  "/api": {
    target: "http://localhost:5003",
    changeOrigin: true,
  },
} as const;

export default defineConfig({
  plugins: [react()],
  build: {
    // Rolldown + lightningcss minify breaks on legacy attribute selectors in zh-ui.css.
    cssMinify: false,
  },
  test: {
    include: ["src/**/*.test.{ts,tsx}"],
    exclude: ["e2e/**", "node_modules/**"],
  },
  server: {
    // Mismo origen que el front (5173…): evita CORS al llamar a /api/* en desarrollo.
    proxy: { ...apiProxy },
  },
  // npm run preview: mismo patrón que dev (origen 4173 → API 5003).
  preview: {
    proxy: { ...apiProxy },
  },
});
