import { useCallback, useEffect, useRef } from "react";
import { NoAccessPage } from "../../../components/PageShell";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHTabBar } from "../../../components/zh/ZHTabBar";
import { useI18n } from "../../../i18n/i18n";
import { useMasterDataCustomersPage } from "./useMasterDataCustomersPage";
import { MasterDataSalesSettingsModal } from "./MasterDataSalesSettingsModal";
import { MasterDataCustomerConfigModal } from "../components/MasterDataCustomerConfigModal";
import { MasterDataPartnerWizard } from "../components/MasterDataPartnerWizard";
import { MasterDataPartnerResumenTab } from "../components/MasterDataPartnerResumenTab";
import { MasterDataPartnerListTab } from "../components/MasterDataPartnerListTab";
import { useMasterDataCustomersUiStore } from "../store/masterDataPartnerUiStore";
import { message } from "../../../lib/messages";
import { formatApiRequestError } from "../../lib/apiError";
import type {
  CreateBusinessPartnerBody,
  UpdateBusinessPartnerBody,
} from "../types/businessPartner.types";
import "../../../styles/shared/items-catalog.css";
import "./masterdata-pages.css";
const TABS = [
  {
    id: "resumen" as const,
    labelKey: "masterdata.customers.tabs.resumen",
    labelFb: "Resumen",
    icon: "bar_chart_4_bars",
  },
  {
    id: "listado" as const,
    labelKey: "masterdata.customers.tabs.listado",
    labelFb: "Listado",
    icon: "view_list",
  },
  {
    id: "nuevo" as const,
    labelKey: "masterdata.customers.tabs.nuevo",
    labelFb: "Nuevo cliente",
    icon: "add_box",
  },
] as const;

const DRAFT_KEY = "erp.masterdata.customers.draft";

