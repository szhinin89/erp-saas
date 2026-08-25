import { useCallback, useEffect, useMemo, useState } from "react";
import { PageShell, Badge } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHFilterBar } from "../../../components/zh/ZHFilterBar";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZhSelect } from "../../../components/zh/inputs";
import { ZHDataTable, type ZHDataTableColumn } from "../../../components/zh/ZHDataTable";
import { formatApiRequestError } from "../../lib/apiError";
import { message } from "../../../lib/messages";
import { accountingApi, type PostingRuleDto, type PostingRuleLineDto } from "../api/accountingApi";

import "../../../styles/shared/items-catalog.css";

const SOURCE_MODULE_OPTIONS = [
  { value: "", label: "Todos los módulos" },
  { value: "Sales", label: "Ventas" },
  { value: "Purchases", label: "Compras" },
  { value: "Finance", label: "Finanzas" },
];

const STATUS_OPTIONS = [
  { value: "", label: "Todos los estados" },
  { value: "active", label: "Activa" },
  { value: "inactive", label: "Inactiva" },
];

interface RuleProblem {
  label: string;
}

function findRuleProblems(rule: PostingRuleDto): RuleProblem[] {
  const problems: RuleProblem[] = [];
  if (rule.lines.length < 2) problems.push({ label: "Menos de 2 líneas — nunca produciría un asiento" });
  const debitLines = rule.lines.filter((l) => l.nature === "Debit");
  const creditLines = rule.lines.filter((l) => l.nature === "Credit");
  if (debitLines.length === 0) problems.push({ label: "Sin líneas en el Debe" });
  if (creditLines.length === 0) problems.push({ label: "Sin líneas en el Haber" });
  const badAccounts = rule.lines.filter((l) => !l.accountIsActive || !l.accountAllowsPosting);
  for (const l of badAccounts) {
    if (!l.accountIsActive)
      problems.push({ label: `Cuenta ${l.accountCode} (${l.accountName}) está inactiva` });
    if (!l.accountAllowsPosting)
      problems.push({ label: `Cuenta ${l.accountCode} (${l.accountName}) no admite movimientos` });
  }
  return problems;
}

const LINE_COLUMNS: ZHDataTableColumn<PostingRuleLineDto>[] = [
  {
    key: "account",
    header: "Cuenta",
    render: (row) => (
      <span>
        <code className="prd-sku">{row.accountCode}</code> {row.accountName}
      </span>
    ),
  },
  { key: "accountType", header: "Tipo", render: (row) => row.accountType },
  { key: "accountNature", header: "Naturaleza", render: (row) => row.accountNature },
  { key: "amountKind", header: "AmountKind", render: (row) => row.amountKind },
  {
    key: "accountIsActive",
    header: "Cuenta activa",
    align: "center",
    render: (row) => (
      <Badge label={row.accountIsActive ? "Sí" : "No"} variant={row.accountIsActive ? "green" : "red"} />
    ),
  },
  {
    key: "accountAllowsPosting",
    header: "Permite asiento",
    align: "center",
    render: (row) => (
      <Badge
        label={row.accountAllowsPosting ? "Sí" : "No"}
        variant={row.accountAllowsPosting ? "green" : "red"}
      />
    ),
  },
];

function PostingRuleCard({ rule }: { rule: PostingRuleDto }) {
  const debitLines = rule.lines.filter((l) => l.nature === "Debit").sort((a, b) => a.sortOrder - b.sortOrder);
  const creditLines = rule.lines.filter((l) => l.nature === "Credit").sort((a, b) => a.sortOrder - b.sortOrder);
  const problems = findRuleProblems(rule);

  return (
    <ZHCard
      title={
        <span>
          <strong>{rule.sourceModule}</strong> / {rule.factType}
        </span>
      }
      actions={
        <div className="zh-form-actions-row">
          <Badge label={rule.isActive ? "Activa" : "Inactiva"} variant={rule.isActive ? "green" : "gray"} />
          <Badge label={`${rule.lines.length} líneas`} variant="blue" />
        </div>
      }
    >
      {problems.length > 0 && (
        <ZHPageNotice
          variant="warning"
          message="Esta regla tiene problemas que impedirían o distorsionarían el asiento generado"
          detail={problems.map((p) => p.label).join(" · ")}
        />
      )}

      <div className="zh-mb-16">
        <h4>Debe</h4>
        <ZHDataTable
          columns={LINE_COLUMNS}
          rows={debitLines}
          rowKey={(row) => row.id}
          emptyMessage="Sin líneas en el Debe."
        />
      </div>

      <div>
        <h4>Haber</h4>
        <ZHDataTable
          columns={LINE_COLUMNS}
          rows={creditLines}
          rowKey={(row) => row.id}
          emptyMessage="Sin líneas en el Haber."
        />
      </div>
    </ZHCard>
  );
}

