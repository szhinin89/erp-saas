import { Navigate, useParams } from "react-router-dom";

/**
 * URLS-MENU-ALIGNMENT-01: <Navigate to="…/:id" /> no interpola params — a diferencia de los
 * redirects de rutas fijas (Navigate simple), un redirect legacy con parámetro dinámico necesita
 * leerlo de la URL actual antes de armar el destino.
 */
export function SupplierCreditDetailLegacyRedirect() {
  const { id } = useParams();
  return <Navigate to={`/suppliers/credits/${id}`} replace />;
}
