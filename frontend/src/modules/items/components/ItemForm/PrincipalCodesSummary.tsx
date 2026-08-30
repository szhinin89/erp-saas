import { useState } from "react";
import { Badge, EmptyState } from "../../../../components/PageShell";
import { ZHBtn, ZHFormSection } from "../../../../components/zh/ZHForm";
import { ZhSelect, ZhTextInput } from "../../../../components/zh/inputs";
import { ZHDataTable, type ZHDataTableColumn } from "../../../../components/zh/ZHDataTable";
import { itemService } from "../../api/itemService";
import type {
  ItemDetailDto,
  ItemSupplierCodeDto,
  ItemVariantDto,
  VariantBarcodeDto,
} from "../../../../types/items";

type TFunc = (key: string, fallback?: string) => string;

type BarcodeSummary = VariantBarcodeDto & {
  variantId: string;
  variantName: string;
};

type ManagerProps = {
  t: TFunc;
  item: ItemDetailDto | null;
  disabled?: boolean;
};

function flattenBarcodes(variants: ItemVariantDto[]): BarcodeSummary[] {
  return variants.flatMap((variant) =>
    variant.barcodes.map((barcode) => ({
      ...barcode,
      variantId: variant.id,
      variantName: variant.name || variant.sku,
    })),
  );
}

function supplierLabel(supplierCode: ItemSupplierCodeDto, fallback: string) {
  return (
    supplierCode.supplierDisplayName?.trim() ||
    supplierCode.supplierIdentification?.trim() ||
    fallback
  );
}

