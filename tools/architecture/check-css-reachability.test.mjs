import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  analyzeCssReachability,
  blankComments,
  classTokens,
  cssClassOwners,
  runCheckCssReachability,
} from './check-css-reachability.mjs';

/**
 * F-04-css-reach: una pantalla debe cargar por sí sola el CSS de las clases que usan sus
 * componentes. Los fixtures son un repo mínimo en memoria: main → App → routes (import lazy) → pantallas.
 */
const S = 'frontend/src/';
const CFG = { entry: `${S}main.tsx`, routeDirs: [`${S}routes/`] };

function repo(extra = {}) {
  return new Map(
    Object.entries({
      [`${S}main.tsx`]: `import "./index.css";\nconst App = lazy(() => import("./App.tsx"));`,
      [`${S}index.css`]: `@import "./styles/global.css";`,
      [`${S}styles/global.css`]: `.zh-global { color: red; }`,
      [`${S}App.tsx`]: `import { pages } from "./routes/routes.tsx"; export default function App() { return null; }`,
      [`${S}routes/routes.tsx`]:
        `const A = lazy(() => import("../pages/PageA.tsx"));\nconst B = lazy(() => import("../pages/PageB.tsx"));`,
      [`${S}pages/PageB.tsx`]: `import "../widgets/widget.css";\nexport const PageB = () => <div className="mod-widget" />;`,
      [`${S}widgets/widget.css`]: `.mod-widget { padding: 4px; }\n.mod-widget__title { font-weight: 700; }`,
      ...extra,
    }),
  );
}
const pageA = (body) => ({
  [`${S}pages/PageA.tsx`]: body,
});
const comp = (body) => ({ [`${S}widgets/Widget.tsx`]: body });
const keys = (r) => r.added;

test('positivo: el componente usa una clase cuya hoja solo importa OTRA pantalla → violación', () => {
  const files = repo({
    ...pageA(`import { Widget } from "../widgets/Widget.tsx"; export const PageA = () => <Widget />;`),
    ...comp(`export const Widget = () => <div className="mod-widget mod-widget__title" />;`),
  });
  const r = analyzeCssReachability(files, CFG);
  assert.deepEqual(keys(r), [`${S}widgets/Widget.tsx|${S}widgets/widget.css`]);
  const e = r.found.get(keys(r)[0]);
  assert.deepEqual([...e.classes].sort(), ['mod-widget', 'mod-widget__title']);
  assert.deepEqual([...e.roots], [`${S}pages/PageA.tsx`]);
});

test('negativo: el componente importa su propio CSS → sin violación', () => {
  const files = repo({
    ...pageA(`import { Widget } from "../widgets/Widget.tsx"; export const PageA = () => <Widget />;`),
    ...comp(`import "./widget.css";\nexport const Widget = () => <div className="mod-widget" />;`),
  });
  assert.deepEqual(keys(analyzeCssReachability(files, CFG)), []);
});

test('negativo: la pantalla importa el CSS (cierre de imports) → sin violación', () => {
  const files = repo({
    ...pageA(`import "../widgets/widget.css";\nimport { Widget } from "../widgets/Widget.tsx"; export const PageA = () => <Widget />;`),
    ...comp(`export const Widget = () => <div className="mod-widget" />;`),
  });
  assert.deepEqual(keys(analyzeCssReachability(files, CFG)), []);
});

test('negativo: clase propiedad del CSS global → sin violación', () => {
  const files = repo({
    ...pageA(`export const PageA = () => <div className="zh-global" />;`),
  });
  assert.deepEqual(keys(analyzeCssReachability(files, CFG)), []);
});

test('negativo: clase solo usada como contexto en otra hoja (marcador) no la hace propietaria', () => {
  const files = repo({
    ...pageA(`export const PageA = () => <div className="mod-marker" />;`),
    [`${S}pages/other.css`]: `.rpt-toolbar .mod-marker { gap: 2px; }`,
    [`${S}pages/PageB.tsx`]: `import "./other.css";\nexport const PageB = () => <div className="rpt-toolbar" />;`,
  });
  assert.deepEqual(keys(analyzeCssReachability(files, CFG)), []);
});

test('negativo: clases en comentarios y archivos de prueba no cuentan (los de prueba ni se cargan)', () => {
  const files = repo({
    ...pageA(`// usa mod-widget\n/* mod-widget */\nexport const PageA = () => <div />;`),
  });
  assert.deepEqual(keys(analyzeCssReachability(files, CFG)), []);
  assert.equal(blankComments('a // mod-widget\n/* mod-widget */b').includes('mod-widget'), false);
  assert.deepEqual([...classTokens(blankComments('const x = "mod-widget"; // "otro-valor"'))], ['mod-widget']);
});

test('positivo: un `import type` del módulo que trae el CSS no lo carga en runtime → violación se mantiene', () => {
  const files = repo({
    ...pageA(
      `import type { T } from "../widgets/WithCss.tsx";\nimport { Widget } from "../widgets/Widget.tsx"; export const PageA = () => <Widget />;`,
    ),
    ...comp(`export const Widget = () => <div className="mod-widget" />;`),
    [`${S}widgets/WithCss.tsx`]: `import "./widget.css";\nexport type T = string;`,
  });
  assert.equal(keys(analyzeCssReachability(files, CFG)).length, 1);
});

test('negativo: un modal lazy interno (import() fuera de routes/) carga junto a su pantalla', () => {
  const files = repo({
    ...pageA(`const Modal = lazy(() => import("../widgets/Modal.tsx")); export const PageA = () => <Modal />;`),
    [`${S}widgets/Modal.tsx`]: `import "./widget.css";\nexport const Modal = () => <div className="mod-widget" />;`,
  });
  assert.deepEqual(keys(analyzeCssReachability(files, CFG)), []);
});

test('línea base: par conocido → pasa; par nuevo → falla; par ya resuelto en la base → obsoleto', () => {
  const files = repo({
    ...pageA(`import { Widget } from "../widgets/Widget.tsx"; export const PageA = () => <Widget />;`),
    ...comp(`export const Widget = () => <div className="mod-widget" />;`),
  });
  const key = `${S}widgets/Widget.tsx|${S}widgets/widget.css`;
  const known = analyzeCssReachability(files, { ...CFG, baseline: [key] });
  assert.deepEqual([known.added, known.stale], [[], []]);

  const fresh = analyzeCssReachability(files, { ...CFG, baseline: [] });
  assert.deepEqual(fresh.added, [key]);

  const obsolete = analyzeCssReachability(files, { ...CFG, baseline: [key, `${S}gone/Old.tsx|${S}gone/old.css`] });
  assert.deepEqual(obsolete.stale, [`${S}gone/Old.tsx|${S}gone/old.css`]);
});

test('propiedad: selectores agrupados y @media reconocen la clase que encabeza cada selector; comentarios ignorados', () => {
  const owners = cssClassOwners(
    new Map([['x.css', '/* .fantasma {} */\n.uno, .dos .tres { a: 1 }\n@media (max-width: 1px) { .cuatro > .cinco { a: 1 } }']]),
  );
  assert.deepEqual([...owners.keys()].sort(), ['cuatro', 'uno', 'dos'].sort());
});

test('repo real: sin dependencias CSS entre rutas fuera de la línea base', () => {
  const result = runCheckCssReachability();
  assert.deepEqual(
    result.violations.map((v) => `${v.file}: ${v.message.slice(0, 80)}`),
    [],
  );
});
