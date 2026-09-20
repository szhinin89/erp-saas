/**
 * SALES-QUICK-CUSTOMER-MODAL-INPUT-FIX-07: true cuando el usuario está escribiendo en un
 * control editable (input/textarea/select nativo, o cualquier elemento contentEditable) —
 * úsalo para que un listener global de teclado (shortcuts, atajos POS, captura de barcode) no
 * intercepte texto normal. Un atajo global NUNCA debe reaccionar mientras el foco está dentro de
 * un campo editable, sin importar la tecla.
 */
export function isEditableTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) return false;
  const tag = target.tagName;
  if (tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT") return true;
  // target.isContentEditable no está implementado en jsdom (siempre undefined) — se resuelve por
  // el atributo directamente, válido tanto en navegador real como en tests.
  const contentEditable = target.getAttribute("contenteditable");
  return contentEditable === "" || contentEditable === "true";
}

/**
 * Establece el valor de un input nativo y dispara el evento para que React (y RHF) lo detecte.
 * Usar cuando se manipula el valor programáticamente (p. ej. al sanitizar un paste).
 */
export function setProgrammaticInputValue(
  input: HTMLInputElement,
  value: string,
): void {
  const nativeSetter = Object.getOwnPropertyDescriptor(
    window.HTMLInputElement.prototype,
    "value",
  )?.set;
  if (nativeSetter) {
    nativeSetter.call(input, value);
    input.dispatchEvent(new Event("input", { bubbles: true }));
  }
}
