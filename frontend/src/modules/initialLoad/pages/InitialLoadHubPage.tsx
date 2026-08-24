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
    description: "Importación de proveedores.",
  },
  {
    importType: "Items",
    title: "Ítems",
    description: "Importación del catálogo de ítems.",
  },
  {
    importType: "Prices",
    title: "Precios",
    description: "Importación de precios base.",
  },
  {
    importType: "InitialStock",
    title: "Stock Inicial",
    description: "Importación de saldos iniciales de inventario.",
  },
];

/**
 * Hub de Carga Inicial (INITIAL-LOAD-ARCH-01) — grid de tarjetas, una por tipo de dato maestro.
 * Solo "Clientes" está habilitada en esta entrega; el resto muestra "Próximamente" con el mismo
 * idioma visual que los tabs deshabilitados de CompanySettingsHubPage.
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
          </ZHCard>
        ))}
      </div>
    </ErpPageTemplate>
  );
}
