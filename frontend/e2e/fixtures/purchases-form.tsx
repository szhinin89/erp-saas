import { createRoot, type Root } from "react-dom/client";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { I18nProvider, useI18n } from "../../src/i18n/i18n";
import { setPrecisionPolicyForTests } from "../../src/lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../src/test/precisionPolicyFixture";
import { useSessionStore } from "../../src/store/sessionStore";
import "../../src/index.css";
import "../../src/components/AppLayout.css";
import { ZHToast } from "../../src/components/zh/ZHToast";
import { LayoutFrame } from "../../src/components/layout/LayoutFrame";
import { PurchasesPage } from "../../src/modules/purchases/pages/PurchasesPage";

/**
 * Fixture visual de «Nueva compra»: monta la página REAL con el CSS real dentro del `LayoutFrame`
 * real. La API se simula con `page.route` desde el spec; aquí solo se siembra sesión/permisos.
 * `?invoiceId=inv-1` abre una compra existente (borrador o confirmada según el mock).
 */
setPrecisionPolicyForTests(TEST_PRECISION_POLICY);
useSessionStore.setState({ isLoaded: true, authorization: { permissions: ["*"] } as never });

const entry = `/purchases${location.search}`;
const page = (
  <LayoutFrame topUtilities={<div className="app-tenantHeaderWrap"><div className="fx-header" /></div>}>
    <PurchasesPage />
  </LayoutFrame>
);

/**
 * Puente de prueba: expone el cambio de idioma del `I18nProvider` oficial (el mismo `setLocale` del
 * selector de idioma de la app) para verificar «Español <-> Inglés» sin recargar la página.
 */
function LocaleBridge() {
  const { locale, setLocale } = useI18n();
  const w = window as unknown as { __setLocale: (l: "es" | "en") => void; __locale: string };
  w.__setLocale = setLocale;
  w.__locale = locale;
  return null;
}

function Fixture() {
  return (
    <I18nProvider>
      <LocaleBridge />
      <MemoryRouter initialEntries={[entry]}>
        <Routes><Route path="/purchases" element={page} /></Routes>
      </MemoryRouter>
      <ZHToast />
    </I18nProvider>
  );
}

const mount = document.getElementById("root")! as HTMLElement & { purRoot?: Root };
mount.purRoot ??= createRoot(mount);
mount.purRoot.render(<Fixture />);
