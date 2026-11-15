import { useCallback, useEffect, useState } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { useI18n } from "../../../i18n/i18n";
import { Badge } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { message } from "../../../lib/messages";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { formatApiRequestError } from "../../lib/apiError";
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import { formatDateTime } from "../../../lib/formatters/dateFormatters";
import { useSriIdTypes, getSriIdTypeName } from "../api/useSriIdTypes";
import { useLegalEntityTypes } from "../api/useLegalEntityTypes";
import { BpLocationsSection } from "../components/BpLocationsSection";
import { BpContactsSection } from "../components/BpContactsSection";
import type {
  BusinessPartnerDetailDto,
  BusinessPartnerRoleDto,
  BpLocationDto,
  BpContactDto,
} from "../types/businessPartner.types";
import "./masterdata-pages.css";
import { ZHTabBar } from "../../../components/zh/ZHTabBar";

// ── Helpers ──────────────────────────────────────────────────────────────────

function Row({ label, value }: { label: string; value?: string | null }) {
  return (
    <div className="md-detail-row">
      <span className="md-detail-label">{label}</span>
      <span className="md-detail-value">
        {value ?? <em className="md-detail-empty">—</em>}
      </span>
    </div>
  );
}

// ── Main page ────────────────────────────────────────────────────────────────

type TabId = "identity" | "roles" | "locations" | "contacts";

