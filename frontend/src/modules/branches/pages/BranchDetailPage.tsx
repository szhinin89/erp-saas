import { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import { NoAccessPage, LoadingState } from "../../../components/PageShell";
import { ZHLinkButton } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { ZHTabBar, type ZHTab } from "../../../components/zh/ZHTabBar";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { branchService, type BranchDetailDto } from "../api/branchService";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { useI18n } from "../../../i18n/i18n";
import { BranchConfigSection } from "../components/BranchConfigSection";
import { BranchGeneralSection } from "../components/BranchGeneralSection";
import "../../../styles/shared/items-catalog.css";

type Tab = "general" | "establishments" | "configuraciones";


export function BranchDetailPage() {
  const { id } = useParams<{ id: string }>();
  const { t } = useI18n();
  const { canShow } = usePermissionsUi();
  const canView = canShow("settings.branches.view");
  const canViewConfig = canShow("settings.company.view");

  const [branch, setBranch] = useState<BranchDetailDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [tab, setTab] = useState<Tab>("general");

  useEffect(() => {
    if (!id) return;
    setLoading(true);
    branchService
      .getById(id)
      .then(setBranch)
      .catch(() => setError("No se pudo cargar la sucursal."))
      .finally(() => setLoading(false));
  }, [id]);

  if (!canView) return <NoAccessPage title="Sucursal" />;

  if (loading) {
    return (
      <ErpPageTemplate kicker="Configuración" title="Cargando…">
        <LoadingState />
      </ErpPageTemplate>
    );
  }

  if (error || !branch) {
    return (
      <ErpPageTemplate kicker="Configuración" title="Sucursal">
        <ZHPageNotice
          variant="error"
          message="Error"
          detail={error || "Sucursal no encontrada."}
        />
        <ZHLinkButton to="/settings/branches" variant="ghost">
          ← Volver a sucursales
        </ZHLinkButton>
      </ErpPageTemplate>
    );
  }

  return (
    <ErpPageTemplate
      kicker="Sucursales"
      title={branch.name}
      subtitle={branch.address}
      action={
        <ZHLinkButton to="/settings/branches" variant="ghost">
          <span className="material-symbols-outlined">arrow_back</span>
          Volver
        </ZHLinkButton>
      }
    >
      <ZHTabBar
        tabs={[
          { id: "general", label: "General" },
          {
            id: "establishments",
            label: "Establecimientos",
            mobileLabel: t("branches.detail.tabs.establishmentsShort", "Establ."),
          },
          // BRANCH-CONFIG-READ-PERMISSION-01: la pestaña lee GET
          // /org-config/branch/{id}/invoice-defaults, que exige settings.company.view.
          ...(canViewConfig
            ? [
                {
                  id: "configuraciones" as const,
                  label: "Configuraciones",
                  mobileLabel: t("branches.detail.tabs.configurationsShort", "Config."),
                },
              ]
            : []),
        ] satisfies ZHTab<Tab>[]}
        activeTab={tab}
        onChange={setTab}
        ariaLabel="Secciones de la sucursal"
      />

      {tab === "general" && id && (
        <BranchGeneralSection
          branchId={id}
          initialData={branch}
          onSaved={(updated) => setBranch(updated)}
        />
      )}

      {tab === "configuraciones" && canViewConfig && id && (
        <BranchConfigSection branchId={id} />
      )}

      {tab === "establishments" && (
        <div className="pg-section">
          <div className="pg-section-header">
            <div className="pg-section-header-left">
              <span className="material-symbols-outlined pg-section-icon">
                receipt_long
              </span>
              <span className="pg-section-label">
                Establecimientos asociados
              </span>
            </div>
          </div>
          <div className="pg-section-body">
            <p className="zh-text-muted zh-mb-16">
              Los establecimientos se gestionan de forma independiente. Usa
              el módulo de Establecimientos para crear, editar y asignar
              establecimientos a esta sucursal.
            </p>
            <ZHLinkButton
              to={`/settings/establishments?branchId=${id}`}
              variant="primary"
              size="md"
            >
              <span className="material-symbols-outlined zh-icon-md">
                receipt_long
              </span>
              Ver establecimientos de esta sucursal
            </ZHLinkButton>
          </div>
        </div>
      )}
    </ErpPageTemplate>
  );
}
