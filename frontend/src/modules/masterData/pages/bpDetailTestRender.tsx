import { render } from "@testing-library/react";
import { createMemoryRouter, RouterProvider, useLocation } from "react-router-dom";
import { I18nProvider } from "../../../i18n/i18n";
import { useAuthStore } from "../../../store/authStore";
import { MasterDataBusinessPartnerDetailPage } from "./MasterDataBusinessPartnerDetailPage";

/**
 * Render de la ficha bajo un router de datos (`useBlocker` lo exige, igual que App.tsx).
 * `before`/`after` añaden entradas al historial para probar Atrás/Adelante.
 */
const BP_PATH = "/masterdata/business-partners/bp-1";

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
  history: { before?: string[]; after?: string[]; state?: unknown } = {},
) {
  const before = history.before ?? [];
  // La ficha solo existe con sesión activa; la protección de cambios no interviene sin ella.
  useAuthStore.setState({ isAuthenticated: true });
  const router = createMemoryRouter(
    [
      {
        path: "/masterdata/business-partners/:id",
        element: (
          <>
            <LocationProbe />
            <MasterDataBusinessPartnerDetailPage />
          </>
        ),
      },
      { path: "*", element: <LocationProbe /> },
    ],
    {
      initialEntries: [
        ...before,
        { pathname: BP_PATH, search, state: history.state },
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
