// En la app, main.tsx descarga `en` bajo demanda antes del primer render (P-01). Los tests leen
// `dictionaries.en` y cambian de idioma de forma síncrona, así que se precarga una vez por archivo.
// Import dinámico: solo resuelve el módulo de diccionarios (sin dependencias), no `i18n.tsx`, que
// algunos tests sustituyen con vi.mock.
const { ensureLocaleLoaded } = await import("../i18n/dictionaries");
await ensureLocaleLoaded("en");
