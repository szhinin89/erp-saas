import { StrictMode, lazy, Suspense } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import './components/zh/ZHLayout.css'
import './components/zh/ZHFormTabs.css'
import { I18nProvider } from './i18n/i18n'
import { DeploymentProvider } from './deployment/DeploymentContext'
import { LoadingState } from './components/PageShell'

const App = lazy(() => import('./App.tsx'))

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <I18nProvider>
      <DeploymentProvider>
        <Suspense fallback={<LoadingState />}>
          <App />
        </Suspense>
      </DeploymentProvider>
    </I18nProvider>
  </StrictMode>,
)
