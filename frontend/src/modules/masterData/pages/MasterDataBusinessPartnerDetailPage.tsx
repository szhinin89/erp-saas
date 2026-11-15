import { useCallback, useEffect, useMemo, useState } from "react";
import { useLocation, useParams, useNavigate, useSearchParams } from "react-router-dom";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { useI18n } from "../../../i18n/i18n";
import { Badge } from "../../../components/PageShell";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHTabBar } from "../../../components/zh/ZHTabBar";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { useAuthStore } from "../../../store/authStore";
import { useUnsavedChangesGuard } from "../../../hooks/useUnsavedChangesGuard";
import { businessPartnerFacade } from "../api/businessPartnerFacade";
import { BpSummarySection } from "../components/BpSummarySection";
import { BpGeneralSection } from "../components/BpGeneralSection";
import { BpSupplierSection } from "../components/BpSupplierSection";
import { BpCustomerSection } from "../components/BpCustomerSection";
import { BpLocationsSection } from "../components/BpLocationsSection";
import { BpContactsSection } from "../components/BpContactsSection";
import { BpRolesSection } from "../components/BpRolesSection";
import {
  availableBpTabs,
  resolveBpTab,
  type BpTabId,
} from "../components/bpDetailTabs";
import type {
  BusinessPartnerDetailDto,
  BpLocationDto,
  BpContactDto,
} from "../types/businessPartner.types";
import "./masterdata-pages.css";

const TAB_LABELS: Record<BpTabId, [key: string, fallback: string]> = {
  summary: ["masterdata.bp.detail.tabs.summary", "Resumen"],
  general: ["masterdata.bp.detail.tabs.general", "Datos generales"],
  supplier: ["masterdata.bp.detail.tabs.supplier", "Proveedor"],
  customer: ["masterdata.bp.detail.tabs.customer", "Cliente"],
  locations: ["masterdata.bp.detail.tabs.locations", "Ubicaciones"],
  contacts: ["masterdata.bp.detail.tabs.contacts", "Contactos"],
  roles: ["masterdata.bp.detail.tabs.roles", "Roles"],
};

/**
 * Ficha unificada de Business Partner: centro único de administración de Proveedores y Clientes.
 * Cada pestaña compone los componentes ya existentes (identidad = wizard embebido; Proveedor y
 * Cliente = formularios de configuración; Ubicaciones/Contactos/Roles), con permisos y guardados
 * independientes. `?tab=` permite enlaces profundos; una pestaña inexistente o de un rol no activo
 * cae en Resumen. Las pestañas visitadas permanecen montadas para no perder cambios sin guardar.
 */
