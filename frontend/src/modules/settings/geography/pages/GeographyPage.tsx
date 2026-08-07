import { useEffect, useState } from "react";
import {
  EmptyState,
  LoadingState,
  NoAccessPage,
} from "../../../../components/PageShell";
import { ErpPageTemplate } from "../../../../templates/ErpPageTemplate";
import { ReportKpiCard } from "../../../../components/ReportPageTemplate";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import { ZhSelect } from "../../../../components/zh/inputs";
import { useI18n } from "../../../../i18n/i18n";
import { branchService } from "../../../branches/api/branchService";
import { usePermissionsUi } from "../../../../access/usePermissionsUi";

type GeoItem = { id: string; name: string };

export function GeographyPage() {
  const { canShow } = usePermissionsUi();
  const { t } = useI18n();
  const canView = canShow("settings.geography.view");

  const [countries, setCountries] = useState<GeoItem[]>([]);
  const [provinces, setProvinces] = useState<GeoItem[]>([]);
  const [cantons, setCantons] = useState<GeoItem[]>([]);
  const [parishes, setParishes] = useState<GeoItem[]>([]);

  const [countryId, setCountryId] = useState("");
  const [provinceId, setProvinceId] = useState("");
  const [cantonId, setCantonId] = useState("");

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!canView) return;
    setLoading(true);
    branchService
      .countries()
      .then((rows) => {
        setCountries(rows);
        if (rows.length > 0) setCountryId(rows[0].id);
      })
      .catch((e) => setError(e instanceof Error ? e.message : String(e)))
      .finally(() => setLoading(false));
  }, [canView]);

  useEffect(() => {
    if (!countryId) {
      setProvinces([]);
      setProvinceId("");
      return;
    }
    branchService
      .provinces(countryId)
      .then(setProvinces)
      .catch(() => setProvinces([]));
    setProvinceId("");
    setCantonId("");
  }, [countryId]);

  useEffect(() => {
    if (!provinceId) {
      setCantons([]);
      setCantonId("");
      return;
    }
    branchService
      .cantons(provinceId)
      .then(setCantons)
      .catch(() => setCantons([]));
    setCantonId("");
  }, [provinceId]);

  useEffect(() => {
    if (!cantonId) {
      setParishes([]);
      return;
    }
    branchService
      .parishes(cantonId)
      .then(setParishes)
      .catch(() => setParishes([]));
  }, [cantonId]);

  if (!canView)
    return <NoAccessPage title={t("app.nav.item.settings.geography")} />;

  const selectedCountry = countries.find((c) => c.id === countryId);
  const selectedProvince = provinces.find((p) => p.id === provinceId);
  const selectedCanton = cantons.find((c) => c.id === cantonId);

  return (
    <ErpPageTemplate
      kicker={t("app.nav.group.settings")}
      title={t("app.nav.item.settings.geography")}
      subtitle="Catálogo de países, provincias, cantones y parroquias (solo lectura)."
    >
      {error && (
        <ZHPageNotice
          variant="error"
          message="Error al cargar geografía"
          detail={error}
        />
      )}

      <div className="pg-section">
        <div className="pg-table-controls">
          <div className="pg-table-controls-left">
            <ZhSelect
              value={countryId}
              disabled={loading}
              onChange={(e) => setCountryId(e.target.value)}
            >
              <option value="">País…</option>
              {countries.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </ZhSelect>
            <ZhSelect
              value={provinceId}
              disabled={!countryId}
              onChange={(e) => setProvinceId(e.target.value)}
            >
              <option value="">Provincia…</option>
              {provinces.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </ZhSelect>
            <ZhSelect
              value={cantonId}
              disabled={!provinceId}
              onChange={(e) => setCantonId(e.target.value)}
            >
              <option value="">Cantón…</option>
              {cantons.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </ZhSelect>
          </div>
        </div>

        {loading ? (
          <div className="pg-pad-40">
            <LoadingState />
          </div>
        ) : countries.length === 0 ? (
          <div className="pg-pad-40">
            <EmptyState message="No hay datos geográficos disponibles." />
          </div>
        ) : (
          <>
            <div className="pg-kpis">
              <ReportKpiCard
                layout="horizontal"
                label="Países"
                value={String(countries.length)}
              />
              <ReportKpiCard
                layout="horizontal"
                label="Provincias"
                value={String(provinces.length)}
              />
              <ReportKpiCard
                layout="horizontal"
                label="Cantones"
                value={String(cantons.length)}
              />
              <ReportKpiCard
                layout="horizontal"
                label="Parroquias"
                value={String(parishes.length)}
              />
            </div>

            {selectedCountry && (
              <p className="subtle pg-text-muted-sm pg-mb-16">
                Ruta: <strong>{selectedCountry.name}</strong>
                {selectedProvince ? ` › ${selectedProvince.name}` : ""}
                {selectedCanton ? ` › ${selectedCanton.name}` : ""}
              </p>
            )}

            {cantonId && parishes.length === 0 ? (
              <EmptyState message="No hay parroquias para el cantón seleccionado." />
            ) : (
              <div className="table-scroll">
                <table className="table">
                  <thead>
                    <tr>
                      <th>Nivel</th>
                      <th>Código</th>
                      <th>Nombre</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(cantonId
                      ? parishes
                      : provinceId
                        ? cantons
                        : countryId
                          ? provinces
                          : countries
                    ).map((item) => (
                      <tr key={item.id}>
                        <td>
                          {cantonId
                            ? "Parroquia"
                            : provinceId
                              ? "Cantón"
                              : countryId
                                ? "Provincia"
                                : "País"}
                        </td>
                        <td className="mono">{item.id}</td>
                        <td>{item.name}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </>
        )}
      </div>
    </ErpPageTemplate>
  );
}
