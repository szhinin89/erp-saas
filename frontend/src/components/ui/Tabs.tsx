import React, { useState } from 'react'

interface TabItem {
  id: string
  label: string
  content: React.ReactNode
}

interface TabsProps {
  tabs: TabItem[]
  defaultActiveId?: string
}

export const Tabs: React.FC<TabsProps> = ({ tabs, defaultActiveId }) => {
  const [activeId, setActiveId] = useState(defaultActiveId || tabs[0]?.id)

  return (
    <div>
      <div className="tabs">
        {tabs.map((tab) => (
          <button
            key={tab.id}
            className={`tab-btn ${activeId === tab.id ? 'active' : ''}`}
            onClick={() => setActiveId(tab.id)}
            type="button"
          >
            {tab.label}
          </button>
        ))}
      </div>
      {tabs.map((tab) => (
        <div key={tab.id} className={`tab-pane ${activeId === tab.id ? 'active-pane' : ''}`}>
          {tab.content}
        </div>
      ))}
    </div>
  )
}
