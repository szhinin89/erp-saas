import { Badge } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import type {
  BusinessPartnerDetailDto,
  BusinessPartnerRoleDto,
} from "../types/businessPartner.types";

export interface BpRolesSectionProps {
  bp: BusinessPartnerDetailDto;
  canDisable: boolean;
  /** Estado de guardado compartido con la ficha (una sola operación a la vez). */
  saving: boolean;
  onSavingChange: (saving: boolean) => void;
  onActionError: (error: string | null) => void;
  /** Recarga los datos de la ficha tras una operación exitosa. */
  onReload: () => Promise<void>;
}

/**
 * Pestaña «Roles» de la ficha: roles activos con su configuración resumida y revocación con
 * confirmación. Extraída de MasterDataBusinessPartnerDetailPage sin cambio de comportamiento.
 */
export function BpRolesSection({
  bp,
  canDisable,
  saving,
  onSavingChange,
  onActionError,
  onReload,
}: BpRolesSectionProps) {
  const activeRoles = bp.roles.filter((r) => r.isActive);
  const revokedRoles = bp.roles.filter((r) => !r.isActive);

  const revokeRole = async (role: BusinessPartnerRoleDto) => {
    if (saving) return;

    const confirmed = await message.confirm({
      title: `Revocar rol "${role.roleLabel}"`,
      message: `El socio de negocio dejará de operar bajo el rol "${role.roleLabel}". El socio de negocio en sí no se elimina — solo deja de tener este rol activo.`,
      variant: "danger",
      confirmLabel: "Revocar rol",
      cancelLabel: "Cancelar",
    });
    if (!confirmed) return;

    onSavingChange(true);
    onActionError(null);
    try {
      await businessPartnerFacade.revokeRole(bp.id, role.id);
      await onReload();
      message.success(`Rol "${role.roleLabel}" revocado correctamente.`);
    } catch (err) {
      const errorMessage = formatApiRequestError(err, {
        generic: "Error al revocar el rol.",
      });
      onActionError(errorMessage);
      message.error(errorMessage);
    } finally {
      onSavingChange(false);
    }
  };

  return (
    <section className="md-detail-section">
      {bp.roles.length === 0 ? (
        <p className="md-detail-empty-hint">
          Este BP no tiene roles asignados.
        </p>
      ) : (
        <>
          {activeRoles.length > 0 && (
            <div className="md-roles-grid">
              {activeRoles.map((r) => (
                <div key={r.id} className="md-role-card">
                  <div className="md-role-card__header">
                    <Badge variant="success" label={r.roleLabel} />
                    {canDisable && (
                      <ZHBtn
                        variant="ghost"
                        size="sm"
                        disabled={saving}
                        onClick={() => void revokeRole(r)}
                      >
                        Revocar
                      </ZHBtn>
                    )}
                  </div>
                  {r.notes && <p className="md-role-card__notes">{r.notes}</p>}
                  {r.customerConfig && (
                    <div className="md-role-card__config md-role-card__config--crm">
                      {r.customerConfig.customerCategory && (
                        <small>Categoría: {r.customerConfig.customerCategory}</small>
                      )}
                      {r.customerConfig.customerSegment && (
                        <small>Segmento: {r.customerConfig.customerSegment}</small>
                      )}
                      {r.customerConfig.salesZone && (
                        <small>Zona: {r.customerConfig.salesZone}</small>
                      )}
                      {r.customerConfig.creditRating && (
                        <small>Rating: {r.customerConfig.creditRating}</small>
                      )}
                      {r.customerConfig.loyaltyTier && (
                        <small>Fidelización: {r.customerConfig.loyaltyTier}</small>
                      )}
                    </div>
                  )}
                  {r.supplierConfig && (
                    <div className="md-role-card__config">
                      <small>
                        Sustento: {r.supplierConfig.defaultTaxSupportCode ?? "—"}
                      </small>
                    </div>
                  )}
                  {r.carrierConfig?.transportAuthorizationNumber && (
                    <div className="md-role-card__config">
                      <small>
                        Autorización: {r.carrierConfig.transportAuthorizationNumber}
                      </small>
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
          {revokedRoles.length > 0 && (
            <>
              <h4 className="md-detail-subheader">Roles revocados</h4>
              <div className="md-roles-grid md-roles-grid--inactive">
                {revokedRoles.map((r) => (
                  <div key={r.id} className="md-role-card md-role-card--inactive">
                    <div className="md-role-card__header">
                      <Badge variant="warning" label={r.roleLabel} />
                      <Badge variant="warning" label="Revocado" />
                    </div>
                  </div>
                ))}
              </div>
            </>
          )}
        </>
      )}
    </section>
  );
}
