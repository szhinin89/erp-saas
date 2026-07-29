/**
 * Descarga un Blob binario ya obtenido (ej. el PDF de un RIDE) — generalización de
 * `downloadTextFile` (electronicDocuments/monitor/utils/download.ts) para blobs binarios en vez
 * de contenido de texto construido en memoria. Mismo mecanismo: ancla sintética + object URL.
 */
export function downloadBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = filename;
  document.body.appendChild(anchor);
  anchor.click();
  document.body.removeChild(anchor);
  URL.revokeObjectURL(url);
}
