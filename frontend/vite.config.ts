import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

// https://vite.dev/config/
const apiProxy = {
  "/api": {
    target: "http://localhost:5003",
    changeOrigin: true,
  },
} as const;

const HEAVY_DOM_TESTS = ["src/modules/accounting/pages/AccountingPostingsPage.test.tsx"];

export default defineConfig({
  plugins: [react()],
  build: {
    // Rolldown + lightningcss minify breaks on legacy attribute selectors in zh-ui.css.
    cssMinify: false,
  },
  test: {
    setupFiles: ["src/test/setupPrecisionPolicy.ts", "src/test/setupI18n.ts"],
    // Tests jsdom con trabajo de render inevitable (p. ej. 100 selecciones reales sobre una tabla
    // de 101 filas): corren después del resto, sin contención de CPU, para que su tiempo sea estable.
    projects: [
      {
        extends: true,
        test: {
          name: "unit",
          include: ["src/**/*.test.{ts,tsx}"],
          exclude: ["e2e/**", "node_modules/**", ...HEAVY_DOM_TESTS],
        },
      },
      {
        extends: true,
        test: { name: "heavy-dom", include: HEAVY_DOM_TESTS, sequence: { groupOrder: 1 } },
      },
    ],
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
