import { useNavigate } from "react-router-dom";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { Badge, EmptyState, ErrorState } from "../../../components/PageShell";
import { ZhFileUpload } from "../../../components/zh/ZhFileUpload";
import {
  ZHDataTable,
  type ZHDataTableColumn,
} from "../../../components/zh/ZHDataTable";
import { ZHBtn } from "../../../components/zh/ZHForm";
import {
  formatDate,
  formatDateTime,
} from "../../../lib/formatters/dateFormatters";
import { formatMoneyWithSymbol } from "../../../lib/sanitizers";
import { usePurchaseReceptionPage } from "../hooks/usePurchaseReceptionPage";
import type { PurchaseReceptionItem } from "../api/purchaseReceptionService";
import { ZHItemMatchingPanel } from "../components/ZHItemMatchingPanel";
import "../styles/purchase-reception.css";

const STATUS_LABEL: Record<PurchaseReceptionItem["status"], string> = {
  IMPORTED: "Importada",
  PENDING: "Pendiente",
  NEW_SUPPLIER: "Proveedor nuevo",
};

const STATUS_VARIANT: Record<
  PurchaseReceptionItem["status"],
  "green" | "gray" | "orange"
> = {
  IMPORTED: "green",
  PENDING: "gray",
  NEW_SUPPLIER: "orange",
};

const DOCUMENT_STATUS_LABEL: Record<
  PurchaseReceptionItem["documentStatus"],
  string
> = {
  IMPORTED: "Guardado",
  VERIFIED: "Verificado",
  PROCESSED: "Procesado",
  CANCELLED: "Anulado",
};

const PROCESSING_STATUS_LABEL: Record<
  PurchaseReceptionItem["processingStatus"],
  string
> = {
  PENDING: "Sin procesar",
  PROCESSED: "Detalle OK",
  PROCESSED_WITH_WARNINGS: "Con advertencias",
  FAILED: "No interpretado",
};

const PROCESSING_STATUS_VARIANT: Record<
  PurchaseReceptionItem["processingStatus"],
  "green" | "orange" | "red" | "gray"
> = {
  PENDING: "gray",
  PROCESSED: "green",
  PROCESSED_WITH_WARNINGS: "orange",
  FAILED: "red",
};