export function MasterDataBusinessPartnerDetailPage() {
  const { t } = useI18n();
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { canShow } = usePermissionsUi();
  const canUpdate = canShow("masterdata.businesspartners.update");
  const canDisable = canShow("masterdata.businesspartners.disable");
  const { options: idTypes } = useSriIdTypes();
  const { options: legalEntityTypes } = useLegalEntityTypes();

  const [bp, setBp] = useState<BusinessPartnerDetailDto | null>(null);
  const [locations, setLocations] = useState<BpLocationDto[]>([]);
  const [contacts, setContacts] = useState<BpContactDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<TabId>("identity");
  const [saving, setSaving] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const reload = useCallback(
    async (showLoading = false) => {
      if (!id) return;
      if (showLoading) setLoading(true);
      setError(null);
      try {
        const [bpData, locs, cons] = await Promise.all([
          businessPartnerFacade.getBusinessPartner(id),
          businessPartnerFacade.getLocations(id, undefined),
          businessPartnerFacade.getContacts(id, undefined),
        ]);
        setBp(bpData);
        setLocations(locs);
        setContacts(cons);
      } catch {
        setError("No se pudo cargar el BusinessPartner.");
      } finally {
        setLoading(false);
      }
    },
    [id],
  );

  useEffect(() => {
    void reload(true);
  }, [reload]);

  // ── Role revoke ────────────────────────────────────────────────────────────

  const revokeRole = async (role: BusinessPartnerRoleDto) => {
    if (!id || saving) return;

    const confirmed = await message.confirm({
      title: `Revocar rol "${role.roleLabel}"`,
      message: `El socio de negocio dejará de operar bajo el rol "${role.roleLabel}". El socio de negocio en sí no se elimina — solo deja de tener este rol activo.`,
      variant: "danger",
      confirmLabel: "Revocar rol",
      cancelLabel: "Cancelar",
    });
    if (!confirmed) return;

    setSaving(true);
    setActionError(null);
    try {
      await businessPartnerFacade.revokeRole(id, role.id);
      await reload();
      message.success(`Rol "${role.roleLabel}" revocado correctamente.`);
    } catch (err) {
      const errorMessage = formatApiRequestError(err, {
        generic: "Error al revocar el rol.",
      });
      setActionError(errorMessage);
      message.error(errorMessage);
    } finally {
      setSaving(false);
    }
  };

  // ── Derived ────────────────────────────────────────────────────────────────

  const activeRoles = bp?.roles.filter((r) => r.isActive) ?? [];
  const revokedRoles = bp?.roles.filter((r) => !r.isActive) ?? [];
  const activeLocs = locations.filter((l) => l.isActive);
  const activeCons = contacts.filter((c) => c.isActive);

  const TABS: { id: TabId; label: string; count?: number }[] = [
    { id: "identity", label: "Identidad" },
    { id: "roles", label: "Roles", count: activeRoles.length },
    { id: "locations", label: "Ubicaciones", count: activeLocs.length },
    { id: "contacts", label: "Contactos", count: activeCons.length },
  ];

  return (
    <ErpPageTemplate
      kicker={t("masterdata.kicker", "Clientes y proveedores")}
      title={bp ? bp.tradeName?.trim() || bp.legalName : "BusinessPartner"}
      subtitle={bp ? `${bp.identificationType} ${bp.identificationNumber}` : ""}
      action={
        <ZHBtn variant="ghost" onClick={() => navigate(-1)}>
          ← Volver
        </ZHBtn>
      }
    >
      {loading && <p>Cargando…</p>}
      {error && <ZHPageNotice variant="error" message={error} />}
      {actionError && <ZHPageNotice variant="error" message={actionError} />}

      {bp && (
        <>
          <div className="md-detail-status-row">
            <Badge
              variant={bp.isActive ? "success" : "warning"}
              label={bp.isActive ? "Activo" : "Inactivo"}
            />
            {activeRoles.map((r) => (
              <Badge key={r.id} variant="success" label={r.roleLabel} />
            ))}
          </div>

          <ZHTabBar tabs={TABS} activeTab={activeTab} onChange={setActiveTab} />

          {/* ── Identity ──────────────────────────────────────────── */}
          {activeTab === "identity" && (
            <section className="md-detail-section">
              <Row
                label="Tipo de identificación"
                value={`${bp.identificationType} — ${getSriIdTypeName(bp.identificationType, idTypes)}`}
              />
              <Row label="Número" value={bp.identificationNumber} />
              <Row
  label="Tipo de entidad legal"
  value={legalEntityTypes.find((x) => x.code === bp.legalEntityTypeCode)?.name ?? String(bp.legalEntityTypeCode)}
/>
              <Row label="Razón social" value={bp.legalName} />
              <Row label="Nombre comercial" value={bp.tradeName} />
              <Row label="País" value={bp.countryCode ?? "EC"} />
              <Row label="Creado" value={formatDateTime(bp.createdAt)} />
            </section>
          )}

          {/* ── Roles ─────────────────────────────────────────────── */}
          {activeTab === "roles" && (
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
                          {r.notes && (
                            <p className="md-role-card__notes">{r.notes}</p>
                          )}
                          {r.customerConfig && (
                            <div className="md-role-card__config md-role-card__config--crm">
                              {r.customerConfig.customerCategory && (
                                <small>
                                  Categoría: {r.customerConfig.customerCategory}
                                </small>
                              )}
                              {r.customerConfig.customerSegment && (
                                <small>
                                  Segmento: {r.customerConfig.customerSegment}
                                </small>
                              )}
                              {r.customerConfig.salesZone && (
                                <small>
                                  Zona: {r.customerConfig.salesZone}
                                </small>
                              )}
                              {r.customerConfig.creditRating && (
                                <small>
                                  Rating: {r.customerConfig.creditRating}
                                </small>
                              )}
                              {r.customerConfig.loyaltyTier && (
                                <small>
                                  Fidelización: {r.customerConfig.loyaltyTier}
                                </small>
                              )}
                            </div>
                          )}
                          {r.supplierConfig && (
                            <div className="md-role-card__config">
                              <small>
                                Sustento:{" "}
                                {r.supplierConfig.defaultTaxSupportCode ?? "—"}
                              </small>
                            </div>
                          )}
                          {r.carrierConfig?.transportAuthorizationNumber && (
                            <div className="md-role-card__config">
                              <small>
                                Autorización:{" "}
                                {r.carrierConfig.transportAuthorizationNumber}
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
                          <div
                            key={r.id}
                            className="md-role-card md-role-card--inactive"
                          >
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
          )}

          {/* ── Locations / Contacts (secciones propias; permanecen montadas) ── */}
          <BpLocationsSection
            bpId={bp.id}
            visible={activeTab === "locations"}
            locations={locations}
            canUpdate={canUpdate}
            canDisable={canDisable}
            saving={saving}
            onSavingChange={setSaving}
            actionError={actionError}
            onActionError={setActionError}
            onReload={reload}
          />
          <BpContactsSection
            bpId={bp.id}
            visible={activeTab === "contacts"}
            contacts={contacts}
            activeLocations={activeLocs}
            canUpdate={canUpdate}
            canDisable={canDisable}
            saving={saving}
            onSavingChange={setSaving}
            actionError={actionError}
            onActionError={setActionError}
            onReload={reload}
          />
        </>
      )}
    </ErpPageTemplate>
  );
}