export function MasterDataCustomersPage() {
  const { t } = useI18n();
  const page = useMasterDataCustomersPage();
  const ui = useMasterDataCustomersUiStore;

  const activeTab = ui((s) => s.activeTab);
  const editingPartner = ui((s) => s.editingPartner);
  const setActiveTab = ui((s) => s.setActiveTab);
  const cancelEdit = ui((s) => s.cancelEdit);
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
    async (body: CreateBusinessPartnerBody): Promise<void> => {
      await page.createCustomer(body);
      addActivity(body.legalName, "created");
      message.success(
        t(
          "masterdata.customers.created.success",
          "Cliente creado correctamente.",
        ),
      );
      setActiveTab("listado");
    },
    [page, addActivity, setActiveTab, t],
  );

  const handleUpdate = useCallback(
    async (body: UpdateBusinessPartnerBody): Promise<void> => {
      if (!editingPartner) return;
      await page.updateCustomer(editingPartner.id, body);
      addActivity(body.legalName, "updated");
      message.success(
        t(
          "masterdata.customers.updated.success",
          "Cliente actualizado correctamente.",
        ),
      );
      cancelEdit();
    },
    [editingPartner, page, addActivity, cancelEdit, t],
  );

  const handleAssign = useCallback(
    async (id: string): Promise<void> => {
      await page.assignAsCustomer(id);
      const bp = page.customers.find((c) => c.id === id);
      addActivity(bp?.legalName ?? id, "assigned");
      message.success(
        t(
          "masterdata.customers.assigned.success",
          "Rol de cliente asignado correctamente.",
        ),
      );
      setActiveTab("listado");
    },
    [page, addActivity, setActiveTab, t],
  );

  // CRITICAL-CONFIRMATIONS-BUSINESS-PARTNERS-04: antes se mostraba éxito aunque
  // page.disableCustomer/activateCustomer fallaran internamente (el hook no relanzaba el error).
  // Ahora sí relanza, así que message.success solo corre si el await no lanzó — y se agrega
  // confirmación previa explicando el impacto real.
  const handleDisable = useCallback(
    async (id: string) => {
      const bp = page.customers.find((c) => c.id === id);
      const confirmed = await message.confirm({
        title: "Desactivar cliente",
        message: bp
          ? `"${bp.legalName}" dejará de estar disponible para operaciones futuras (nuevas ventas, nuevos documentos). El histórico existente no se elimina.`
          : "El cliente dejará de estar disponible para operaciones futuras. El histórico existente no se elimina.",
        variant: "danger",
        confirmLabel: "Desactivar",
        cancelLabel: "Cancelar",
      });
      if (!confirmed) return;

      try {
        await page.disableCustomer(id);
        if (bp) addActivity(bp.legalName, "disabled");
        message.success("Cliente desactivado correctamente.");
      } catch (err) {
        message.error(
          formatApiRequestError(err, {
            generic: "No se pudo desactivar el cliente.",
          }),
        );
      }
    },
    [page, addActivity],
  );

  const handleActivate = useCallback(
    async (id: string) => {
      const bp = page.customers.find((c) => c.id === id);
      const confirmed = await message.confirm({
        title: "Activar cliente",
        message: bp
          ? `"${bp.legalName}" volverá a estar disponible para operaciones futuras (nuevas ventas, nuevos documentos).`
          : "El cliente volverá a estar disponible para operaciones futuras.",
        variant: "warning",
        confirmLabel: "Activar",
        cancelLabel: "Cancelar",
      });
      if (!confirmed) return;

      try {
        await page.activateCustomer(id);
        if (bp) addActivity(bp.legalName, "enabled");
        message.success("Cliente activado correctamente.");
      } catch (err) {
        message.error(
          formatApiRequestError(err, {
            generic: "No se pudo activar el cliente.",
          }),
        );
      }
    },
    [page, addActivity],
  );

  if (!page.canView)
    return <NoAccessPage title={t("masterdata.customers.title")} />;

  return (
    <ErpPageTemplate
      title={t("masterdata.customers.title")}
      subtitle={t("masterdata.customers.subtitle")}
    >
      {page.listError && (
        <ZHPageNotice variant="error" message={page.listError} />
      )}
      {page.inlineError && (
        <ZHPageNotice variant="error" message={page.inlineError} />
      )}

      <ZHTabBar
        variant="responsive-three"
        ariaLabel={t("masterdata.customers.tabs.aria", "Secciones de clientes")}
        tabs={TABS.map((tab) => ({
          id: tab.id,
          label: t(tab.labelKey, tab.labelFb),
          mobileLabel: tab.id === "nuevo"
            ? t("masterdata.customers.tabs.nuevoShort", "Nuevo")
            : undefined,
          icon: tab.icon,
        }))}
        activeTab={activeTab}
        onChange={setActiveTab}
      />

      <div className="prd-tab-content">
        {activeTab === "resumen" && (
          <MasterDataPartnerResumenTab
            role="customer"
            partners={page.customers}
            totalCount={page.totalCount}
            store={ui}
          />
        )}
        {activeTab === "listado" && (
          <MasterDataPartnerListTab
            role="customer"
            store={ui}
            canCreate={page.canCreate}
            canUpdate={page.canUpdate}
            canDisable={page.canDisable}
            canConfigure={page.canConfigure}
            loading={page.loading}
            saving={page.saving}
            partners={page.customers}
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
            onSettings={(bp) => void page.openSettings(bp)}
            onCustomerConfig={
              page.canUpdate
                ? (bp) => void page.openCustomerConfig(bp)
                : undefined
            }
            onAddAsSupplier={(id) => void page.addAsSupplier(id)}
            onActivate={handleActivate}
            onDisable={handleDisable}
          />
        )}
        {activeTab === "nuevo" && (page.canCreate || editingPartner) && (
          <MasterDataPartnerWizard
            key={editingPartner?.id ?? "create"}
            role="customer"
            draftKey={DRAFT_KEY}
            submitting={page.saving}
            editingPartner={editingPartner}
            onSubmitCreate={handleCreate}
            onSubmitUpdate={handleUpdate}
            onAssignRole={handleAssign}
            onCancel={cancelEdit}
          />
        )}
      </div>

      {page.settingsBp && page.canConfigure && (
        <MasterDataSalesSettingsModal
          partner={page.settingsBp}
          initialSettings={page.settingsData}
          saving={page.saving}
          error={page.modalError}
          onClose={page.closeSettings}
          onSave={(payload) => page.saveSettings(page.settingsBp!.id, payload)}
        />
      )}

      {page.customerConfigBp && (
        <MasterDataCustomerConfigModal
          key={page.customerConfigBp.roleId}
          bpName={page.customerConfigBp.bp.legalName}
          initialConfig={page.customerConfigBp.config}
          saving={page.saving}
          error={page.modalError}
          onClose={page.closeCustomerConfig}
          onSave={(body) =>
            void page.saveCustomerConfig(
              page.customerConfigBp!.bp.id,
              page.customerConfigBp!.roleId,
              body,
            )
          }
        />
      )}
    </ErpPageTemplate>
  );
}