export function PurchaseReceptionPage() {
  const ctx = usePurchaseReceptionPage();
  const navigate = useNavigate();

  const columns: ZHDataTableColumn<PurchaseReceptionItem>[] = [
    {
      key: "issueDate",
      header: "Fecha emisión",
      render: (row) => formatDate(row.issueDate),
    },
    {
      key: "supplierName",
      header: "Proveedor",
      render: (row) => row.supplierName,
    },
    { key: "supplierRuc", header: "RUC", render: (row) => row.supplierRuc },
    {
      key: "invoiceNumber",
      header: "Factura",
      render: (row) => row.invoiceNumber,
    },
    {
      key: "total",
      header: "Total",
      align: "right",
      render: (row) => formatMoneyWithSymbol(row.total),
    },
    {
      key: "supplierExists",
      header: "Proveedor ERP",
      align: "center",
      render: (row) => (
        <Badge
          variant={row.supplierExists ? "green" : "red"}
          label={row.supplierExists ? "Existe" : "No existe"}
        />
      ),
    },
    {
      key: "purchaseExists",
      header: "Compra ERP",
      align: "center",
      render: (row) => (
        <Badge
          variant={row.purchaseExists ? "green" : "red"}
          label={row.purchaseExists ? "Existe" : "No existe"}
        />
      ),
    },
    {
      key: "status",
      header: "Estado",
      align: "center",
      render: (row) => (
        <Badge
          variant={STATUS_VARIANT[row.status]}
          label={STATUS_LABEL[row.status]}
        />
      ),
    },
    {
      key: "documentStatus",
      header: "Documento",
      align: "center",
      render: (row) => (
        <Badge
          variant="blue"
          upper
          size="md"
          label={DOCUMENT_STATUS_LABEL[row.documentStatus]}
          title={`Documento persistido — id ${row.documentId}`}
        />
      ),
    },
    {
      key: "processingStatus",
      header: "Procesamiento",
      align: "center",
      render: (row) => {
        if (row.documentStatus === "IMPORTED") return null;
        return (
          <Badge
            variant={PROCESSING_STATUS_VARIANT[row.processingStatus]}
            label={PROCESSING_STATUS_LABEL[row.processingStatus]}
            title={
              row.processingNotes ??
              "El detalle del XML se interpretó sin advertencias."
            }
          />
        );
      },
    },
    {
      key: "xmlSri",
      header: "XML SRI",
      align: "center",
      render: (row) => {
        const rowState = ctx.xmlRowState[row.documentId];

        if (row.documentStatus !== "IMPORTED") {
          return <Badge variant="green" label="XML recibido" />;
        }
        if (rowState === "loading") {
          return <Badge variant="gray" label="Consultando..." />;
        }
        return (
          <div className="pur-xml-cell">
            <Badge
              variant={rowState === "error" ? "red" : "gray"}
              label={rowState === "error" ? "Error consulta" : "Pendiente XML"}
            />
            <ZHBtn
              variant="secondary"
              size="xs"
              type="button"
              onClick={() => void ctx.handleDownloadXml(row.documentId)}
            >
              Consultar XML
            </ZHBtn>
          </div>
        );
      },
    },
    {
      key: "itemMatching",
      header: "Productos",
      align: "center",
      render: (row) => {
        if (
          row.documentStatus !== "VERIFIED" &&
          row.documentStatus !== "PROCESSED"
        ) {
          return null;
        }
        if (row.processingStatus === "FAILED") {
          return null;
        }
        return (
          <ZHBtn
            variant="secondary"
            size="xs"
            type="button"
            onClick={() =>
              ctx.openMatchingPanel(row.documentId, row.supplierName)
            }
          >
            Vincular productos
          </ZHBtn>
        );
      },
    },
    {
      key: "createPurchase",
      header: "Compra",
      align: "center",
      render: (row) => {
        if (row.documentStatus === "PROCESSED") {
          return <Badge variant="green" label="Compra creada" />;
        }
        if (row.documentStatus !== "VERIFIED") {
          return null;
        }
        // Un único botón para todos los documentos Verificados, sin excepciones visibles: la
        // reconstrucción del detalle cuando el intento anterior falló (o el rechazo si el XML
        // sigue sin poder interpretarse) ocurre de forma transparente dentro de create-draft — el
        // usuario nunca ve un paso, label ni concepto distinto de "Crear Compra".
        return (
          <ZHBtn
            variant="primary"
            size="xs"
            type="button"
            onClick={() =>
              navigate(`/purchases?fromReceptionId=${row.documentId}`)
            }
          >
            Crear compra
          </ZHBtn>
        );
      },
    },
  ];

  return (
    <ErpPageTemplate
      title="Recepción electrónica"
      subtitle="Importe el TXT de comprobantes recibidos del SRI. Cada factura queda guardada como documento de recepción y se compara contra los proveedores y compras ya registrados en el ERP. Al consultar el XML autorizado, sus líneas quedan disponibles para vincular con el catálogo de Items — todavía no crea compras automáticamente."
    >
      <div className="pg-section">
        <ZhFileUpload
          accept=".txt"
          onFileSelected={(file) => void ctx.handleFileSelected(file)}
          uploading={ctx.uploading}
          progress={ctx.progress}
          error={ctx.error}
          currentFile={
            ctx.fileName
              ? {
                  name: ctx.fileName,
                  sizeBytes: 0,
                  uploadedAt: formatDateTime(new Date().toISOString()),
                }
              : null
          }
          selectLabel="Seleccione el archivo TXT de recepción"
          dropLabel="o arrástrelo aquí"
          uploadingLabel="Analizando archivo..."
          noFileLabel="Aún no se ha importado ningún archivo."
        />
      </div>

      {ctx.error && !ctx.uploading && (
        <div className="pg-section">
          <ErrorState message={ctx.error} />
        </div>
      )}

      {ctx.result && (
        <div className="pg-section pur-reception-summary">
          <Badge
            variant="green"
            label={`Importadas: ${ctx.summary.imported}`}
          />
          <Badge variant="gray" label={`Pendientes: ${ctx.summary.pending}`} />
          <Badge
            variant="orange"
            label={`Proveedor nuevo: ${ctx.summary.newSupplier}`}
          />
          {ctx.summary.skipped > 0 && (
            <Badge
              variant="gray"
              label={`Omitidas (no soportadas en esta fase): ${ctx.summary.skipped}`}
            />
          )}
        </div>
      )}

      <div className="pg-section">
        {ctx.result === null && !ctx.uploading ? (
          <EmptyState message="Importe un archivo TXT para ver los comprobantes recibidos." />
        ) : (
          <ZHDataTable
            columns={columns}
            rows={ctx.items}
            rowKey={(row) => row.documentId}
            loading={ctx.uploading}
            emptyMessage="El archivo no contiene facturas para comparar."
            page={ctx.page}
            pageSize={ctx.pageSize}
            total={ctx.total}
            onPageChange={ctx.setPage}
          />
        )}
      </div>

      <ZHItemMatchingPanel
        open={ctx.matchingDocumentId !== null}
        documentId={ctx.matchingDocumentId}
        supplierName={ctx.matchingSupplierName}
        onClose={ctx.closeMatchingPanel}
      />
    </ErpPageTemplate>
  );
}
