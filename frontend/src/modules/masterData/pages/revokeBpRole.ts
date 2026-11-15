import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import type { BpContext } from "../components/bpDetailTabs";
import { BP_CONTEXT_ROLE } from "../components/bpDetailTabs";

const ROLE_LABEL: Record<BpContext, string> = { supplier: "Proveedor", customer: "Cliente" };

/**
 * Revoca el rol del módulo (Proveedor/Cliente) desde su listado, con la confirmación oficial. Es la vía
 * de revocación tras retirar la pestaña «Roles» de la ficha; el socio no se elimina ni se desactiva y el
 * otro rol no se toca. Devuelve true si se revocó (el llamador recarga el listado).
 */
export async function revokeBpContextRole(context: BpContext, bpId: string): Promise<boolean> {
  const role = ROLE_LABEL[context];
  try {
    const bp = await businessPartnerFacade.getBusinessPartner(bpId);
    const active = bp.roles.find((r) => r.isActive && r.roleType === BP_CONTEXT_ROLE[context]);
    if (!active) return false;
    const confirmed = await message.confirm({
      title: `Revocar rol "${role}"`,
      message: `El socio de negocio dejará de operar bajo el rol "${role}". El socio de negocio en sí no se elimina — solo deja de tener este rol activo.`,
      variant: "danger",
      confirmLabel: "Revocar rol",
      cancelLabel: "Cancelar",
    });
    if (!confirmed) return false;
    await businessPartnerFacade.revokeRole(bpId, active.id);
    message.success(`Rol "${role}" revocado correctamente.`);
    return true;
  } catch (err) {
    message.error(formatApiRequestError(err, { generic: "Error al revocar el rol." }));
    return false;
  }
}
