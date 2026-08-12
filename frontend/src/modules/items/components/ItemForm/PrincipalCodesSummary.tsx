import { Badge } from "../../../../components/PageShell";
import { ZHBtn, ZHFormSection } from "../../../../components/zh/ZHForm";
import type {
  ItemDetailDto,
  ItemPackagingLevelDto,
  ItemSupplierCodeDto,
  ItemVariantDto,
  VariantBarcodeDto,
} from "../../../../types/items";

type TFunc = (key: string, fallback?: string) => string;

type BarcodeSummary = VariantBarcodeDto & {
  variantName: string;
};

type Props = {
  t: TFunc;
  item: ItemDetailDto | null;
  onManageBarcodes: () => void;
  onManageSupplierPresentations: () => void;
};

function flattenBarcodes(variants: ItemVariantDto[]): BarcodeSummary[] {
  return variants.flatMap((variant) =>
    variant.barcodes.map((barcode) => ({
      ...barcode,
      variantName: variant.name || variant.sku,
    })),
  );
}

function packagingLabel(
  packagingLevels: ItemPackagingLevelDto[],
  packagingLevelId: string | null,
  fallback: string,
) {
  const level = packagingLevels.find((p) => p.id === packagingLevelId);
  if (!level) return fallback;
  return `${level.name} x ${level.baseQuantity} ${level.uomAbbrev}`;
}

function supplierLabel(supplierCode: ItemSupplierCodeDto, fallback: string) {
  return (
    supplierCode.supplierDisplayName?.trim() ||
    supplierCode.supplierIdentification?.trim() ||
    fallback
  );
}

export function BarcodePrincipalSummary({
  t,
  item,
  onManageBarcodes,
}: Pick<Props, "t" | "item" | "onManageBarcodes">) {
  const barcodes = item ? flattenBarcodes(item.variants) : [];

  return (
    <ZHFormSection
      title={t("items.barcodes.title", "Códigos de barras")}
      description={t(
        "items.barcodes.sectionDesc",
        "Código usado para escanear o buscar el ítem.",
      )}
    >
      <div className="items-principal-code-summary">
        {barcodes.length > 0 ? (
          <div className="items-principal-code-summary__list">
            {barcodes.map((barcode) => (
              <div
                key={barcode.id}
                className="items-principal-code-summary__item"
              >
                <div className="items-principal-code-summary__main">
                  <code className="items-principal-code-summary__code">
                    {barcode.code}
                  </code>
                  {barcode.isPrimary ? (
                    <Badge
                      label={t("items.barcodes.isPrimary", "Principal")}
                      variant="info"
                      size="md"
                    />
                  ) : null}
                </div>
                <div className="items-principal-code-summary__meta">
                  <span>{barcode.barcodeType}</span>
                  <span>{barcode.variantName}</span>
                </div>
              </div>
            ))}
          </div>
        ) : (
          <div className="items-principal-code-summary__empty">
            <div>
              <p className="items-principal-code-summary__empty-title">
                {t(
                  "items.barcodes.empty",
                  "No hay códigos de barras registrados.",
                )}
              </p>
              <p className="items-principal-code-summary__empty-desc">
                {t(
                  "items.barcodes.emptyHint",
                  "Agregue un código de barras si el producto será escaneado en compras o ventas.",
                )}
              </p>
            </div>
          </div>
        )}
        <ZHBtn
          type="button"
          variant="secondary"
          size="sm"
          onClick={onManageBarcodes}
        >
          {t(
            "items.barcodes.manageInDetail",
            "Gestionar códigos de barras en detalle del ítem",
          )}
        </ZHBtn>
      </div>
    </ZHFormSection>
  );
}

export function SupplierCodesPrincipalSummary({
  t,
  item,
  onManageSupplierPresentations,
}: Pick<Props, "t" | "item" | "onManageSupplierPresentations">) {
  const supplierCodes = item?.supplierCodes.filter((s) => s.isActive) ?? [];
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

  return (
    <ZHFormSection
      title={t(
        "items.supplierCodes.createTitle",
        "Códigos del proveedor para compras",
      )}
      description={t(
        "items.supplierCodes.editDesc",
        "Los códigos proveedor se completan en Inventario y presentaciones para vincular cada código con su presentación.",
      )}
    >
      <div className="items-principal-code-summary">
        {supplierCodes.length > 0 ? (
          <div className="items-principal-code-summary__list">
            {supplierCodes.map((supplierCode) => {
              const presentation = packagingLabel(
                item?.packagingLevels ?? [],
                supplierCode.packagingLevelId,
                noPresentationLabel,
              );
              const missingPresentation = !supplierCode.packagingLevelId;

              return (
                <div
                  key={supplierCode.id}
                  className="items-principal-code-summary__item"
                >
                  <div className="items-principal-code-summary__main">
                    <span className="items-principal-code-summary__supplier">
                      {supplierLabel(supplierCode, unnamedSupplierLabel)}
                    </span>
                    {supplierCode.isPrimary ? (
                      <Badge
                        label={t(
                          "items.supplierCodes.isPrimary",
                          "Principal",
                        )}
                        variant="info"
                        size="md"
                      />
                    ) : null}
                  </div>
                  {supplierCode.supplierIdentification ? (
                    <div className="items-principal-code-summary__meta">
                      <span>
                        {identificationPrefix}:{" "}
                        {supplierCode.supplierIdentification}
                      </span>
                    </div>
                  ) : null}
                  <div className="items-principal-code-summary__meta">
                    <span>
                      {t("items.supplierCodes.code", "Código")}:{" "}
                      {supplierCode.code}
                    </span>
                    <span>{presentation}</span>
                  </div>
                  {missingPresentation ? (
                    <p className="items-principal-code-summary__warning">
                      {t(
                        "items.supplierCodes.presentationMissingWarning",
                        "Sin presentación vinculada; una compra XML inventariable se bloqueará al confirmar.",
                      )}
                    </p>
                  ) : null}
                </div>
              );
            })}
          </div>
        ) : (
          <div className="items-principal-code-summary__empty">
            <div>
              <p className="items-principal-code-summary__empty-title">
                {t(
                  "items.supplierCodes.empty",
                  "No hay códigos de proveedor configurados.",
                )}
              </p>
              <p className="items-principal-code-summary__empty-desc">
                {t(
                  "items.supplierCodes.emptyHint",
                  "Agregue el código si el proveedor lo informa en facturas o XML.",
                )}
              </p>
            </div>
          </div>
        )}
        <ZHBtn
          type="button"
          variant="secondary"
          size="sm"
          onClick={onManageSupplierPresentations}
        >
          {t(
            "items.supplierCodes.completePresentationCta",
            "Completar presentación en Inventario y presentaciones",
          )}
        </ZHBtn>
      </div>
    </ZHFormSection>
  );
}