export function MasterDataBusinessPartnerDetailPage() {
  const { t } = useI18n();
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams, setSearchParams] = useSearchParams();
  const { canShow } = usePermissionsUi();
  const canUpdate = canShow("masterdata.businesspartners.update");
  const canDisable = canShow("masterdata.businesspartners.disable");
  const canConfigure = canShow("masterdata.businesspartners.configure-company");
  const companySessionVersion = useAuthStore((s) => s.companySessionVersion);

  const [bp, setBp] = useState<BusinessPartnerDetailDto | null>(null);
  const [locations, setLocations] = useState<BpLocationDto[]>([]);
  const [contacts, setContacts] = useState<BpContactDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [visited, setVisited] = useState<ReadonlySet<BpTabId>>(new Set());
  const [dirtyMap, setDirtyMap] = useState<Record<string, boolean>>({});

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
        setError(t("masterdata.bp.detail.loadError", "No se pudo cargar el socio de negocio."));
      } finally {
        setLoading(false);
      }
    },
    [id, t],
  );

  // También al cambiar de empresa: las secciones por empresa se remontan (clave) y la ficha se relee.
  useEffect(() => {
    void reload(true);
  }, [reload, companySessionVersion]);

  const reloadQuiet = useCallback(() => reload(), [reload]);

  // ── Pestañas y enlaces profundos ───────────────────────────────────────────

  const supplierRole = bp?.roles.find(
    (r) => r.isActive && r.roleType === "Supplier",
  );
  const customerRole = bp?.roles.find(
    (r) => r.isActive && r.roleType === "Customer",
  );
  const available = useMemo(
    () =>
      bp
        ? availableBpTabs({
            hasSupplier: Boolean(supplierRole),
            hasCustomer: Boolean(customerRole),
          })
        : [],
    [bp, supplierRole, customerRole],
  );
  const requestedTab = searchParams.get("tab");
  const activeTab = resolveBpTab(requestedTab, available);

  // Con la ficha cargada, un `?tab=` inválido o de un rol no activo se normaliza a Resumen.
  useEffect(() => {
    if (bp && requestedTab !== null && requestedTab !== activeTab)
      setSearchParams({ tab: activeTab }, { replace: true, state: location.state });
  }, [bp, requestedTab, activeTab, setSearchParams, location.state]);

  // Solo se registra una pestaña realmente disponible (antes de cargar no hay pestañas).
  useEffect(() => {
    if (!bp) return;
    setVisited((prev) =>
      prev.has(activeTab) ? prev : new Set(prev).add(activeTab),
    );
  }, [bp, activeTab]);

  // ── Cambios sin guardar ────────────────────────────────────────────────────

  const reportDirty = useCallback((key: string, dirty: boolean) => {
    setDirtyMap((prev) => (prev[key] === dirty ? prev : { ...prev, [key]: dirty }));
  }, []);
  const hasUnsaved = Object.entries(dirtyMap).some(
    ([key, dirty]) =>
      dirty &&
      !(key.startsWith("supplier") && !supplierRole) &&
      !(key.startsWith("customer") && !customerRole),
  );

  // Bloquea la navegación a otra ruta (menú, Atrás/Adelante, «Volver») con la confirmación oficial,
  // una sola vez por navegación; `?tab=` no cambia de ruta y nunca bloquea. Incluye `beforeunload`.
  useUnsavedChangesGuard(hasUnsaved);

  // «Volver» regresa al listado de origen (`state.from`, como UserConfigPage), no al historial: cada
  // pestaña visitada añade una entrada y `navigate(-1)` retrocedería de pestaña en pestaña. Sin origen
  // (enlace directo) se usa el listado del rol del socio.
  const returnTo =
    (location.state as { from?: string } | null)?.from ??
    (supplierRole && !customerRole ? "/suppliers" : "/customers");

  // ── Derived ────────────────────────────────────────────────────────────────

  const activeRoles = bp?.roles.filter((r) => r.isActive) ?? [];
  const activeLocs = locations.filter((l) => l.isActive);
  const activeCons = contacts.filter((c) => c.isActive);
  const counts: Partial<Record<BpTabId, number>> = {
    roles: activeRoles.length,
    locations: activeLocs.length,
    contacts: activeCons.length,
  };

  // El contenedor del panel existe siempre (las relaciones `aria-controls`/`aria-labelledby` de cada
  // pestaña deben apuntar a un elemento real); su contenido se monta al visitarla y se conserva.
  const renderPanel = (tabId: BpTabId, content: () => React.ReactNode) =>
    available.includes(tabId) ? (
      <div
        key={tabId}
        role="tabpanel"
        id={`bp-panel-${tabId}`}
        aria-labelledby={`bp-tab-${tabId}`}
        hidden={tabId !== activeTab}
        className="prd-tab-content"
      >
        {visited.has(tabId) || tabId === activeTab ? content() : null}
      </div>
    ) : null;

  return (
    <ErpPageTemplate
      kicker={t("masterdata.kicker", "Clientes y proveedores")}
      title={bp ? bp.tradeName?.trim() || bp.legalName : t("masterdata.bp.detail.title.fallback", "Socio de negocio")}
      subtitle={bp ? `${bp.identificationType} ${bp.identificationNumber}` : ""}
      action={
        <ZHBtn variant="ghost" onClick={() => navigate(returnTo)}>
          ← {t("common.back", "Volver")}
        </ZHBtn>
      }
    >
      {loading && <p>{t("masterdata.bp.detail.loading", "Cargando…")}</p>}
      {error && <ZHPageNotice variant="error" message={error} />}
      {actionError && <ZHPageNotice variant="error" message={actionError} />}

      {bp && (
        <>
          <div className="md-detail-status-row">
            <Badge
              variant={bp.isActive ? "success" : "warning"}
              label={
                bp.isActive
                  ? t("masterdata.bp.detail.status.active", "Activo")
                  : t("masterdata.bp.detail.status.inactive", "Inactivo")
              }
            />
            {activeRoles.map((r) => (
              <Badge key={r.id} variant="success" label={r.roleLabel} />
            ))}
          </div>

          <ZHTabBar
            ariaLabel={t("masterdata.bp.detail.tabs.aria", "Secciones de la ficha")}
            idPrefix="bp-tab"
            panelIdPrefix="bp-panel"
            tabs={available.map((tabId) => ({
              id: tabId,
              label: t(TAB_LABELS[tabId][0], TAB_LABELS[tabId][1]),
              count: counts[tabId],
            }))}
            activeTab={activeTab}
            onChange={(tabId) => setSearchParams({ tab: tabId }, { state: location.state })}
          />

          {renderPanel("summary", () => (
            <BpSummarySection bp={bp} locations={locations} contacts={contacts} />
          ))}
          {renderPanel("general", () => (
            <BpGeneralSection
              bp={bp}
              canUpdate={canUpdate}
              onReload={reloadQuiet}
              onDirtyChange={(d) => reportDirty("general", d)}
            />
          ))}
          {supplierRole &&
            renderPanel("supplier", () => (
              <BpSupplierSection
                key={`supplier-${companySessionVersion}`}
                bpId={bp.id}
                role={supplierRole}
                canUpdate={canUpdate}
                canConfigure={canConfigure}
                onReload={reloadQuiet}
                onDirtyChange={reportDirty}
              />
            ))}
          {customerRole &&
            renderPanel("customer", () => (
              <BpCustomerSection
                key={`customer-${companySessionVersion}`}
                bpId={bp.id}
                role={customerRole}
                canUpdate={canUpdate}
                canConfigure={canConfigure}
                onReload={reloadQuiet}
                onDirtyChange={reportDirty}
              />
            ))}
          {renderPanel("locations", () => (
            <BpLocationsSection
              bpId={bp.id}
              visible
              locations={locations}
              canUpdate={canUpdate}
              canDisable={canDisable}
              saving={saving}
              onSavingChange={setSaving}
              actionError={actionError}
              onActionError={setActionError}
              onReload={reloadQuiet}
            />
          ))}
          {renderPanel("contacts", () => (
            <BpContactsSection
              bpId={bp.id}
              visible
              contacts={contacts}
              activeLocations={activeLocs}
              canUpdate={canUpdate}
              canDisable={canDisable}
              saving={saving}
              onSavingChange={setSaving}
              actionError={actionError}
              onActionError={setActionError}
              onReload={reloadQuiet}
            />
          ))}
          {renderPanel("roles", () => (
            <BpRolesSection
              bp={bp}
              canDisable={canDisable}
              saving={saving}
              onSavingChange={setSaving}
              onActionError={setActionError}
              onReload={reloadQuiet}
            />
          ))}
        </>
      )}
    </ErpPageTemplate>
  );
}
