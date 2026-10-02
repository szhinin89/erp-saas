/**
 * Copia texto al portapapeles del navegador. Utilidad compartida (Monitor de comprobantes, datos del
 * trámite de anulación de retenciones ante el SRI). Devuelve false si el navegador lo impide.
 */
export async function copyToClipboard(value: string): Promise<boolean> {
  try {
    await navigator.clipboard.writeText(value);
    return true;
  } catch {
    return false;
  }
}
