import React, { useEffect } from 'react'

interface DrawerProps {
  isOpen: boolean
  onClose: () => void
  title?: string
  children: React.ReactNode
}

export const Drawer: React.FC<DrawerProps> = ({ isOpen, onClose, title, children }) => {
  useEffect(() => {
    if (isOpen) {
      document.body.style.overflow = 'hidden'
    } else {
      document.body.style.overflow = ''
    }

    return () => {
      document.body.style.overflow = ''
    }
  }, [isOpen])

  return (
    <div className={`drawer-overlay ${isOpen ? 'open' : ''}`} onClick={onClose}>
      <div className={`drawer ${isOpen ? 'open' : ''}`} onClick={(e) => e.stopPropagation()}>
        {title && <h3>{title}</h3>}
        {children}
        <button className="btn btn-secondary btn-sm ui-drawer-close-btn" onClick={onClose} type="button">
          Cerrar
        </button>
      </div>
    </div>
  )
}
