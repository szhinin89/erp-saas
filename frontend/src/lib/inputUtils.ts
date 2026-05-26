/**
 * Establece el valor de un input nativo y dispara el evento para que React (y RHF) lo detecte.
 * Usar cuando se manipula el valor programáticamente (p. ej. al sanitizar un paste).
 */
export function setProgrammaticInputValue(input: HTMLInputElement, value: string): void {
  const nativeSetter = Object.getOwnPropertyDescriptor(
    window.HTMLInputElement.prototype,
    'value',
  )?.set;
  if (nativeSetter) {
    nativeSetter.call(input, value);
    input.dispatchEvent(new Event('input', { bubbles: true }));
  }
}
