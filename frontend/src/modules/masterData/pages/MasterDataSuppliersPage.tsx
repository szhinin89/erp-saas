import { useCallback, useEffect, useRef } from "react";
import { NoAccessPage } from "../../../components/PageShell";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHTabBar } from "../../../components/zh/ZHTabBar";
import { useI18n } from "../../../i18n/i18n";
import { useMasterDataSuppliersPage } from "./useMasterDataSuppliersPage";
import { MasterDataPartnerWizard } from "../components/MasterDataPartnerWizard";
import { MasterDataPartnerResumenTab } from "../components/MasterDataPartnerResumenTab";
import { MasterDataPartnerListTab } from "../components/MasterDataPartnerListTab";
import { useMasterDataSuppliersUiStore } from "../store/masterDataPartnerUiStore";
import { message } from "../../../lib/messages";
import type { CreateBusinessPartnerBody } from "../types/businessPartner.types";
import { formatApiRequestError } from "../../lib/apiError";
import "../../../styles/shared/items-catalog.css";
import "./masterdata-pages.css";

const TABS = [
  {
    id: "resumen" as const,
    labelKey: "masterdata.suppliers.tabs.resumen",
    labelFb: "Resumen",
    icon: "bar_chart_4_bars",
  },
  {
    id: "listado" as const,
    labelKey: "masterdata.suppliers.tabs.listado",
    labelFb: "Listado",
    icon: "view_list",
  },
  {
    id: "nuevo" as const,
    labelKey: "masterdata.suppliers.tabs.nuevo",
    labelFb: "Nuevo proveedor",
    icon: "add_box",
  },
] as const;

const DRAFT_KEY = "erp.masterdata.suppliers.draft";

export function MasterDataSuppliersPage() {
  const { t } = useI18n();
  const page = useMasterDataSuppliersPage();
  const ui = useMasterDataSuppliersUiStore;

  const activeTab = ui((s) => s.activeTab);
  const setActiveTab = ui((s) => s.setActiveTab);
  const showList = ui((s) => s.showList);
  const addActivity = ui((s) => s.addActivity);
  const reset = ui((s) => s.reset);
  const searchRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    reset();
    return () => reset();
  }, [reset]);

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key === "k") {
        e.preventDefault();
        setActiveTab("listado");
        setTimeout(() => searchRef.current?.focus(), 80);
      }
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, [setActiveTab]);

  const handleCreate = useCallback(
    async (
      body: CreateBusinessPartnerBody,
      supplierConfig?: {
        refundProviderTypeCode: string;
      },
    ): Promise<void> => {
      await page.createSupplier(body, supplierConfig);
      addActivity(body.legalName, "created");
      message.success("Proveedor creado correctamente.");
      setActiveTab("listado");
    },
    [page, addActivity, setActiveTab],
  );

  const handleAssign = useCallback(
    async (id: string): Promise<void> => {
      await page.assignAsSupplier(id);
      const bp = page.suppliers.find((s) => s.id === id);
      addActivity(bp?.legalName ?? id, "assigned");
      message.success("Rol de proveedor asignado correctamente.");
      setActiveTab("listado");
    },
    [page, addActivity, setActiveTab],
  );

  // CRITICAL-CONFIRMATIONS-BUSINESS-PARTNERS-04: antes se mostraba éxito aunque
  // page.disableSupplier/activateSupplier fallaran internamente (el hook no relanzaba el error).
  // Ahora sí relanza, así que message.success solo corre si el await no lanzó — y se agrega
  // confirmación previa explicando el impacto real.
  const handleDisable = useCallback(
    async (id: string) => {
      const bp = page.suppliers.find((s) => s.id === id);
      const confirmed = await message.confirm({
        title: "Desactivar proveedor",
        message: bp
          ? `"${bp.legalName}" dejará de estar disponible para operaciones futuras (nuevas compras, nuevos documentos). El histórico existente no se elimina.`
          : "El proveedor dejará de estar disponible para operaciones futuras. El histórico existente no se elimina.",
        variant: "danger",
        confirmLabel: "Desactivar",
        cancelLabel: "Cancelar",
      });
      if (!confirmed) return;

      try {
        await page.disableSupplier(id);
        if (bp) addActivity(bp.legalName, "disabled");
        message.success("Proveedor desactivado correctamente.");
      } catch (err) {
        message.error(
          formatApiRequestError(err, {
            generic: "No se pudo desactivar el proveedor.",
          }),
        );
      }
    },
    [page, addActivity],
  );

  const handleActivate = useCallback(
    async (id: string) => {
      const bp = page.suppliers.find((s) => s.id === id);
      const confirmed = await message.confirm({
        title: "Activar proveedor",
        message: bp
          ? `"${bp.legalName}" volverá a estar disponible para operaciones futuras (nuevas compras, nuevos documentos).`
          : "El proveedor volverá a estar disponible para operaciones futuras.",
        variant: "warning",
        confirmLabel: "Activar",
        cancelLabel: "Cancelar",
      });
      if (!confirmed) return;

      try {
        await page.activateSupplier(id);
        if (bp) addActivity(bp.legalName, "enabled");
        message.success("Proveedor activado correctamente.");
      } catch (err) {
        message.error(
          formatApiRequestError(err, {
            generic: "No se pudo activar el proveedor.",
          }),
        );
      }
    },
    [page, addActivity],
  );

  if (!page.canView)
    return <NoAccessPage title={t("masterdata.suppliers.title")} />;

  return (
    <ErpPageTemplate
      pageClassName="md-suppliers-page"
      title={t("masterdata.suppliers.title")}
      subtitle={t("masterdata.suppliers.subtitle")}
    >
      {page.listError && (
        <ZHPageNotice variant="error" message={page.listError} />
      )}
      {page.inlineError && (
        <ZHPageNotice variant="error" message={page.inlineError} />
      )}

      <ZHTabBar
        tabs={TABS.map((tab) => ({
          id: tab.id,
          label: t(tab.labelKey, tab.labelFb),
          mobileLabel: tab.id === "nuevo"
            ? t("masterdata.suppliers.tabs.nuevoShort", "Nuevo")
            : undefined,
          icon: tab.icon,
        }))}
        activeTab={activeTab}
        onChange={setActiveTab}
      />

      <div className="prd-tab-content">
        {activeTab === "resumen" && (
          <MasterDataPartnerResumenTab
            role="supplier"
            partners={page.suppliers}
            totalCount={page.totalCount}
            store={ui}
          />
        )}
        {activeTab === "listado" && (
          <MasterDataPartnerListTab
            role="supplier"
            canCreate={page.canCreate}
            canUpdate={page.canUpdate}
            canDisable={page.canDisable}
            loading={page.loading}
            saving={page.saving}
            partners={page.suppliers}
            totalCount={page.totalCount}
            search={page.search}
            setSearch={page.setSearch}
            statusFilter={page.statusFilter}
            setStatusFilter={page.setStatusFilter}
            page={page.page}
            pageSize={page.pageSize}
            totalPages={page.totalPages}
            setPage={page.setPage}
            searchInputRef={searchRef}
            onAddAsCustomer={(id) => void page.addAsCustomer(id)}
            onActivate={handleActivate}
            onDisable={handleDisable}
          />
        )}
        {activeTab === "nuevo" && page.canCreate && (
          <MasterDataPartnerWizard
            role="supplier"
            draftKey={DRAFT_KEY}
            submitting={page.saving}
            editingPartner={null}
            onSubmitCreate={handleCreate}
            onAssignRole={handleAssign}
            onCancel={showList}
          />
        )}
      </div>
    </ErpPageTemplate>
  );
}
