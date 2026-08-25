import { useNavigate } from "react-router-dom";
import { PageShell, Badge } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn } from "../../../components/zh/ZHForm";
import "./accounting-hub.css";

interface AccountingBlock {
  key: string;
  title: string;
  description: string;
  route?: string;
  pendingReason?: string;
}

const BLOCKS: AccountingBlock[] = [
  {
    key: "journal-entries",
    title: "Asientos contables",
    description:
      "Consulta de los movimientos contables ya generados por el motor de contabilización — solo lectura, sin creación manual de asientos.",
    route: "/accounting/journal-entries",
  },
  {
    key: "chart-of-accounts",
    title: "Plan de cuentas",
    description:
      "Administración de cuentas (código, tipo, naturaleza) usadas para clasificar los asientos contables.",
    route: "/accounting/chart-of-accounts",
  },
  {
    key: "reports",
    title: "Reportes",
    description:
      "Libro Diario, Libro Mayor, Balance de Comprobación, Estado de Resultados y Balance General.",
    route: "/accounting/reports",
  },
  {
    key: "posting-rules",
    title: "Configuración contable",
    description: "Reglas de contabilización — qué cuentas recibe cada hecho contable.",
    pendingReason: "Pendiente: administración de reglas de contabilización desde la UI.",
  },
  {
    key: "accounting-periods",
    title: "Períodos contables",
    description: "Apertura, cierre y bloqueo de períodos fiscales.",
    pendingReason: "Pendiente: administración de períodos contables desde la UI.",
  },
];

/**
 * Hub de Contabilidad (ACCOUNTING-MODULE-NAV-UX-10B) — grid de tarjetas, una por sección del
 * módulo. Auditoría de reutilización: revisado `InitialLoadHubPage.tsx` (mismo patrón exacto de
 * grid de tarjetas con "Próximamente" para bloques sin pantalla real, ya establecido en el ERP) y
 * las páginas hermanas del módulo (`JournalEntriesPage`/`ChartOfAccountsPage`/
 * `AccountingReportsPage`, todas sobre `PageShell` — se usa el mismo aquí en vez de
 * `ErpPageTemplate`, que es el patrón de InitialLoad, para mantener consistencia dentro del
 * propio módulo Contabilidad). Reutiliza PageShell/ZHCard/ZHBtn/Badge — sin componentes nuevos.
 * "Configuración contable"/"Períodos contables" quedan como "Próximamente": los endpoints
 * (`posting-rules*`/`accounting-periods*`) ya existen en `AccountingController` pero no tienen
 * pantalla propia en el frontend todavía (brecha reportada en el entregable, no un olvido).
 */
export function AccountingHubPage() {
  const navigate = useNavigate();

  return (
    <PageShell
      title="Contabilidad"
      subtitle="Asientos, plan de cuentas y reportes generados por el motor de contabilización"
    >
      <div className="acc-hub-grid">
        {BLOCKS.map((block) => (
          <ZHCard
            key={block.key}
            title={block.title}
            actions={
              block.route ? (
                <ZHBtn onClick={() => navigate(block.route!)}>Abrir</ZHBtn>
              ) : (
                <Badge label="Próximamente" variant="neutral" />
              )
            }
            className={block.route ? undefined : "acc-hub-card--disabled"}
          >
            <p>{block.description}</p>
            {!block.route && block.pendingReason && (
              <p className="acc-hub-card__pending">{block.pendingReason}</p>
            )}
          </ZHCard>
        ))}
      </div>
    </PageShell>
  );
}
