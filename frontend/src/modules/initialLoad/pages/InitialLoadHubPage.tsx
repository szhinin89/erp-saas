import { useNavigate } from "react-router-dom";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { Badge } from "../../../components/PageShell";
import type { ImportType } from "../types/importBatch.types";
import "./initial-load.css";

interface ImportBlock {
  importType: ImportType;
  title: string;
  description: string;
  route?: string;
  pendingReason?: string;
}

const BLOCKS: ImportBlock[] = [
  {
    importType: "Customers",
    title: "Clientes",
    description: "Importa clientes desde una plantilla Excel.",
    route: "/initial-load/customers",
  },
  {
    importType: "Suppliers",
    title: "Proveedores",
    description: "Importa proveedores desde una plantilla Excel.",
    route: "/initial-load/suppliers",
  },
  {
    importType: "Items",
    title: "Catálogo de Productos",
    description:
      "Importa productos completos (categoría, marca, códigos de barra, PVP, proveedor) desde una sola plantilla Excel.",
    route: "/initial-load/products",
  },
  {
    importType: "Prices",
    title: "Precios",
    description: "Importación de precios base.",
    pendingReason: "Pendiente: Precios multi-lista",
  },
  {
    importType: "InitialStock",
    title: "Stock Inicial",
    description:
      "Carga existencias iniciales por producto y bodega — nunca crea productos ni bodegas.",
    route: "/initial-load/initial-stock",
  },
];

/**
 * Hub de Carga Inicial (INITIAL-LOAD-ARCH-01) — grid de tarjetas, una por tipo de dato maestro.
 * "Clientes", "Proveedores" (INITIAL-LOAD-SUPPLIERS-01), "Catálogo de Productos" (mismo
 * ImportType.Items — rediseño "importación inteligente" de INITIAL-LOAD-ITEMS-01) y "Stock
 * Inicial" (INITIAL-LOAD-INITIAL-STOCK-01) están habilitadas; el resto muestra "Próximamente"
 * con el mismo idioma visual que los tabs deshabilitados de CompanySettingsHubPage.
 */
export function InitialLoadHubPage() {
  const navigate = useNavigate();

  return (
    <ErpPageTemplate
      kicker="Configuración / Implementación"
      title="Carga Inicial"
      subtitle="Importa datos maestros y saldos iniciales para una empresa nueva."
    >
      <div className="il-grid">
        {BLOCKS.map((block) => (
          <ZHCard
            key={block.importType}
            title={block.title}
            actions={
              block.route ? (
                <ZHBtn onClick={() => navigate(block.route!)}>Iniciar</ZHBtn>
              ) : (
                <Badge label="Próximamente" variant="neutral" />
              )
            }
            className={block.route ? undefined : "il-card--disabled"}
          >
            <p>{block.description}</p>
            {!block.route && block.pendingReason && (
              <p className="il-card__pending">{block.pendingReason}</p>
            )}
          </ZHCard>
        ))}
      </div>
    </ErpPageTemplate>
  );
}
