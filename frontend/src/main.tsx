import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import './components/zh/ZHLayout.css'
import './components/zh/ZHFormTabs.css'
import './styles/zh-global-bridge.css'
import App from './App.tsx'
import { I18nProvider } from './i18n/i18n'
import { DeploymentProvider } from './deployment/DeploymentContext'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <I18nProvider>
      <DeploymentProvider>
        <App />
      </DeploymentProvider>
    </I18nProvider>
  </StrictMode>,
)
