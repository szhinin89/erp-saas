import { render } from "@testing-library/react";
import { createMemoryRouter, RouterProvider, useLocation } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { useAuthStore } from "../../../store/authStore";
import { BP_CONTEXT_LIST_PATH, type BpContext } from "../components/bpDetailTabs";
import { MasterDataBusinessPartnerDetailPage } from "./MasterDataBusinessPartnerDetailPage";

/**
 * Render de la ficha bajo un router de datos (`useBlocker` lo exige, igual que App.tsx).
 * `before`/`after` añaden entradas al historial para probar Atrás/Adelante.
 */
const BP_PATHS = { supplier: "/suppliers/bp-1", customer: "/customers/bp-1" } as const;

// eslint-disable-next-line react-refresh/only-export-components -- archivo de helpers de pruebas; nunca se carga en la app
function LocationProbe() {
  const { pathname, search } = useLocation();
  return (
    <>
      <div data-testid="pathname">{pathname}</div>
      <div data-testid="search">{search}</div>
    </>
  );
}

export function renderBpDetail(
  search = "",
  history: { before?: string[]; after?: string[]; state?: unknown; context?: BpContext } = {},
) {
  const before = history.before ?? [];
  // Si la prueba no fija contexto, se infiere de la pestaña solicitada (SRI/Compras/Retenciones/Subcategorías → Proveedor).
  const context = history.context ?? (/tab=(sri|purchases|retentions|expenseSubcategories)/.test(search) ? "supplier" : "customer");
  // La ficha solo existe con sesión activa; la protección de cambios no interviene sin ella.
  useAuthStore.setState({ isAuthenticated: true });
  const router = createMemoryRouter(
    [
      {
        path: `${BP_CONTEXT_LIST_PATH[context]}/:id`,
        element: (
          <>
            <LocationProbe />
            <MasterDataBusinessPartnerDetailPage context={context} />
          </>
        ),
      },
      { path: "*", element: <LocationProbe /> },
    ],
    {
      initialEntries: [
        ...before,
        { pathname: BP_PATHS[context], search, state: history.state },
        ...(history.after ?? []),
      ],
      initialIndex: before.length,
    },
  );
  return {
    router,
    ...render(
      <I18nProvider>
        <RouterProvider router={router} />
      </I18nProvider>,
    ),
  };
}
