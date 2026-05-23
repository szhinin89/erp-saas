import { PlatformPanelPage } from '../PlatformPanelPage';

/**
 * @deprecated Sin ruta en `platformRoutes.tsx`. Usar `/platform/subscribers` y ficha `/platform/subscribers/:id`.
 */
export function PlatformCompaniesShellPage() {
  return <PlatformPanelPage shellLayout embeddedTab="companies" />;
}
