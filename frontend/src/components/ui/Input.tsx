import React from 'react'

interface InputProps extends React.InputHTMLAttributes<HTMLInputElement> {
  label?: string
  error?: string
  hint?: string
  required?: boolean
}

export const Input: React.FC<InputProps> = ({ label, error, hint, required, id, className = '', ...props }) => {
  const inputId = id || label?.toLowerCase().replace(/\s/g, '-')

  return (
    <div className="form-group">
      {label && (
        <label htmlFor={inputId} className={required ? 'label-required' : ''}>
          {label}
        </label>
      )}
      <input id={inputId} className={`${error ? 'error' : ''} ${className}`.trim()} {...props} />
      {error && <div className="error-message active">{error}</div>}
      {!error && hint ? <div className="text-sm text-muted">{hint}</div> : null}
    </div>
  )
}