/**
 * Contabilidad → Reglas contables (ACCOUNTING-POSTING-RULES-UI-12). Auditoría de reutilización:
 * revisadas `ChartOfAccountsPage.tsx` (mismo módulo, filtros vía `ZHCard actions` para 2-3
 * campos) y `ElectronicDocumentsFilters.tsx` (referencia real de `ZHFilterBar`+`ZHField
 * density="compact"` para una barra de filtros con varios campos — usada aquí en vez del slot
 * `actions` de `ZHCard` porque son 3 filtros independientes). Reutiliza PageShell/ZHCard/
 * ZHFilterBar/ZHField/ZhSelect/ZHDataTable/Badge/ZHPageNotice — sin componentes nuevos. Solo
 * lectura: consume exclusivamente `GET /api/v1/accounting/posting-rules`
 * (ACCOUNTING-POSTING-RULES-SEED-11B ya sembró las reglas; esta pantalla no crea/edita/deshabilita
 * ninguna, aunque el endpoint ya lo admite — ver ticket, fuera de alcance). Pantalla principal,
 * accesible solo desde el menú (`AccountingModule.cs`) — sin botones cruzados hacia Asientos
 * contables/Plan de cuentas/Reportes (navegación duplicada del menú, mismo criterio de
 * ACCOUNTING-NAVIGATION-CANONICAL-AUDIT-11C).
 */
export function PostingRulesPage() {
  const [rules, setRules] = useState<PostingRuleDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [sourceModule, setSourceModule] = useState("");
  const [factType, setFactType] = useState("");
  const [status, setStatus] = useState("");

  const fetchRules = useCallback(async () => {
    setLoading(true);
    try {
      const list = await accountingApi.listPostingRules();
      setRules(list);
    } catch (err: unknown) {
      message.error(
        formatApiRequestError(err, { generic: "No se pudo cargar las reglas de contabilización." }),
      );
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void fetchRules();
  }, [fetchRules]);

  const factTypeOptions = useMemo(() => {
    const scoped = sourceModule ? rules.filter((r) => r.sourceModule === sourceModule) : rules;
    const unique = Array.from(new Set(scoped.map((r) => r.factType))).sort();
    return [{ value: "", label: "Todos los hechos contables" }, ...unique.map((f) => ({ value: f, label: f }))];
  }, [rules, sourceModule]);

  const filteredRules = useMemo(() => {
    return rules
      .filter((r) => (sourceModule ? r.sourceModule === sourceModule : true))
      .filter((r) => (factType ? r.factType === factType : true))
      .filter((r) => {
        if (status === "active") return r.isActive;
        if (status === "inactive") return !r.isActive;
        return true;
      })
      .sort((a, b) => a.sourceModule.localeCompare(b.sourceModule) || a.factType.localeCompare(b.factType));
  }, [rules, sourceModule, factType, status]);

  const handleClearFilters = () => {
    setSourceModule("");
    setFactType("");
    setStatus("");
  };

  return (
    <PageShell
      kicker="Contabilidad"
      title="Reglas contables"
      subtitle="Configuración de mapeo cuenta/Debe-Haber que el motor de contabilización usa para generar cada asiento"
    >
      <ZHCard title="Filtros">
        <ZHFilterBar onClear={handleClearFilters} disabled={loading}>
          <div className="zh-filterbar__field">
            <ZHField label="Módulo origen" density="compact">
              <ZhSelect
                value={sourceModule}
                disabled={loading}
                onChange={(e) => {
                  setSourceModule(e.target.value);
                  setFactType("");
                }}
              >
                {SOURCE_MODULE_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                  </option>
                ))}
              </ZhSelect>
            </ZHField>
          </div>
          <div className="zh-filterbar__field">
            <ZHField label="Hecho contable" density="compact">
              <ZhSelect value={factType} disabled={loading} onChange={(e) => setFactType(e.target.value)}>
                {factTypeOptions.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                  </option>
                ))}
              </ZhSelect>
            </ZHField>
          </div>
          <div className="zh-filterbar__field">
            <ZHField label="Estado" density="compact">
              <ZhSelect value={status} disabled={loading} onChange={(e) => setStatus(e.target.value)}>
                {STATUS_OPTIONS.map((o) => (
                  <option key={o.value} value={o.value}>
                    {o.label}
                  </option>
                ))}
              </ZhSelect>
            </ZHField>
          </div>
          <div className="zh-filterbar__field">
            <ZHBtn variant="ghost" size="sm" type="button" onClick={() => void fetchRules()} disabled={loading}>
              Actualizar
            </ZHBtn>
          </div>
        </ZHFilterBar>
      </ZHCard>

      {!loading && rules.length === 0 && (
        <ZHPageNotice
          variant="warning"
          message="No existen reglas contables configuradas. Cree o ejecute la configuración inicial antes de emitir documentos."
        />
      )}

      {!loading && rules.length > 0 && filteredRules.length === 0 && (
        <ZHPageNotice variant="info" message="Ningún resultado con los filtros seleccionados." />
      )}

      {filteredRules.map((rule) => (
        <PostingRuleCard key={rule.id} rule={rule} />
      ))}
    </PageShell>
  );
}
