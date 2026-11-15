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
import { describeCatalogCode } from "../api/catalogDescription";
import { useSriIdTypes } from "../api/useSriIdTypes";
import { BpSummarySection } from "../components/BpSummarySection";
import { BpGeneralSection } from "../components/BpGeneralSection";
import { BpSupplierSection } from "../components/BpSupplierSection";
import { BpCustomerSection } from "../components/BpCustomerSection";
import { BpLocationsSection } from "../components/BpLocationsSection";
import { BpContactsSection } from "../components/BpContactsSection";
import {
  BP_CONTEXT_LIST_PATH,
  BP_CONTEXT_ROLE,
  resolveBpTab,
  tabsForContext,
  type BpContext,
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
  sri: ["masterdata.bp.detail.tabs.sri", "Configuración SRI"],
  purchases: ["masterdata.bp.detail.tabs.purchases", "Compras"],
  retentions: ["masterdata.bp.detail.tabs.retentions", "Retenciones"],
  expenseSubcategories: ["masterdata.bp.detail.tabs.expenseSubcategories", "Subcategorías de gasto"],
  classification: ["masterdata.bp.detail.tabs.classification", "Clasificación"],
  sales: ["masterdata.bp.detail.tabs.sales", "Condiciones de ventas"],
  locations: ["masterdata.bp.detail.tabs.locations", "Ubicaciones"],
  contacts: ["masterdata.bp.detail.tabs.contacts", "Contactos"],
};

const SUPPLIER_CONFIG_PARTS = ["sri", "purchases", "retentions", "expenseSubcategories"] as const;
const CUSTOMER_CONFIG_PARTS = ["classification", "sales"] as const;

/**
 * Ficha de administración de un Business Partner en el contexto de un módulo: `/suppliers/:id`
 * (Proveedor) o `/customers/:id` (Cliente). Una única estructura parametrizada por `context`: cada
 * contexto muestra solo sus pestañas y los datos de su rol (sin mezclar Proveedor con Cliente ni
 * roles). Cada pestaña compone los componentes existentes con permisos y guardados independientes.
 * `?tab=` permite enlaces profundos; una pestaña ajena al contexto cae en Resumen. Las pestañas
 * visitadas permanecen montadas para no perder cambios sin guardar. Si el socio no tiene el rol
 * activo del contexto no se muestra la ficha (aviso + volver al listado).
 */
