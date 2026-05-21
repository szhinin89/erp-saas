import type { ButtonHTMLAttributes, ReactNode } from 'react';

type ButtonVariant = 'primary' | 'secondary' | 'success' | 'danger' | 'warning' | 'ghost';
type ButtonSize = 'sm' | 'md' | 'lg';

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  children: ReactNode;
}

const variantClass: Record<ButtonVariant, string> = {
  primary: 'zh-btn--primary',
  secondary: 'zh-btn--ghost',
  success: 'zh-btn--primary',
  danger: 'zh-btn--destructive',
  warning: 'zh-btn--ghost',
  ghost: 'zh-btn--ghost',
};

const sizeClass: Record<ButtonSize, string> = {
  sm: 'zh-btn--sm',
  md: 'zh-btn--md',
  lg: 'zh-btn--lg',
};

/** @deprecated Preferir `ZHBtn`. Usa clases ZH internamente. */
export function Button({ variant = 'primary', size = 'md', children, className = '', type, ...props }: ButtonProps) {
  return (
    <button
      type={type ?? 'button'}
      className={`zh-btn ${variantClass[variant]} ${sizeClass[size]} ${className}`.trim()}
      {...props}
    >
      {children}
    </button>
  );
}
