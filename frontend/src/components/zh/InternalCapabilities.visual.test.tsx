// @vitest-environment node
import { afterAll, beforeAll, expect, it, vi } from "vitest";
import { chromium, type Browser } from "@playwright/test";
import { readFileSync, mkdirSync } from "node:fs";
import { resolve, dirname } from "node:path";
import { renderToStaticMarkup } from "react-dom/server";
import { MemoryRouter } from "react-router-dom";
import { I18nProvider } from "../../i18n/i18n";
import { ZHBtn, ZHField, ZHFormBody, ZHFormSection, ZHGrid, ZHToggle } from "./ZHForm";
import { ZhTextInput } from "./inputs";
import { ZHDataTable } from "./ZHDataTable";
import { ZHPageNotice } from "./ZHPageNotice";

const root = resolve(import.meta.dirname, "../../..");
const output = resolve(root, "../.local/internal-v1/visual");
function cssFile(file: string): string {
  return readFileSync(file, "utf8").replace(/@import\s+"([^"]+)";/g, (_, target: string) => cssFile(resolve(dirname(file), target)));
}
const css = cssFile(resolve(root, "src/index.css")) + cssFile(resolve(root, "src/components/zh/inputs/ZhInputs.css"));
const fonts = [...readFileSync(resolve(root, "index.html"), "utf8").matchAll(/<link[\s\S]*?>/g)]
  .map(m => m[0]).filter(tag => tag.includes("fonts.")).join("\n");
let browser: Browser;
beforeAll(async () => {
  vi.stubGlobal("localStorage", { getItem: () => null });
  mkdirSync(output, { recursive: true });
  browser = await chromium.launch({ headless: true });
});
afterAll(async () => { await browser?.close(); vi.unstubAllGlobals(); });

for (const width of [390, 1440]) {
  it(`ZH real: fuentes/iconos, formulario y tabla compacta a ${width}px`, async () => {
    const markup = renderToStaticMarkup(<I18nProvider><MemoryRouter><ZHFormBody standalone>
      <ZHFormSection title="Componentes ZH — validación interna">
        <ZHGrid cols={2}>
          <ZHField label="Empresa" required><ZhTextInput value="Empresa de prueba" readOnly /></ZHField>
          <ZHField label="Correo" error="Ingrese un correo válido"><ZhTextInput value="incompleto" readOnly /></ZHField>
        </ZHGrid>
        <ZHToggle value label="Usuario activo" description="Acceso habilitado" onChange={() => {}} />
        <ZHBtn variant="primary"><span className="material-symbols-outlined">save</span> Guardar</ZHBtn>
        <ZHBtn variant="secondary" disabled>Acción deshabilitada</ZHBtn>
        <ZHPageNotice variant="success" message="Configuración guardada" />
      </ZHFormSection>
      <ZHDataTable columns={[
        { key: "name", header: "Usuario", render: row => row.name },
        { key: "role", header: "Perfil", render: row => row.role },
      ]} rows={[{ id: "1", name: "Ana Pérez", role: "Consulta" }, { id: "2", name: "Luis López", role: "Operación" }]}
        rowKey={row => row.id} showRowNumber tableClassName="table--compact" />
    </ZHFormBody></MemoryRouter></I18nProvider>);
    const page = await browser.newPage({ viewport: { width, height: 900 } });
    try {
      await page.setContent(`<!doctype html><html><head><meta charset="utf-8">${fonts}<style>${css}</style></head><body>${markup}</body></html>`, { waitUntil: "networkidle" });
      const loaded = await page.evaluate(async () => {
        const inter = await document.fonts.load('400 14px "Inter"', "Empresa de prueba");
        const icons = await document.fonts.load('24px "Material Symbols Outlined"', "save");
        await document.fonts.ready;
        return { inter: inter.length, icons: icons.length, overflow: document.documentElement.scrollWidth > innerWidth };
      });
      expect(loaded.inter, "Inter real descargada").toBeGreaterThan(0);
      expect(loaded.icons, "Material Symbols real descargada").toBeGreaterThan(0);
      expect(loaded.overflow).toBe(false);
      expect(await page.locator("tbody tr").count()).toBe(2);
      const icon = await page.locator(".material-symbols-outlined").first().boundingBox();
      expect(icon!.width, "Ligadura dibujada como icono, no texto save").toBeLessThan(35);
      await page.screenshot({ path: resolve(output, `zh-components-${width}.png`), fullPage: true });
    } finally { await page.close(); }
  }, 60000);
}
