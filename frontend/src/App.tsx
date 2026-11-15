import { createBrowserRouter, RouterProvider } from "react-router-dom";
import { ConfigProvider } from "./modules/config";
import { SessionBootstrap } from "./components/SessionBootstrap";
import { AppRoutes } from "./AppRoutes";

/**
 * Router de datos (BP-NAV-GUARD-01): necesario para `useBlocker` (protección de cambios sin guardar).
 * Una única ruta comodín renderiza el árbol declarativo existente (`AppRoutes`), de modo que rutas,
 * guards y layouts no cambian.
 */
const router = createBrowserRouter([{ path: "*", element: <AppRoutes /> }]);

export default function App() {
  return (
    <SessionBootstrap>
      <ConfigProvider>
        <RouterProvider router={router} />
      </ConfigProvider>
    </SessionBootstrap>
  );
}
