import { StrictMode, lazy, Suspense } from "react";
import { createRoot } from "react-dom/client";
import "./index.css";
import { I18nProvider } from "./i18n/i18n";
import { LoadingState } from "./components/PageShell";

const App = lazy(() => import("./App.tsx"));

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <I18nProvider>
      <Suspense fallback={<LoadingState />}>
        <App />
      </Suspense>
    </I18nProvider>
  </StrictMode>,
);