export function MasterDataBusinessPartnerDetailPage({ context }: { context: BpContext }) {
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
  const isSupplier = context === "supplier";
  const { options: idTypes, loading: idTypesLoading } = useSriIdTypes();

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

  const contextRole = bp?.roles.find(
    (r) => r.isActive && r.roleType === BP_CONTEXT_ROLE[context],
  );
  const available = useMemo(
    () => (contextRole ? tabsForContext(context) : []),
    [contextRole, context],
  );
  const requestedTab = searchParams.get("tab");
  const activeTab = resolveBpTab(requestedTab, available);

  // Con la ficha cargada, un `?tab=` inválido o ajeno al contexto se normaliza a Resumen.
  useEffect(() => {
    if (contextRole && requestedTab !== null && requestedTab !== activeTab)
      setSearchParams({ tab: activeTab }, { replace: true, state: location.state });
  }, [contextRole, requestedTab, activeTab, setSearchParams, location.state]);

  // Solo se registra una pestaña realmente disponible (antes de cargar no hay pestañas).
  useEffect(() => {
    if (!contextRole) return;
    setVisited((prev) =>
      prev.has(activeTab) ? prev : new Set(prev).add(activeTab),
    );
  }, [contextRole, activeTab]);

  // ── Cambios sin guardar ────────────────────────────────────────────────────

  const reportDirty = useCallback((key: string, dirty: boolean) => {
    setDirtyMap((prev) => (prev[key] === dirty ? prev : { ...prev, [key]: dirty }));
  }, []);
  const hasUnsaved = Boolean(contextRole) && Object.values(dirtyMap).some(Boolean);

  // Bloquea la navegación a otra ruta (menú, Atrás/Adelante, «Volver») con la confirmación oficial,
  // una sola vez por navegación; `?tab=` no cambia de ruta y nunca bloquea. Incluye `beforeunload`.
  useUnsavedChangesGuard(hasUnsaved);

  // «Volver» regresa siempre al listado del contexto, no al historial: cada pestaña visitada añade
  // una entrada y `navigate(-1)` retrocedería de pestaña en pestaña.
  const returnTo = BP_CONTEXT_LIST_PATH[context];
  // El contexto del módulo se identifica siempre en el encabezado (el kicker del shell no se renderiza).
  const contextLabel = isSupplier
    ? t("masterdata.bp.detail.context.supplier", "Administrar proveedor")
    : t("masterdata.bp.detail.context.customer", "Administrar cliente");
  // «04» es el código SRI del tipo de identificación (catálogo sri_id_type): el encabezado es un
  // espacio compacto, por lo que muestra su abreviatura oficial (`shortName`).
  const idTypeName = bp
    ? describeCatalogCode(
        bp.identificationType,
        idTypes,
        idTypesLoading,
        (c) => t("masterdata.bp.detail.unknownCatalog", { code: c }),
        "shortName",
      )
    : null;
  const identificationText = bp
    ? [idTypeName, bp.identificationNumber].filter(Boolean).join(" ")
    : "";
  const backLabel = isSupplier
    ? t("masterdata.bp.detail.backSuppliers", "Volver a proveedores")
    : t("masterdata.bp.detail.backCustomers", "Volver a clientes");

  // ── Derived ────────────────────────────────────────────────────────────────

  const activeLocs = locations.filter((l) => l.isActive);
  const activeCons = contacts.filter((c) => c.isActive);
  const counts: Partial<Record<BpTabId, number>> = {
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
      title={
        bp
          ? bp.tradeName?.trim() || bp.legalName
          : isSupplier
            ? t("masterdata.bp.detail.title.supplier", "Proveedor")
            : t("masterdata.bp.detail.title.customer", "Cliente")
      }
      subtitle={`${contextLabel}${bp ? ` · ${identificationText}` : ""}`}
      action={
        <ZHBtn variant="ghost" onClick={() => navigate(returnTo)}>
          ← {backLabel}
        </ZHBtn>
      }
    >
      {loading && <p>{t("masterdata.bp.detail.loading", "Cargando…")}</p>}
      {error && <ZHPageNotice variant="error" message={error} />}
      {actionError && <ZHPageNotice variant="error" message={actionError} />}

      {bp && !contextRole && (
        <ZHPageNotice
          variant="error"
          message={
            isSupplier
              ? t("masterdata.bp.detail.noRole.supplier", "Este socio de negocio no es un proveedor activo.")
              : t("masterdata.bp.detail.noRole.customer", "Este socio de negocio no es un cliente activo.")
          }
        />
      )}

      {bp && contextRole && (
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
            <BpSummarySection context={context} bp={bp} locations={locations} contacts={contacts} />
          ))}
          {renderPanel("general", () => (
            <BpGeneralSection
              context={context}
              bp={bp}
              canUpdate={canUpdate}
              onReload={reloadQuiet}
              onDirtyChange={(d) => reportDirty("general", d)}
            />
          ))}
          {isSupplier &&
            SUPPLIER_CONFIG_PARTS.map((part) =>
              renderPanel(part, () => (
                <BpSupplierSection
                  key={`supplier-${part}-${companySessionVersion}`}
                  part={part}
                  bpId={bp.id}
                  role={contextRole}
                  canUpdate={canUpdate}
                  canConfigure={canConfigure}
                  onReload={reloadQuiet}
                  onDirtyChange={reportDirty}
                />
              )),
            )}
          {!isSupplier &&
            CUSTOMER_CONFIG_PARTS.map((part) =>
              renderPanel(part, () => (
                <BpCustomerSection
                  key={`customer-${part}-${companySessionVersion}`}
                  part={part}
                  bpId={bp.id}
                  role={contextRole}
                  canUpdate={canUpdate}
                  canConfigure={canConfigure}
                  onReload={reloadQuiet}
                  onDirtyChange={reportDirty}
                />
              )),
            )}
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
        </>
      )}
    </ErpPageTemplate>
  );
}
