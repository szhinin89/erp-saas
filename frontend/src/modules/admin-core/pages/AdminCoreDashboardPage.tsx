import { useCallback, useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { PageShell } from "../../../components/PageShell";
import { ZHCard } from "../../../components/zh/ZHCard";
import {
  ZHDataTable,
  type ZHDataTableColumn,
} from "../../../components/zh/ZHDataTable";
import { ZHBtn, ZHField } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { adminCoreService } from "../api/adminCoreService";
import { authService } from "../../auth/api/authService";
import { useAuthStore } from "../../../store/authStore";
import { formatApiRequestError } from "../../lib/apiError";
import type { AdminCoreCompany } from "../../../types/adminCore";

import { ZHModal } from "../../../components/zh/ZHModal";

interface TenantGroup {
  tenantId: string;
  tenantName: string;
  tenantIsActive: boolean;
  companies: AdminCoreCompany[];
}

function groupByTenant(companies: AdminCoreCompany[]): TenantGroup[] {
  const map = new Map<string, TenantGroup>();
  for (const c of companies) {
    let group = map.get(c.tenantId);
    if (!group) {
      group = {
        tenantId: c.tenantId,
        tenantName: c.tenantName,
        tenantIsActive: c.tenantIsActive,
        companies: [],
      };
      map.set(c.tenantId, group);
    }
    group.companies.push(c);
  }
  return Array.from(map.values()).sort((a, b) =>
    a.tenantName.localeCompare(b.tenantName),
  );
}

/**
 * Dashboard global AdminGlobalCore — nunca llama endpoints operativos (/me/menu,
 * /session/context, /auth/my-companies, /session/available-branches, /config/decimals,
 * /electronic-invoicing/status, /dashboard/kpis). Solo consume GET /api/v1/admin-core/companies
 * PUT /api/v1/admin-core/companies/{id} y POST /api/v1/auth/global/operate-company.
 */
export function AdminCoreDashboardPage() {
  const navigate = useNavigate();
  const login = useAuthStore((s) => s.login);
  const [companies, setCompanies] = useState<AdminCoreCompany[]>([]);
  const [editing, setEditing] = useState<AdminCoreCompany | null>(null);
  const [taxId, setTaxId] = useState("");
  const [saving, setSaving] = useState(false);
  const [editError, setEditError] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [operatingCompanyId, setOperatingCompanyId] = useState<string | null>(
    null,
  );

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const rows = await adminCoreService.listCompanies();
        if (!cancelled) setCompanies(rows);
      } catch (e) {
        if (!cancelled)
          setError(
            formatApiRequestError(e, {
              offline: "No se pudo conectar con el servidor.",
              generic: "No se pudo cargar el listado de empresas.",
            }),
          );
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const groups = useMemo(() => groupByTenant(companies), [companies]);

  const handleOperate = async (companyId: string) => {
    setError("");
    setOperatingCompanyId(companyId);
    try {
      const payload = await authService.operateCompany(companyId);
      login(payload);
      navigate("/dashboard", { replace: true });
    } catch (e) {
      setError(
        formatApiRequestError(e, {
          offline: "No se pudo conectar con el servidor.",
          generic: "No se pudo ingresar a operar esta empresa.",
        }),
      );
      setOperatingCompanyId(null);
    }
  };

  const closeEditor = useCallback(() => {
    if (!saving) setEditing(null);
  }, [saving]);

  const handleSave = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!editing || saving) return;
    setSaving(true);
    setEditError("");
    try {
      await adminCoreService.updateCompany(editing, taxId);
      const rows = await adminCoreService.listCompanies();
      setCompanies(rows);
      setEditing(null);
    } catch (e) {
      setEditError(
        formatApiRequestError(e, {
          offline: "No se pudo conectar con el servidor.",
          generic: "No se pudo guardar la empresa.",
        }),
      );
    } finally {
      setSaving(false);
    }
  };

  const columns: ZHDataTableColumn<AdminCoreCompany>[] = [
    {
      key: "ruc",
      header: "RUC",
      render: (r) => (
        <>
          {r.ruc}
          {r.ruc.startsWith("TMP-EC-") ? (
            <span className="zh-form-badge">RUC Pendiente</span>
          ) : null}
        </>
      ),
    },
    { key: "legalName", header: "Razón social", render: (r) => r.legalName },
    {
      key: "tradeName",
      header: "Nombre comercial",
      render: (r) => r.tradeName ?? "—",
    },
    {
      key: "isActive",
      header: "Estado",
      render: (r) => (r.isActive ? "Activa" : "Inactiva"),
    },
    {
      key: "actions",
      header: "",
      align: "right",
      render: (r) => (
        <>
          <ZHBtn
            variant="ghost"
            size="sm"
            type="button"
            disabled={operatingCompanyId !== null}
            onClick={() => {
              setEditing({ ...r });
              setTaxId(r.ruc.startsWith("TMP-EC-") ? "" : r.ruc);
              setEditError("");
            }}
          >
            Editar
          </ZHBtn>
          <ZHBtn
            variant="primary"
            size="sm"
            type="button"
            disabled={operatingCompanyId !== null}
            onClick={() => void handleOperate(r.companyId)}
          >
            {operatingCompanyId === r.companyId
              ? "Ingresando…"
              : "Ingresar a esta empresa"}
          </ZHBtn>
        </>
      ),
    },
  ];

  return (
    <PageShell title="Dashboard global" subtitle="Empresas por tenant">
      <ZHModal
        open={editing !== null}
        title="Editar empresa"
        onClose={closeEditor}
      >
        {editing ? (
          <form onSubmit={(event) => void handleSave(event)}>
            {editError ? (
              <ZHPageNotice variant="error" message={editError} />
            ) : null}
            <fieldset disabled={saving}>
              <ZHField label="RUC">
                <input
                  className="zh-input"
                  aria-label="RUC"
                  value={taxId}
                  maxLength={13}
                  placeholder={
                    editing.ruc.startsWith("TMP-EC-")
                      ? "Pendiente: ingrese el RUC real"
                      : "RUC"
                  }
                  onChange={(event) => setTaxId(event.target.value)}
                />
              </ZHField>
              <ZHField label="Razón social" required>
                <input
                  className="zh-input"
                  aria-label="Razón social"
                  required
                  maxLength={200}
                  value={editing.legalName}
                  onChange={(event) =>
                    setEditing({ ...editing, legalName: event.target.value })
                  }
                />
              </ZHField>
              <ZHField label="Nombre comercial">
                <input
                  className="zh-input"
                  aria-label="Nombre comercial"
                  maxLength={200}
                  value={editing.tradeName ?? ""}
                  onChange={(event) =>
                    setEditing({ ...editing, tradeName: event.target.value })
                  }
                />
              </ZHField>
              <label>
                <input
                  type="checkbox"
                  checked={editing.isActive}
                  onChange={(event) =>
                    setEditing({ ...editing, isActive: event.target.checked })
                  }
                />{" "}
                Empresa activa
              </label>
              <ZHBtn type="submit" variant="primary">
                {saving ? "Guardando..." : "Guardar"}
              </ZHBtn>
              <ZHBtn
                type="button"
                variant="ghost"
                onClick={() => setEditing(null)}
              >
                Cancelar
              </ZHBtn>
            </fieldset>
          </form>
        ) : null}
      </ZHModal>
      {error ? <ZHPageNotice variant="error" message={error} /> : null}
      {groups.length === 0 && !loading ? (
        <ZHCard>
          <p>No hay empresas registradas todavía.</p>
        </ZHCard>
      ) : (
        groups.map((group) => (
          <ZHCard
            key={group.tenantId}
            title={`${group.tenantName}${group.tenantIsActive ? "" : " (tenant inactivo)"}`}
            actions={
              <Link to={`/admin-core/companies/new?tenantId=${group.tenantId}`}>
                <ZHBtn variant="ghost" size="sm" type="button">
                  Crear empresa en este tenant
                </ZHBtn>
              </Link>
            }
            className="zh-mb-16"
          >
            <ZHDataTable
              columns={columns}
              rows={group.companies}
              rowKey={(r) => r.companyId}
              loading={loading}
              emptyMessage="Sin empresas."
            />
          </ZHCard>
        ))
      )}
    </PageShell>
  );
}
