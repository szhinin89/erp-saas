import { useMemo, useState } from "react";
import { useForm, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import {
  EmptyState,
  LoadingState,
  NoAccessPage,
  Badge,
} from "../../../../components/PageShell";
import { ErpPageTemplate } from "../../../../templates/ErpPageTemplate";
import { ReportKpiCard } from "../../../../components/ReportPageTemplate";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHBtn, ZHField, ZHGrid } from "../../../../components/zh/ZHForm";
import { ZHIconButton } from "../../../../components/zh/ZHIconButton";
import { ZHModal } from "../../../../components/zh/ZHModal";
import { ZhPhoneInput, ZhTextInput } from "../../../../components/zh/inputs";
import { useI18n } from "../../../../i18n/i18n";
import type { Carrier } from "../api/carrierService";
import { useCarriers } from "../hooks/useCarriers";
import {
  carrierSchema,
  defaultCarrierValues,
  type CarrierFormValues,
} from "../schemas/carrierSchema";
import "./carriers-page.css";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";

const ID_TYPES = ["RUC", "CI", "PASSPORT"] as const;

export function CarriersPage() {
  const { canShow } = usePermissionsUi();
  const { t } = useI18n();
  const canView = canShow("logistics.carriers.view");
  const canCreate = canShow("logistics.carriers.create");
  const canEdit = canShow("logistics.carriers.update") || canCreate;

  /* â”€â”€ State â”€â”€ */
  const [searchQuery, setSearchQuery] = useState("");
  const [statusFilter, setStatusFilter] = useState<
    "all" | "active" | "inactive"
  >("all");
  const [modalOpen, setModalOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);

  /* â”€â”€ Data â”€â”€ */
  const {
    carriers,
    loading,
    error,
    saving,
    saveError,
    createCarrier,
    updateCarrier,
    toggleCarrierStatus,
  } = useCarriers();

  const {
    register,
    handleSubmit,
    reset,
    setValue,
    control,
    formState: { errors },
  } = useForm<CarrierFormValues>({
    resolver: zodResolver(carrierSchema),
    defaultValues: defaultCarrierValues,
  });

  /* â”€â”€ Derived â”€â”€ */
  const filtered = useMemo(() => {
    let list = carriers;
    if (statusFilter === "active") list = list.filter((c) => c.isActive);
    if (statusFilter === "inactive") list = list.filter((c) => !c.isActive);
    const term = searchQuery.trim().toLowerCase();
    if (!term) return list;
    return list.filter(
      (c) =>
        c.legalName.toLowerCase().includes(term) ||
        c.identificationNumber.toLowerCase().includes(term) ||
        c.licensePlate.toLowerCase().includes(term) ||
        (c.email ?? "").toLowerCase().includes(term),
    );
  }, [carriers, searchQuery, statusFilter]);

  const totals = useMemo(
    () => ({
      total: carriers.length,
      active: carriers.filter((c) => c.isActive).length,
      inactive: carriers.filter((c) => !c.isActive).length,
    }),
    [carriers],
  );

  /* â”€â”€ Modal helpers â”€â”€ */
  const openCreate = () => {
    setEditingId(null);
    reset(defaultCarrierValues);
    setModalOpen(true);
  };

  const openEdit = (carrier: Carrier) => {
    setEditingId(carrier.id);
    setValue("identificationType", carrier.identificationType);
    setValue("identificationNumber", carrier.identificationNumber);
    setValue("legalName", carrier.legalName);
    setValue("licensePlate", carrier.licensePlate);
    setValue("phone", carrier.phone ?? "");
    setValue("email", carrier.email ?? "");
    setModalOpen(true);
  };

  const closeModal = () => {
    setModalOpen(false);
    setEditingId(null);
    reset(defaultCarrierValues);
  };

  const onSubmit = handleSubmit(async (values) => {
    const payload = {
      identificationType: values.identificationType,
      identificationNumber: values.identificationNumber,
      legalName: values.legalName,
      licensePlate: values.licensePlate,
      phone: values.phone || null,
      email: values.email || null,
    };
    const result = editingId
      ? await updateCarrier(editingId, payload)
      : await createCarrier(payload);
    if (result) closeModal();
  });

  const handleToggle = async (carrier: Carrier) => {
    if (!canEdit) return;
    await toggleCarrierStatus(carrier.id, !carrier.isActive);
  };

  if (!canView) return <NoAccessPage title={t("carriers.title")} />;

  return (
    <ErpPageTemplate
      kicker={t("carriers.kicker")}
      title={t("carriers.title")}
      subtitle={t("carriers.subtitle")}
      action={
        canCreate ? (
          <ZHBtn
            variant="primary"
            size="md"
            type="button"
            disabled={saving}
            onClick={openCreate}
          >
            <span className="material-symbols-outlined">add</span>
            {t("carriers.new")}
          </ZHBtn>
        ) : undefined
      }
    >
      {/* â”€â”€ Errors â”€â”€ */}
      {error && (
        <ZHPageNotice
          variant="error"
          message={t("common.errorPrefix")}
          detail={error}
        />
      )}
      {saveError && (
        <ZHPageNotice
          variant="error"
          message={t("common.errorPrefix")}
          detail={saveError}
        />
      )}

      {/* â”€â”€ KPI cards â”€â”€ */}
      <div className="pg-kpis">
        <ReportKpiCard
          layout="horizontal"
          icon="local_shipping"
          tone="primary"
          label={t("carriers.kpi.total")}
          value={String(totals.total)}
        />
        <ReportKpiCard
          layout="horizontal"
          icon="check_circle"
          tone="success"
          label={t("carriers.kpi.active")}
          value={String(totals.active)}
        />
        <ReportKpiCard
          layout="horizontal"
          icon="do_not_disturb"
          tone="error"
          label={t("carriers.kpi.inactive")}
          value={String(totals.inactive)}
        />
      </div>

      {/* â”€â”€ Table section â”€â”€ */}
      <div className="pg-section">
        {/* Filter bar */}
        <div className="pg-table-controls">
          <div className="pg-table-controls-left">
            <div className="pg-search">
              <span className="material-symbols-outlined">search</span>
              <input
                className="zh-input"
                type="search"
                placeholder={t("carriers.search.placeholder")}
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
              />
            </div>
            <select
              className="zh-input"
              value={statusFilter}
              onChange={(e) =>
                setStatusFilter(e.target.value as typeof statusFilter)
              }
            >
              <option value="all">{t("carriers.filter.all")}</option>
              <option value="active">{t("carriers.filter.active")}</option>
              <option value="inactive">{t("carriers.filter.inactive")}</option>
            </select>
          </div>
          <div className="pg-table-controls-right">
            <span className="pg-result-count">
              {filtered.length} / {carriers.length}{" "}
              {t("carriers.kpi.total").toLowerCase()}
            </span>
          </div>
        </div>

        {/* Table */}
        {loading ? (
          <LoadingState />
        ) : filtered.length === 0 ? (
          <EmptyState message={t("common.noData")} />
        ) : (
          <div className="table-scroll">
            <table className="table">
              <thead>
                <tr>
                  <th>{t("carriers.table.idType")}</th>
                  <th>{t("carriers.table.identification")}</th>
                  <th>{t("carriers.table.legalName")}</th>
                  <th>{t("carriers.table.licensePlate")}</th>
                  <th>{t("carriers.table.phone")}</th>
                  <th>{t("carriers.table.email")}</th>
                  <th>{t("carriers.table.status")}</th>
                  <th>{t("carriers.table.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {filtered.map((carrier) => (
                  <tr key={carrier.id}>
                    <td>
                      <Badge
                        label={t(
                          `carriers.idType.${carrier.identificationType}`,
                        )}
                        variant="neutral"
                        upper
                      />
                    </td>
                    <td className="mono">{carrier.identificationNumber}</td>
                    <td className="crt-col-name">{carrier.legalName}</td>
                    <td>
                      <Badge
                        label={carrier.licensePlate}
                        variant="info"
                        className="mono"
                      />
                    </td>
                    <td className="subtle">{carrier.phone ?? "â€”"}</td>
                    <td className="subtle">{carrier.email ?? "â€”"}</td>
                    <td>
                      <span
                        className={
                          carrier.isActive
                            ? "zh-status zh-status--active"
                            : "zh-status zh-status--inactive"
                        }
                      >
                        {carrier.isActive
                          ? t("carriers.status.active")
                          : t("carriers.status.inactive")}
                      </span>
                    </td>
                    <td>
                      <div className="crt-actions-cell">
                        <ZHBtn
                          type="button"
                          variant="ghost"
                          size="sm"
                          onClick={() => openEdit(carrier)}
                          disabled={!canEdit}
                          aria-label="Edit"
                        >
                          <span className="material-symbols-outlined">
                            edit
                          </span>
                        </ZHBtn>
                        <ZHIconButton
                          icon={carrier.isActive ? "block" : "check_circle"}
                          variant={carrier.isActive ? "danger" : "success"}
                          title={carrier.isActive ? "Disable" : "Enable"}
                          onClick={() => void handleToggle(carrier)}
                          disabled={!canEdit || saving}
                        />
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <ZHModal
        open={modalOpen}
        onClose={closeModal}
        size="md"
        title={
          editingId
            ? t("carriers.modal.editTitle")
            : t("carriers.modal.createTitle")
        }
        subtitle={t("carriers.subtitle", "GestiÃ³n de transportistas.")}
        footer={
          <div className="pg-actions-buttons">
            <ZHBtn variant="ghost" size="md" type="button" onClick={closeModal}>
              {t("common.cancel")}
            </ZHBtn>
            <ZHBtn
              variant="primary"
              size="md"
              type="submit"
              form="carrier-form"
              disabled={saving}
            >
              {saving ? t("common.saving") : t("common.save")}
            </ZHBtn>
          </div>
        }
      >
        <form id="carrier-form" onSubmit={onSubmit}>
          <ZHGrid cols={2}>
            <ZHField
              label={t("carriers.modal.idType")}
              required
              error={
                errors.identificationType?.message
                  ? t(errors.identificationType.message)
                  : undefined
              }
            >
              <select disabled={saving} {...register("identificationType")}>
                {ID_TYPES.map((type) => (
                  <option key={type} value={type}>
                    {t(`carriers.idType.${type}`)}
                  </option>
                ))}
              </select>
            </ZHField>
            <ZHField
              label={t("carriers.modal.idNumber")}
              required
              error={
                errors.identificationNumber?.message
                  ? t(errors.identificationNumber.message)
                  : undefined
              }
            >
              <input
                className="zh-input"
                placeholder={t("carriers.modal.idNumberPlaceholder")}
                disabled={saving}
                {...register("identificationNumber")}
              />
            </ZHField>
          </ZHGrid>

          <ZHField
            label={t("carriers.modal.legalName")}
            required
            error={
              errors.legalName?.message
                ? t(errors.legalName.message)
                : undefined
            }
          >
            <input
              className="zh-input"
              placeholder={t("carriers.modal.legalNamePlaceholder")}
              disabled={saving}
              {...register("legalName")}
            />
          </ZHField>

          <ZHGrid cols={2}>
            <ZHField
              label={t("carriers.modal.licensePlate")}
              required
              error={
                errors.licensePlate?.message
                  ? t(errors.licensePlate.message)
                  : undefined
              }
            >
              <ZhTextInput
                mode="uppercase"
                placeholder={t("carriers.modal.licensePlatePlaceholder")}
                disabled={saving}
                {...register("licensePlate")}
              />
            </ZHField>
            <ZHField
              label={t("carriers.modal.phone")}
              error={errors.phone?.message}
            >
              <Controller
                name="phone"
                control={control}
                render={({ field }) => (
                  <ZhPhoneInput {...field} disabled={saving} />
                )}
              />
            </ZHField>
          </ZHGrid>

          <ZHField
            label={t("carriers.modal.email")}
            error={errors.email?.message ? t(errors.email.message) : undefined}
          >
            <input
              className="zh-input"
              type="email"
              placeholder={t("carriers.modal.emailPlaceholder")}
              disabled={saving}
              {...register("email")}
            />
          </ZHField>
        </form>
      </ZHModal>
    </ErpPageTemplate>
  );
}

