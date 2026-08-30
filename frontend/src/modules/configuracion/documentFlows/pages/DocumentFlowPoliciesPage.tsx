import { useCallback, useEffect, useMemo, useState } from "react";
import { useI18n } from "../../../../i18n/i18n";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";
import { Badge, NoAccessPage } from "../../../../components/PageShell";
import { ErpPageTemplate } from "../../../../templates/ErpPageTemplate";
import { ZHBtn, ZHField, ZHGrid, ZHToggle } from "../../../../components/zh/ZHForm";
import { ZhSelect } from "../../../../components/zh/inputs/ZhSelect";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZHDataTable, type ZHDataTableColumn } from "../../../../components/zh/ZHDataTable";
import { ConfigTabsLayout } from "../../../../components/shared/ConfigTabsLayout";
import { message } from "../../../../lib/messages";
import {
  documentFlowPolicyService,
  type AccountingPostingMode,
  type AuthorizationMode,
  type CancellationMode,
  type ConfirmationMode,
  type CreationMode,
  type DocumentFlowPolicyDto,
  type InventoryImpactMode,
  type NotificationMode,
  type PayableGenerationMode,
  type PendingDocumentMode,
} from "../api/documentFlowPolicyService";
import {
  ACCOUNTING_POSTING_MODE_OPTIONS,
  AUTHORIZATION_MODE_OPTIONS,
  CANCELLATION_MODE_OPTIONS,
  CONFIRMATION_MODE_OPTIONS,
  CREATION_MODE_OPTIONS,
  INVENTORY_IMPACT_MODE_OPTIONS,
  IS_ACTIVE_FLAG,
  NOTIFICATION_MODE_OPTIONS,
  PAYABLE_GENERATION_MODE_OPTIONS,
  PENDING_DOCUMENT_MODE_OPTIONS,
  REQUIRES_ATTACHMENT_FLAG,
  REQUIRES_CANCELLATION_REASON_FLAG,
  REQUIRES_DUE_DATE_FLAG,
  REQUIRES_SUPPLIER_FLAG,
  accountingPostingModeOption,
  authorizationModeOption,
  cancellationModeOption,
  compareDocumentCategory,
  confirmationModeOption,
  creationModeOption,
  documentCategory,
  documentTypeDisplayName,
  inventoryImpactModeOption,
  notificationModeOption,
  payableGenerationModeOption,
  pendingDocumentModeOption,
} from "../labels/documentFlowPolicyLabels";

import "../../../../styles/shared/items-catalog.css";
import "./document-flow-policies-page.css";

/**
 * Configuración → Documentos y flujos (DOCUMENT-FLOW-POLICY-01, UX mejorada en
 * DOCUMENT-FLOW-POLICY-UX-01).
 *
 * Auditoría de reutilización previa a escribir esta UI:
 * - Plantillas revisadas: ItemTypesPage (lista→editor con ConfigTabsLayout), ChartOfAccountsPage
 *   (uso canónico de ZHDataTable), la versión previa de esta misma pantalla.
 * - Componentes reutilizados: ErpPageTemplate, ConfigTabsLayout, ZHDataTable, ZHBtn, ZHField
 *   (con `hint`, ya soporta ayuda corta por campo — no se creó markup nuevo para eso), ZHGrid,
 *   ZhSelect, ZHToggle, Badge, ZHPageNotice, NoAccessPage, usePermissionsUi, message — ninguno
 *   nuevo.
 * - Único archivo nuevo: `labels/documentFlowPolicyLabels.ts` (no es un componente de UI, es
 *   el mapa de textos funcionales — no hay componente ZH equivalente para eso).
 *
 * ZH-LISTING-STANDARD-01: el listado principal usa `ZHDataTable` (antes tabla HTML manual),
 * alineado con el estándar del Design System (ver ChartOfAccountsPage). `ZHDataTable` no
 * soporta filas de encabezado agrupadoras — la categoría del documento (Ventas/Compras/
 * Gastos/Inventario/Contabilidad/Tesorería) se muestra como etiqueta dentro de la celda
 * "Documento" en lugar de una fila separadora, para no hackear el tipado ni la accesibilidad
 * de la tabla. Ver reporte de migración para el detalle de esta decisión.
 *
 * ZH-LISTING-PILOT-ROW-NUMBER-01: usa `showRowNumber` de `ZHDataTable` para la numeración
 * visual "N°" — sin columna manual, sin reemplazar ningún dato funcional (código/nombre de
 * documento siguen siendo la identidad real de la fila).
 */