export function BarcodePrincipalManager({
  t,
  item,
  disabled = false,
  barcodeTypeOptions,
  onRefresh,
}: ManagerProps & {
  barcodeTypeOptions: { code: string; name: string }[];
  onRefresh: () => void;
}) {
  const barcodes = item ? flattenBarcodes(item.variants) : [];
  const activeVariants =
    item?.variants.filter((variant) => variant.isActive) ?? [];
  const defaultVariantId = activeVariants[0]?.id ?? item?.variants[0]?.id ?? "";
  const [adding, setAdding] = useState(false);
  const [variantId, setVariantId] = useState(defaultVariantId);
  const [code, setCode] = useState("");
  const [barcodeType, setBarcodeType] = useState("");
  const [busy, setBusy] = useState(false);

  const resetAdd = () => {
    setAdding(false);
    setVariantId(defaultVariantId);
    setCode("");
    setBarcodeType("");
  };

  const handleAdd = async () => {
    if (!item || !variantId || !code.trim() || !barcodeType) return;
    setBusy(true);
    try {
      await itemService.addBarcode(
        item.id,
        variantId,
        code.trim(),
        barcodeType,
      );
      resetAdd();
      onRefresh();
    } finally {
      setBusy(false);
    }
  };

  const handleDisable = async (barcode: BarcodeSummary) => {
    if (!item) return;
    setBusy(true);
    try {
      await itemService.disableBarcode(item.id, barcode.variantId, barcode.id);
      onRefresh();
    } finally {
      setBusy(false);
    }
  };

  return (
    <ZHFormSection
      title={t("items.barcodes.title", "Códigos de barras")}
      description={t(
        "items.barcodes.sectionDesc",
        "Código usado para escanear o buscar el ítem.",
      )}
    >
      {barcodes.length > 0 || adding ? (
        <div className="table-scroll">
          <table className="table">
            <thead>
              <tr>
                <th>{t("items.barcodes.code", "Código")}</th>
                <th>{t("items.barcodes.type", "Tipo")}</th>
                <th>{t("items.barcodes.primary", "Principal")}</th>
                <th className="pg-th-right">
                  {t("common.actions", "Acciones")}
                </th>
              </tr>
            </thead>
            <tbody>
              {barcodes.map((barcode) => (
                <tr key={barcode.id}>
                  <td>
                    <code>{barcode.code}</code>
                    {item && item.variants.length > 1 ? (
                      <p className="zh-field-hint">{barcode.variantName}</p>
                    ) : null}
                  </td>
                  <td>{barcode.barcodeType}</td>
                  <td>
                    {barcode.isPrimary ? (
                      <Badge
                        label={t("items.barcodes.isPrimary", "Principal")}
                        variant="info"
                        size="md"
                      />
                    ) : (
                      t("common.no", "No")
                    )}
                  </td>
                  <td className="pg-td-right">
                    <ZHBtn
                      type="button"
                      variant="ghost"
                      size="sm"
                      disabled={disabled || busy}
                      onClick={() => void handleDisable(barcode)}
                    >
                      {t("common.remove", "Quitar")}
                    </ZHBtn>
                  </td>
                </tr>
              ))}
              {adding && (
                <tr>
                  <td>
                    <ZhTextInput
                      density="compact"
                      value={code}
                      onChange={(event) => setCode(event.target.value)}
                      placeholder={t(
                        "items.barcodes.codePlaceholder",
                        "7501234567890",
                      )}
                      disabled={busy}
                    />
                  </td>
                  <td>
                    <ZhSelect
                      value={barcodeType}
                      onChange={(event) => setBarcodeType(event.target.value)}
                      disabled={busy}
                    >
                      <option value="">
                        {t("common.selectOption", "— Seleccionar —")}
                      </option>
                      {barcodeTypeOptions.map((type) => (
                        <option key={type.code} value={type.code}>
                          {type.name}
                        </option>
                      ))}
                    </ZhSelect>
                  </td>
                  <td>
                    {activeVariants.length > 1 ? (
                      <ZhSelect
                        value={variantId}
                        onChange={(event) => setVariantId(event.target.value)}
                        disabled={busy}
                      >
                        {activeVariants.map((variant) => (
                          <option key={variant.id} value={variant.id}>
                            {variant.name || variant.sku}
                          </option>
                        ))}
                      </ZhSelect>
                    ) : (
                      t(
                        "items.barcodes.pendingPrimary",
                        "Se marcará según regla existente",
                      )
                    )}
                  </td>
                  <td className="pg-td-right">
                    <div className="items-row-actions">
                      <ZHBtn
                        type="button"
                        variant="primary"
                        size="sm"
                        disabled={
                          busy || !variantId || !code.trim() || !barcodeType
                        }
                        onClick={() => void handleAdd()}
                      >
                        {t("common.save", "Guardar")}
                      </ZHBtn>
                      <ZHBtn
                        type="button"
                        variant="ghost"
                        size="sm"
                        disabled={busy}
                        onClick={resetAdd}
                      >
                        {t("common.cancel", "Cancelar")}
                      </ZHBtn>
                    </div>
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      ) : (
        <div className="pg-pad-40">
          <EmptyState
            message={t(
              "items.barcodes.empty",
              "No hay códigos de barras registrados.",
            )}
          />
        </div>
      )}

      {!adding && (
        <ZHBtn
          type="button"
          variant="secondary"
          size="sm"
          disabled={disabled || busy || !defaultVariantId}
          onClick={() => {
            setVariantId(defaultVariantId);
            setAdding(true);
          }}
        >
          {t("items.barcodes.add", "Agregar código de barras")}
        </ZHBtn>
      )}
    </ZHFormSection>
  );
}

export function SupplierCodesPrincipalManager({
  t,
  item,
  disabled = false,
  onUpdatePresentation,
}: ManagerProps & {
  onUpdatePresentation: (
    supplierId: string,
    code: string,
    packagingLevelId: string | null,
  ) => Promise<void>;
}) {
  const supplierCodes = item?.supplierCodes.filter((s) => s.isActive) ?? [];
  const activePackaging = item?.packagingLevels.filter((p) => p.isActive) ?? [];
  const pendingPresentationLabel = t(
    "items.supplierCodes.presentationPending",
    "Presentación pendiente",
  );
  const noPresentationLabel = t(
    "items.supplierCodes.noPresentation",
    "Sin presentación asociada",
  );
  const unnamedSupplierLabel = t(
    "items.supplierCodes.unnamedSupplier",
    "Proveedor sin nombre",
  );
  const identificationPrefix = t(
    "items.supplierCodes.identificationPrefix",
    "RUC",
  );
  const packagingLabel = (id: string | null) => {
    const packaging = activePackaging.find((p) => p.id === id);
    if (packaging) {
      return `${packaging.name} x ${packaging.baseQuantity} ${packaging.uomAbbrev}`;
    }
    return activePackaging.length === 0
      ? pendingPresentationLabel
      : noPresentationLabel;
  };

  const supplierCodeColumns: ZHDataTableColumn<ItemSupplierCodeDto>[] = [
    {
      key: "supplier",
      header: t("items.supplierCodes.col.supplier", "Proveedor"),
      render: (supplierCode) => supplierLabel(supplierCode, unnamedSupplierLabel),
    },
    {
      key: "identification",
      header: t("items.supplierCodes.col.identification", "RUC/Identificación"),
      render: (supplierCode) =>
        supplierCode.supplierIdentification ? (
          <>
            {identificationPrefix}: {supplierCode.supplierIdentification}
          </>
        ) : (
          "—"
        ),
    },
    {
      key: "code",
      header: t("items.supplierCodes.col.code", "Código proveedor"),
      render: (supplierCode) => <code>{supplierCode.code}</code>,
    },
    {
      key: "presentation",
      header: t("items.supplierCodes.col.presentation", "Presentación"),
      render: (supplierCode) => packagingLabel(supplierCode.packagingLevelId),
    },
    {
      key: "primary",
      header: t("items.supplierCodes.col.primary", "Principal"),
      render: (supplierCode) => (supplierCode.isPrimary ? t("common.yes", "Sí") : t("common.no", "No")),
    },
    {
      key: "actions",
      header: t("common.actions", "Acciones"),
      align: "right",
      render: (supplierCode) => (
        <ZhSelect
          value={supplierCode.packagingLevelId ?? ""}
          aria-label={t(
            "items.supplierCodes.presentationSelect",
            "Presentación del código proveedor",
          )}
          disabled={disabled || activePackaging.length === 0 || !supplierCode.supplierId}
          onChange={(event) =>
            void onUpdatePresentation(
              supplierCode.supplierId ?? "",
              supplierCode.code,
              event.target.value || null,
            )
          }
        >
          <option value="">
            {activePackaging.length === 0 ? pendingPresentationLabel : noPresentationLabel}
          </option>
          {activePackaging.map((packaging) => (
            <option key={packaging.id} value={packaging.id}>
              {packagingLabel(packaging.id)}
            </option>
          ))}
        </ZhSelect>
      ),
    },
  ];

  return (
    <ZHFormSection
      title={t(
        "items.supplierCodes.title",
        "Códigos del proveedor para compras",
      )}
      description={t(
        "items.supplierCodes.sectionDesc",
        "Código con el que el proveedor identifica este producto en sus facturas/XML.",
      )}
    >
      {supplierCodes.length > 0 ? (
        <ZHDataTable
          columns={supplierCodeColumns}
          rows={supplierCodes}
          rowKey={(supplierCode) => supplierCode.id}
        />
      ) : (
        <div className="pg-pad-40">
          <EmptyState
            message={t(
              "items.supplierCodes.empty",
              "No hay códigos de proveedor configurados.",
            )}
          />
        </div>
      )}
    </ZHFormSection>
  );
}

export function BarcodePrincipalSummary({
  t,
  item,
  onManageBarcodes,
}: {
  t: TFunc;
  item: ItemDetailDto | null;
  onManageBarcodes: () => void;
}) {
  return (
    <BarcodePrincipalManager
      t={t}
      item={item}
      disabled
      barcodeTypeOptions={[]}
      onRefresh={onManageBarcodes}
    />
  );
}

export function SupplierCodesPrincipalSummary({
  t,
  item,
  onManageSupplierPresentations,
}: {
  t: TFunc;
  item: ItemDetailDto | null;
  onManageSupplierPresentations: () => void;
}) {
  return (
    <SupplierCodesPrincipalManager
      t={t}
      item={item}
      disabled
      onUpdatePresentation={async () => onManageSupplierPresentations()}
    />
  );
}
