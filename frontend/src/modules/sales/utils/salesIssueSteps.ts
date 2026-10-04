/**
 * POS-ISSUE-REAL-PROGRESS-01: pasos de emisión que el frontend REALMENTE conoce. 0 y 1 son
 * awaits reales (validación del formulario y persistDraft); el último es la única request de
 * autorización (numeración + emisión). Lo que ocurre dentro de esa request (XML / firma / SRI en
 * electrónica) no expone progreso intermedio, así que ya no se simulan pasos que el cliente no
 * conoce: el resultado real llega en la respuesta (estado electrónico, autorización).
 */
export function issueStepsFor(isElectronic: boolean): readonly string[] {
  return [
    "Validando datos",
    "Guardando venta",
    isElectronic ? "Procesando factura electrónica" : "Registrando venta",
  ];
}