export function DocumentFlowPoliciesPage() {
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canView = canShow("settings.documentFlows.view");
  const canUpdate = canShow("settings.documentFlows.update");

  const [activeTab, setActiveTab] = useState<"list" | "editor">("list");
  const [policies, setPolicies] = useState<DocumentFlowPolicyDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [editing, setEditing] = useState<DocumentFlowPolicyDto | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState("");

  const fetchPolicies = useCallback(async () => {
    setLoading(true);
    try {
      const all = await documentFlowPolicyService.list();
      setPolicies(all);
    } catch {
      /* */
    }
    setLoading(false);
  }, []);

  useEffect(() => {
    void fetchPolicies();
  }, [fetchPolicies]);

  // ZH-LISTING-STANDARD-01: se mantiene el agrupamiento visual por categoría (Ventas/Compras/
  // Gastos/Inventario/Contabilidad/Tesorería) como criterio de orden — ZHDataTable no soporta
  // filas de encabezado agrupadoras, así que la categoría se muestra dentro de la celda
  // "Documento" (ver `renderDocumentCell`) sobre una lista plana ordenada por categoría y nombre.
  const sortedPolicies = useMemo(() => {
    return [...policies].sort((a, b) => {
      const categoryA = documentCategory(a.documentTypeCode);
      const categoryB = documentCategory(b.documentTypeCode);
      const categoryOrder = compareDocumentCategory(categoryA, categoryB);
      if (categoryOrder !== 0) return categoryOrder;
      return documentTypeDisplayName(a.documentTypeCode, a.documentTypeName).localeCompare(
        documentTypeDisplayName(b.documentTypeCode, b.documentTypeName),
      );
    });
  }, [policies]);

  const openEdit = (policy: DocumentFlowPolicyDto) => {
    setEditing(policy);
    setSaveError("");
    setActiveTab("editor");
  };

  const handleCancel = () => {
    setEditing(null);
    setSaveError("");
    setActiveTab("list");
  };

  const updateField = <K extends keyof DocumentFlowPolicyDto>(
    field: K,
    value: DocumentFlowPolicyDto[K],
  ) => {
    setEditing((prev) => (prev ? { ...prev, [field]: value } : prev));
  };

  const handleSave = async () => {
    if (!editing) return;
    setSaveError("");
    setSaving(true);
    try {
      // Guarda exactamente los valores técnicos del enum — la UI solo cambió cómo se
      // muestran (labels/badges/hints), nunca qué se envía al API.
      await documentFlowPolicyService.update(editing.id, {
        id: editing.id,
        isActive: editing.isActive,
        creationMode: editing.creationMode,
        confirmationMode: editing.confirmationMode,
        authorizationMode: editing.authorizationMode,
        pendingDocumentMode: editing.pendingDocumentMode,
        cancellationMode: editing.cancellationMode,
        requiresCancellationReason: editing.requiresCancellationReason,
        requiresAttachment: editing.requiresAttachment,
        requiresSupplier: editing.requiresSupplier,
        requiresDueDate: editing.requiresDueDate,
        payableGenerationMode: editing.payableGenerationMode,
        accountingPostingMode: editing.accountingPostingMode,
        inventoryImpactMode: editing.inventoryImpactMode,
        notificationMode: editing.notificationMode,
      });
      message.success(
        t(
          "documentFlows.updated.success",
          "Flujo documental actualizado correctamente.",
        ),
      );
      setEditing(null);
      setActiveTab("list");
      void fetchPolicies();
    } catch (e: unknown) {
      const err = e as {
        response?: {
          data?: {
            data?: { errors?: Record<string, string[]> };
            message?: { user?: string };
          };
        };
      };
      const fieldErrors = err.response?.data?.data?.errors;
      const msg = fieldErrors
        ? Object.values(fieldErrors).flat().join(" ")
        : (err.response?.data?.message?.user ??
          t("common.saveError", "Error al guardar. Revisa los datos."));
      setSaveError(msg);
    }
    setSaving(false);
  };

  if (!canView)
    return (
      <NoAccessPage
        title={t("documentFlows.title", "Documentos y flujos")}
      />
    );

  const separationNotice = (
    <ZHPageNotice
      variant="info"
      message={t(
        "documentFlows.separationNotice",
        "Esta pantalla define cómo se comporta cada documento en la empresa. Los accesos de usuario se administran en Roles y Permisos.",
      )}
    />
  );

  // t() solo interpola {{param}} cuando el segundo argumento es un objeto Y la clave existe
  // en el diccionario (i18n/locales/*.json) — con fallback string no interpola. Se resuelve
  // aquí una sola vez para evitar repetir la clase de bug de nav-i18n (clave sin traducción
  // registrada mostrando el placeholder crudo en pantalla).
  const editButtonLabel = (documentName: string) =>
    t("documentFlows.editButtonTitle", { document: documentName });

  const effectBadges = (p: DocumentFlowPolicyDto) => {
    const payable = payableGenerationModeOption(p.payableGenerationMode);
    const posting = accountingPostingModeOption(p.accountingPostingMode);
    const inventory = inventoryImpactModeOption(p.inventoryImpactMode);
    return (
      <div className="prd-row-actions dfp-effects">
        <Badge label={payable.summary} variant={payable.badgeVariant} size="md" />
        <Badge label={posting.summary} variant={posting.badgeVariant} size="md" />
        <Badge label={inventory.summary} variant={inventory.badgeVariant} size="md" />
      </div>
    );
  };

  const renderDocumentCell = (p: DocumentFlowPolicyDto) => {
    const displayName = documentTypeDisplayName(p.documentTypeCode, p.documentTypeName);
    const category = documentCategory(p.documentTypeCode);
    const creation = creationModeOption(p.creationMode);
    const confirmation = confirmationModeOption(p.confirmationMode);
    const cancellation = cancellationModeOption(p.cancellationMode);
    return (
      <div className="cfg-document-cell">
        <div className="zh-text-muted zh-text-sm">{category}</div>
        <div>{displayName}</div>
        <div className="zh-text-muted zh-text-sm">
          {creation.summary} · {confirmation.summary} · {cancellation.summary}
        </div>
      </div>
    );
  };

  const columns: ZHDataTableColumn<DocumentFlowPolicyDto>[] = [
    {
      key: "document",
      header: t("documentFlows.col.document", "Documento"),
      render: renderDocumentCell,
    },
    {
      key: "mainFlow",
      header: t("documentFlows.col.mainFlow", "Flujo principal"),
      render: (p) => {
        const creation = creationModeOption(p.creationMode);
        return <Badge label={creation.label} variant={creation.badgeVariant} />;
      },
    },
    {
      key: "confirmation",
      header: t("documentFlows.col.confirmation", "Confirmación"),
      render: (p) => {
        const confirmation = confirmationModeOption(p.confirmationMode);
        return <Badge label={confirmation.label} variant={confirmation.badgeVariant} />;
      },
    },
    {
      key: "cancellation",
      header: t("documentFlows.col.cancellation", "Anulación"),
      render: (p) => {
        const cancellation = cancellationModeOption(p.cancellationMode);
        return <Badge label={cancellation.label} variant={cancellation.badgeVariant} />;
      },
    },
    {
      key: "effects",
      header: t("documentFlows.col.effects", "Efectos"),
      render: effectBadges,
    },
    {
      key: "status",
      header: t("common.status", "Estado"),
      render: (p) => (
        <Badge
          label={p.isActive ? IS_ACTIVE_FLAG.onLabel : IS_ACTIVE_FLAG.offLabel}
          variant={p.isActive ? "success" : "neutral"}
        />
      ),
    },
    {
      key: "actions",
      header: t("common.actions", "Acciones"),
      align: "right",
      render: (p) => {
        const displayName = documentTypeDisplayName(p.documentTypeCode, p.documentTypeName);
        return (
          <div className="prd-row-actions">
            {canUpdate && (
              <ZHBtn
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => openEdit(p)}
                title={editButtonLabel(displayName)}
                aria-label={editButtonLabel(displayName)}
              >
                <span className="material-symbols-outlined zh-icon-lg">edit</span>
              </ZHBtn>
            )}
          </div>
        );
      },
    },
  ];

  const listContent = (
    <>
      {separationNotice}
      <ZHDataTable
        columns={columns}
        rows={sortedPolicies}
        rowKey={(p) => p.id}
        loading={loading}
        showRowNumber
        emptyMessage={t(
          "documentFlows.empty",
          "Sin políticas de flujo documental registradas.",
        )}
      />
    </>
  );

  const editorContent = editing && (
    <div className="pg-section-body">
      {saveError && <ZHPageNotice variant="error" message={saveError} />}
      {separationNotice}

      <ZHField label={t("documentFlows.form.document", "Documento")}>
        <input
          value={documentTypeDisplayName(editing.documentTypeCode, editing.documentTypeName)}
          disabled
        />
      </ZHField>

      <h3 className="zh-section-title">
        {t("documentFlows.section.creation", "Flujo de creación")}
      </h3>
      <ZHGrid cols={2}>
        <ZHField
          label={t("documentFlows.form.creationMode", "Flujo de creación")}
          hint={creationModeOption(editing.creationMode).help}
        >
          <ZhSelect
            value={editing.creationMode}
            disabled={saving}
            onChange={(e) => updateField("creationMode", e.target.value as CreationMode)}
          >
            {CREATION_MODE_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </ZhSelect>
        </ZHField>
      </ZHGrid>

      <h3 className="zh-section-title">
        {t("documentFlows.section.confirmation", "Confirmación y autorización")}
      </h3>
      <ZHGrid cols={2}>
        <ZHField
          label={t("documentFlows.form.confirmationMode", "Confirmación")}
          hint={confirmationModeOption(editing.confirmationMode).help}
        >
          <ZhSelect
            value={editing.confirmationMode}
            disabled={saving}
            onChange={(e) =>
              updateField("confirmationMode", e.target.value as ConfirmationMode)
            }
          >
            {CONFIRMATION_MODE_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </ZhSelect>
        </ZHField>
        <ZHField
          label={t("documentFlows.form.authorizationMode", "Nivel de autorización")}
          hint={authorizationModeOption(editing.authorizationMode).help}
        >
          <ZhSelect
            value={editing.authorizationMode}
            disabled={saving}
            onChange={(e) =>
              updateField("authorizationMode", e.target.value as AuthorizationMode)
            }
          >
            {AUTHORIZATION_MODE_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </ZhSelect>
        </ZHField>
      </ZHGrid>

      <h3 className="zh-section-title">
        {t("documentFlows.section.pending", "Documento pendiente")}
      </h3>
      <ZHGrid cols={2}>
        <ZHField
          label={t("documentFlows.form.pendingDocumentMode", "Documento pendiente")}
          hint={pendingDocumentModeOption(editing.pendingDocumentMode).help}
        >
          <ZhSelect
            value={editing.pendingDocumentMode}
            disabled={saving}
            onChange={(e) =>
              updateField("pendingDocumentMode", e.target.value as PendingDocumentMode)
            }
          >
            {PENDING_DOCUMENT_MODE_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </ZhSelect>
        </ZHField>
      </ZHGrid>

      <h3 className="zh-section-title">{t("documentFlows.section.cancellation", "Anulación")}</h3>
      <ZHGrid cols={2}>
        <ZHField
          label={t("documentFlows.form.cancellationMode", "Anulación")}
          hint={cancellationModeOption(editing.cancellationMode).help}
        >
          <ZhSelect
            value={editing.cancellationMode}
            disabled={saving}
            onChange={(e) =>
              updateField("cancellationMode", e.target.value as CancellationMode)
            }
          >
            {CANCELLATION_MODE_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </ZhSelect>
        </ZHField>
      </ZHGrid>

      <h3 className="zh-section-title">{t("documentFlows.section.requirements", "Requisitos")}</h3>
      <ZHGrid cols={2}>
        <ZHToggle
          label={REQUIRES_CANCELLATION_REASON_FLAG.onLabel}
          description={REQUIRES_CANCELLATION_REASON_FLAG.description}
          value={editing.requiresCancellationReason}
          disabled={saving}
          onChange={(next) => updateField("requiresCancellationReason", next)}
        />
        <ZHToggle
          label={REQUIRES_ATTACHMENT_FLAG.onLabel}
          description={REQUIRES_ATTACHMENT_FLAG.description}
          value={editing.requiresAttachment}
          disabled={saving}
          onChange={(next) => updateField("requiresAttachment", next)}
        />
        <ZHToggle
          label={REQUIRES_SUPPLIER_FLAG.onLabel}
          description={REQUIRES_SUPPLIER_FLAG.description}
          value={editing.requiresSupplier}
          disabled={saving}
          onChange={(next) => updateField("requiresSupplier", next)}
        />
        <ZHToggle
          label={REQUIRES_DUE_DATE_FLAG.onLabel}
          description={REQUIRES_DUE_DATE_FLAG.description}
          value={editing.requiresDueDate}
          disabled={saving}
          onChange={(next) => updateField("requiresDueDate", next)}
        />
      </ZHGrid>

      <h3 className="zh-section-title">
        {t("documentFlows.section.effects", "Efectos del documento")}
      </h3>
      <ZHGrid cols={2}>
        <ZHField
          label={t("documentFlows.form.payableGenerationMode", "Cuenta por pagar")}
          hint={payableGenerationModeOption(editing.payableGenerationMode).help}
        >
          <ZhSelect
            value={editing.payableGenerationMode}
            disabled={saving}
            onChange={(e) =>
              updateField("payableGenerationMode", e.target.value as PayableGenerationMode)
            }
          >
            {PAYABLE_GENERATION_MODE_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </ZhSelect>
        </ZHField>
        <ZHField
          label={t("documentFlows.form.accountingPostingMode", "Asiento contable")}
          hint={accountingPostingModeOption(editing.accountingPostingMode).help}
        >
          <ZhSelect
            value={editing.accountingPostingMode}
            disabled={saving}
            onChange={(e) =>
              updateField("accountingPostingMode", e.target.value as AccountingPostingMode)
            }
          >
            {ACCOUNTING_POSTING_MODE_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </ZhSelect>
        </ZHField>
        <ZHField
          label={t("documentFlows.form.inventoryImpactMode", "Impacto en inventario")}
          hint={inventoryImpactModeOption(editing.inventoryImpactMode).help}
        >
          <ZhSelect
            value={editing.inventoryImpactMode}
            disabled={saving}
            onChange={(e) =>
              updateField("inventoryImpactMode", e.target.value as InventoryImpactMode)
            }
          >
            {INVENTORY_IMPACT_MODE_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </ZhSelect>
        </ZHField>
      </ZHGrid>

      <h3 className="zh-section-title">
        {t("documentFlows.section.notifications", "Notificaciones")}
      </h3>
      <ZHGrid cols={2}>
        <ZHField
          label={t("documentFlows.form.notificationMode", "Notificaciones")}
          hint={notificationModeOption(editing.notificationMode).help}
        >
          <ZhSelect
            value={editing.notificationMode}
            disabled={saving}
            onChange={(e) => updateField("notificationMode", e.target.value as NotificationMode)}
          >
            {NOTIFICATION_MODE_OPTIONS.map((o) => (
              <option key={o.value} value={o.value}>
                {o.label}
              </option>
            ))}
          </ZhSelect>
        </ZHField>
        <ZHToggle
          label={IS_ACTIVE_FLAG.onLabel}
          description={IS_ACTIVE_FLAG.description}
          value={editing.isActive}
          disabled={saving}
          onChange={(next) => updateField("isActive", next)}
        />
      </ZHGrid>

      <div className="prd-crud-actions">
        <ZHBtn
          variant="primary"
          size="md"
          onClick={() => void handleSave()}
          disabled={saving || !canUpdate}
        >
          {saving ? t("common.saving", "Guardando...") : t("common.update", "Actualizar")}
        </ZHBtn>
        <ZHBtn variant="ghost" size="md" onClick={handleCancel}>
          {t("common.cancel", "Cancelar")}
        </ZHBtn>
      </div>
    </div>
  );

  return (
    <ErpPageTemplate
      kicker={t("app.nav.group.settings", "Configuración")}
      title={t("documentFlows.title", "Documentos y flujos")}
      subtitle={t(
        "documentFlows.subtitle",
        "Define cómo se comporta cada documento en la empresa.",
      )}
    >
      <ConfigTabsLayout
        activeTab={activeTab}
        onTabChange={(tab) => {
          if (tab === "list") handleCancel();
          else setActiveTab(tab);
        }}
        editorLabel={t("documentFlows.editTitle", "Editar flujo documental")}
        editorIcon="edit"
        listContent={listContent}
        editorContent={editorContent}
      />
    </ErpPageTemplate>
  );
}
