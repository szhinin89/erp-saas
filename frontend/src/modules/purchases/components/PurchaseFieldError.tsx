/** DS inline field-error pattern; explicit target for aria-describedby, no nested labels. */
export function PurchaseFieldError({ id, message }: { id: string; message?: string }) {
  return message ? <span id={id} role="alert" className="zh-field-hint zh-field-hint--error">{message}</span> : null;
}
