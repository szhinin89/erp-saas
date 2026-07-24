/** Descarga contenido de texto como archivo — usado para los XML (draft/signed/authorized) del Monitor. */
export function downloadTextFile(content: string, filename: string, mimeType = 'application/xml'): void {
  const blob = new Blob([content], { type: mimeType });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  document.body.appendChild(anchor);
  anchor.click();
  document.body.removeChild(anchor);
  URL.revokeObjectURL(url);
}
