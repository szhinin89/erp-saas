import type { InputHTMLAttributes, ReactNode } from 'react';

interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  label?: ReactNode;
  error?: string;
  hint?: string;
  required?: boolean;
}

/** @deprecated Preferir `ZHField` + `zh-input`. Usa tokens ZH en el input. */
export function Input({ label, error, hint, required, id, className = '', ...props }: InputProps) {
  const inputId = id || (typeof label === 'string' ? label.toLowerCase().replace(/\s/g, '-') : undefined);

  return (
    <div className="form-group">
      {label && (
        <label htmlFor={inputId} className={required ? 'label-required' : ''}>
          {label}
        </label>
      )}
      <input
        id={inputId}
        className={`zh-input ${error ? 'error' : ''} ${className}`.trim()}
        {...props}
      />
      {error && <div className="error-message active">{error}</div>}
      {!error && hint ? <div className="text-sm text-muted">{hint}</div> : null}
    </div>
  );
}
