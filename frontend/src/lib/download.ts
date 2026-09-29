/**
 * Descarga de archivos en el navegador — única implementación del mecanismo
 * ancla sintética + object URL, compartida por todos los módulos (RIDE, XML de
 * comprobantes, retenciones, plantillas de carga inicial).
 */

/** Descarga un Blob ya obtenido (ej. el PDF de un RIDE). */
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

/** Descarga contenido de texto construido en memoria (ej. XML draft/signed/authorized). */
export function downloadTextFile(
  content: string,
  filename: string,
  mimeType = "application/xml",
): void {
  downloadBlob(new Blob([content], { type: mimeType }), filename);
}
