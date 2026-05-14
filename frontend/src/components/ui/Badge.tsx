import React from 'react'

type BadgeVariant = 'success' | 'danger' | 'warning' | 'info'

interface BadgeProps {
  variant: BadgeVariant
  children: React.ReactNode
}

export const Badge: React.FC<BadgeProps> = ({ variant, children }) => {
  return <span className={`badge badge-${variant}`}>{children}</span>
}
