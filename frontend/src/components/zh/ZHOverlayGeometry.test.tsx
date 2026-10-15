// @vitest-environment node
import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { chromium, type Browser } from "@playwright/test";
import { readFileSync } from "node:fs";
import { renderToStaticMarkup } from "react-dom/server";
import { ZHModal } from "./ZHModal";
import { ZHDrawer } from "./ZHDrawer";
import { ZHConfirmModal, ZHPromptModal } from "./ZHConfirmModal";
import { ZHBtn } from "./ZHForm";

const key = "1234567890".repeat(4) + "123456789";
const email = "cliente".repeat(16) + "@example.com";
const uuid = "12345678-1234-1234-1234-123456789012";
const content = (
  <>
    <dl className="zh-description-grid">
      <dt>Clave SRI</dt>
      <dd className="zh-code-value">{key}</dd>
      <dt>Email</dt>
      <dd>{email}</dd>
      <dt>UUID</dt>
      <dd>{uuid}</dd>
      <dd className="zh-description-grid__full-row" role="alert">
        {("Aviso de diferencia de redondeo " + email + key + " ").repeat(4)}
      </dd>
    </dl>
    <div className="zh-grid zh-grid--2">
      <div>{email}</div>
      <div className="zh-col-span-2">{key}</div>
    </div>
    <div className="zh-summary-grid">
      <div>
        <span className="zh-summary-grid__value">{email}</span>
      </div>
    </div>
    <span className="zh-money-value">$ 1,234.56</span>
    <span className="zh-code-value zh-code-value--document">
      001-001-000000001
    </span>
    <div className="table-scroll">
      <table style={{ minWidth: 1600 }}>
        <tbody>
          <tr>
            <td>{key}</td>
            <td>{email}</td>
          </tr>
        </tbody>
      </table>
    </div>
    {Array.from({ length: 50 }, (_, i) => (
      <p key={i}>Contenido vertical extenso {i}</p>
    ))}
    <p data-end>Fin accesible</p>
  </>
);
const footer = [
  "Imprimir tirilla",
  "Imprimir RIDE",
  "Descargar PDF",
  "Descargar XML",
  "Nueva venta",
].map((label) => <ZHBtn key={label}>{label}</ZHBtn>);
const css = ["design-tokens.css", "zh-ui.css"]
  .map((file) =>
    readFileSync(new URL(`../../styles/${file}`, import.meta.url), "utf8"),
  )
  .join("\n");
let browser: Browser;
beforeAll(async () => {
  browser = await chromium.launch({ headless: true });
});
afterAll(async () => {
  await browser?.close();
});

describe.each([320, 375, 768, 1280])("overlay geometry at %i px", (width) => {
  it.each(["modal", "full", "drawer", "confirm", "prompt"])(
    "%s keeps content and actions accessible",
    async (kind) => {
      const page = await browser.newPage({ viewport: { width, height: 720 } });
      try {
        const title = `Documento ${email}`;
        const noop = () => {};
        const overlay =
          kind === "drawer" ? (
            <ZHDrawer open title={title} onClose={noop} footer={footer}>
              {content}
            </ZHDrawer>
          ) : kind === "confirm" ? (
            <ZHConfirmModal
              open
              title={title}
              message={content}
              onConfirm={noop}
              onCancel={noop}
            />
          ) : kind === "prompt" ? (
            <ZHPromptModal
              open
              title={title}
              message={(email + " ").repeat(80)}
              label="Motivo"
              onConfirm={noop}
              onCancel={noop}
            />
          ) : (
            <ZHModal
              open
              size={kind === "full" ? "full" : "sm"}
              title={title}
              onClose={noop}
              footer={footer}
            >
              {content}
            </ZHModal>
          );
        await page.setContent(
          `<style>*{box-sizing:border-box;margin:0;padding:0} body{font-family:Arial} ${css} *{animation:none!important}</style>${renderToStaticMarkup(overlay)}`,
        );
        const result = await page.evaluate(() => {
          const panel = document.querySelector<HTMLElement>(
            ".zh-modal,.zh-drawer",
          )!;
          const body = panel.querySelector<HTMLElement>(
            ".zh-modal-body,.zh-drawer-body,.zh-confirm-body",
          )!;
          const footer = panel.querySelector<HTMLElement>(
            ".zh-modal-footer,.zh-drawer-footer",
          )!;
          const rect = panel.getBoundingClientRect();
          const actions = [...footer.querySelectorAll("button")].map(
            (button) => {
              const r = button.getBoundingClientRect();
              return (
                r.left >= rect.left &&
                r.right <= rect.right + 1 &&
                r.top >= rect.top &&
                r.bottom <= rect.bottom + 1
              );
            },
          );
          body.scrollTop = body.scrollHeight;
          const end = body.querySelector("[data-end]")?.getBoundingClientRect();
          const table = body.querySelector<HTMLElement>(".table-scroll");
          if (table) table.scrollLeft = 100;
          return {
            viewport: document.documentElement.scrollWidth <= innerWidth,
            panel:
              rect.left >= 0 &&
              rect.right <= innerWidth + 1 &&
              rect.top >= 0 &&
              rect.bottom <= innerHeight + 1,
            horizontal:
              body.scrollWidth <= body.clientWidth + 1 &&
              footer.scrollWidth <= footer.clientWidth + 1,
            actions,
            count: actions.length,
            vertical:
              body.scrollTop > 0 &&
              (!end || end.bottom <= body.getBoundingClientRect().bottom + 1),
            table:
              !table ||
              (table.scrollWidth > table.clientWidth && table.scrollLeft > 0),
            nowrap: [
              ...body.querySelectorAll(
                ".zh-money-value,.zh-code-value--document",
              ),
            ].every((el) => getComputedStyle(el).whiteSpace === "nowrap"),
            fullRow: [...body.querySelectorAll<HTMLElement>(".zh-description-grid__full-row")].every((el) => {
              const grid = el.parentElement!;
              const row = el.getBoundingClientRect();
              const bounds = grid.getBoundingClientRect();
              const style = getComputedStyle(el);
              return Math.abs(row.left - bounds.left) <= 1 &&
                Math.abs(row.right - bounds.right) <= 1 &&
                style.textAlign === "start" && style.overflowWrap === "anywhere" &&
                el.scrollWidth <= el.clientWidth + 1 && row.height > 20;
            }),
            title: (() => {
              const el = panel.querySelector<HTMLElement>("h2,h3")!;
              return (
                el.scrollWidth <= el.clientWidth + 1 &&
                getComputedStyle(el).whiteSpace !== "nowrap"
              );
            })(),
            text:
              !table ||
              [
                "1234567890".repeat(4) + "123456789",
                "cliente".repeat(16) + "@example.com",
                "12345678-1234-1234-1234-123456789012",
              ].every((text) =>
                [...body.querySelectorAll("dd")].some(
                  (el) =>
                    el.textContent === text &&
                    el.scrollWidth <= el.clientWidth + 1,
                ),
              ),
          };
        });
        expect(result).toEqual({
          viewport: true,
          panel: true,
          horizontal: true,
          actions: Array(kind === "confirm" || kind === "prompt" ? 2 : 5).fill(
            true,
          ),
          count: kind === "confirm" || kind === "prompt" ? 2 : 5,
          vertical: true,
          table: true,
          nowrap: true,
          fullRow: true,
          title: true,
          text: true,
        });
      } finally {
        await page.close();
      }
    },
  );
});
