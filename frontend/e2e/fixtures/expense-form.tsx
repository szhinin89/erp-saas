import { createRoot, type Root } from "react-dom/client";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { I18nProvider } from "../../src/i18n/i18n";
import { setPrecisionPolicyForTests } from "../../src/lib/config/precisionPolicy.config";
import { TEST_PRECISION_POLICY } from "../../src/test/precisionPolicyFixture";
import { useSessionStore } from "../../src/store/sessionStore";
import "../../src/index.css";
import "../../src/components/AppLayout.css";
import { ZHToast } from "../../src/components/zh/ZHToast";
import { LayoutFrame } from "../../src/components/layout/LayoutFrame";
import { ExpenseDocumentFormPage } from "../../src/modules/expenses/pages/ExpenseDocumentFormPage";

/**
 * Fixture visual de Gastos (EXPENSE-UI-02): monta la página REAL con el CSS real dentro del
 * `LayoutFrame` real (mismo contenedor de scroll que la app). La API se simula con `page.route`
 * desde el spec; aquí solo se siembra sesión/permisos y precisión.
 */
setPrecisionPolicyForTests(TEST_PRECISION_POLICY);
useSessionStore.setState({
  isLoaded: true,
  authorization: { permissions: ["*"] } as never,
});

// `?reception=1` abre un gasto nuevo desde una recepción XML (diferencia de importes).
const fromReception = new URLSearchParams(location.search).get("reception") === "1";
const page = (
  <LayoutFrame topUtilities={<div className="app-tenantHeaderWrap"><div className="fx-header" /></div>}>
    <ExpenseDocumentFormPage />
  </LayoutFrame>
);

function Fixture() {
  return (
    <I18nProvider>
      <MemoryRouter
        initialEntries={[fromReception ? "/expenses/documents/new?fromReceptionId=r1" : "/expenses/documents/exp-1"]}
      >
        <Routes>
          <Route path="/expenses/documents/new" element={page} />
          <Route path="/expenses/documents/:id" element={page} />
        </Routes>
      </MemoryRouter>
      <ZHToast />
    </I18nProvider>
  );
}

const mount = document.getElementById("root")! as HTMLElement & { expRoot?: Root };
mount.expRoot ??= createRoot(mount);
mount.expRoot.render(<Fixture />);
