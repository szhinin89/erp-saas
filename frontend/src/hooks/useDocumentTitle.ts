import { useEffect } from "react";

/**
 * Título por defecto del documento. DEBE mantenerse textualmente idéntico al
 * `<title>` estático de `frontend/index.html` (línea 7). `index.html` es HTML
 * estático y no puede importar esta constante, así que la sincronización es
 * manual: si se cambia uno, cambiar el otro.
 */
export const DEFAULT_DOCUMENT_TITLE = "ZH Technologies · ERP";

export interface UseDocumentTitleOptions {
  /**
   * Sufijo opcional a anexar tras " · ". Por defecto NO se anexa ningún
   * sufijo — APP-DOCUMENT-TITLE-02: la pestaña debe mostrar solo el título
   * de la pantalla, para poder distinguir varias pestañas abiertas.
   */
  suffix?: string;
  /**
   * Título alternativo mientras el llamador considere que la pantalla está
   * cargando (ver `loading`). El hook no infiere estado de carga alguno.
   */
  loadingTitle?: string;
  /** Cuando es true y hay `loadingTitle`, se usa `loadingTitle` en lugar de `title`. */
  loading?: boolean;
}

/**
 * Sincroniza el título de la pestaña del navegador con el título de la pantalla.
 *
 * El hook NO traduce: el llamador pasa texto ya traducido. Solo aplica el
 * título tal cual (sin sufijo por defecto — ver `UseDocumentTitleOptions.suffix`
 * para el caso opt-in) y el fallback cuando no hay título.
 *
 * Comportamiento en desmontaje: **no** restaura el fallback. React Router
 * desmonta la página saliente y monta la entrante en el mismo ciclo de commit,
 * por lo que el `PageShell` de la nueva pantalla vuelve a fijar el título de
 * inmediato; un reset en cleanup solo produciría un parpadeo visible a
 * "ZH Technologies · ERP" en cada navegación. No existe en este código una ruta
 * que desmonte la última pantalla sin montar otra (logout redirige a la pantalla
 * de login, que también usa este hook), así que no hay riesgo real de
 * título obsoleto persistente.
 */
export function useDocumentTitle(
  title: string | undefined,
  options?: UseDocumentTitleOptions,
): void {
  const { suffix, loadingTitle, loading } = options ?? {};

  useEffect(() => {
    const effective = loading && loadingTitle ? loadingTitle : title;
    const trimmed = typeof effective === "string" ? effective.trim() : "";

    if (!trimmed) {
      document.title = DEFAULT_DOCUMENT_TITLE;
      return;
    }

    document.title = suffix ? `${trimmed} · ${suffix}` : trimmed;
  }, [title, suffix, loadingTitle, loading]);
}
