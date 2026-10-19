import { StrictMode, lazy, Suspense } from "react";
import { createRoot } from "react-dom/client";
import "./index.css";
import { I18nProvider } from "./i18n/i18n";
import { ensureLocaleLoaded, safeGetStoredLocale } from "./i18n/dictionaries";
import { LoadingState } from "./components/PageShell";

const App = lazy(() => import("./App.tsx"));

const root = createRoot(document.getElementById("root")!);
const render = () =>
  root.render(
    <StrictMode>
      <I18nProvider>
        <Suspense fallback={<LoadingState />}>
          <App />
        </Suspense>
      </I18nProvider>
    </StrictMode>,
  );

// El idioma guardado se descarga antes del primer render (sin destello en español); si la descarga
// falla, la app arranca igual en el idioma por defecto.
Promise.resolve()
  .then(() => ensureLocaleLoaded(safeGetStoredLocale()))
  .catch(() => undefined)
  .then(render);
