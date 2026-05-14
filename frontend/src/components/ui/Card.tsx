import React from 'react'

interface CardProps {
  title?: React.ReactNode
  actions?: React.ReactNode
  className?: string
  bodyClassName?: string
  children: React.ReactNode
}

export const Card: React.FC<CardProps> = ({ title, actions, className = '', bodyClassName = '', children }) => {
  const cardClassName = `card ${className}`.trim()
  const cardBodyClassName = `card-body ${bodyClassName}`.trim()
  return (
    <div className={cardClassName}>
      {(title || actions) && (
        <div className="card-header">
          {title && <span>{title}</span>}
          {actions && <div>{actions}</div>}
        </div>
      )}
      <div className={cardBodyClassName}>{children}</div>
    </div>
  )
}
