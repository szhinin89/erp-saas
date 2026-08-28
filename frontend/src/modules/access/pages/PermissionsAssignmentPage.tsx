import { useCallback, useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { useI18n } from "../../../i18n/i18n";
import { profileService, type Profile } from "../api/profileService";
import {
  adminPermissionsService,
  type PermissionCatalog,
  type PermissionCatalogItem,
} from "../api/adminPermissionsService";
import { ZHField, ZHToggle, ZHFormActions, ZHBtn } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { NoAccessPage, Badge } from "../../../components/PageShell";
import { ZhSelect, ZhTextInput } from "../../../components/zh/inputs";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { formatApiRequestError } from "../../lib/apiError";
import { message } from "../../../lib/messages";
import "./ProfilesPage.css";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { useAuthStore } from "../../../store/authStore";

/**
 * ADMIN-PERMISSIONS-SSOT-KERNEL-02: el árbol de grupos/pantallas/acciones se carga desde
 * `GET /api/v1/admin/permissions/catalog` — derivado 100% de KernelRegistry en el backend
 * (mismo origen que el menú server-driven). No hay ningún catálogo de permisos hardcodeado en
 * este archivo: agregar un `[NavItem]` nuevo en el Kernel lo hace aparecer aquí automáticamente,
 * sin tocar este componente. No crea usuarios ni perfiles: reutiliza los mismos endpoints ya
 * existentes de AccessProfilesController (GET/PUT .../profiles/{id}/permissions).
 *
 * PERMISSIONS-ASSIGNMENT-UI-COMPACT-03: refactor puramente visual — mismo estado/mismos
 * handlers de guardado/filtro/selección de perfil que antes, solo cambia cómo se presenta cada
 * pantalla (bloque compacto tipo card con grid de acciones, en vez de una lista vertical larga).
 * No existe el concepto de override de permisos por usuario en el dominio (solo perfil→permiso),
 * así que esta pantalla asigna permisos únicamente por perfil.
 */
export function PermissionsAssignmentPage() {
  const { t } = useI18n();
  const user = useAuthStore((s) => s.user);
  const { canShow, isAdminRole } = usePermissionsUi();
  const canManage = canShow("access.profiles.view");

  const [searchParams, setSearchParams] = useSearchParams();
  const selectedProfileId = searchParams.get("profileId") ?? "";

  const [catalog, setCatalog] = useState<PermissionCatalog | null>(null);
  const [catalogLoading, setCatalogLoading] = useState(false);
  const [catalogError, setCatalogError] = useState("");

  const [profiles, setProfiles] = useState<Profile[]>([]);
  const [listLoading, setListLoading] = useState(false);
  const [listError, setListError] = useState("");

  const [filterText, setFilterText] = useState("");
  const [collapsedIds, setCollapsedIds] = useState<Set<string>>(new Set());

  const [permState, setPermState] = useState<Record<string, boolean>>({});
  const [permLoading, setPermLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState("");
  const [rejectedPerms, setRejectedPerms] = useState<
    { permissionKey: string; reason: string }[]
  >([]);

  /* ── Catálogo dinámico (una sola vez, independiente del perfil) ─────── */
  const loadCatalog = useCallback(async () => {
    setCatalogLoading(true);
    setCatalogError("");
    try {
      const res = await adminPermissionsService.getCatalog();
      setCatalog(res ?? { groups: [] });
    } catch (err) {
      setCatalogError(
        formatApiRequestError(err, {
          generic: t("permissionsAssignment.error.loadCatalog"),
        }),
      );
    } finally {
      setCatalogLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void loadCatalog();
  }, [loadCatalog]);

  const allItems = useMemo<PermissionCatalogItem[]>(
    () => catalog?.groups.flatMap((g) => g.items) ?? [],
    [catalog],
  );

  const allActionCodes = useMemo(() => {
    const codes = new Set<string>();
    for (const item of allItems) for (const action of item.actions) codes.add(action.code);
    return codes;
  }, [allItems]);

  const buildEmptyPermState = useCallback((): Record<string, boolean> => {
    const s: Record<string, boolean> = {};
    for (const code of allActionCodes) s[code] = false;
    return s;
  }, [allActionCodes]);

  /* ── Load profile list ─────────────────────────────────────── */
  const loadProfiles = useCallback(async () => {
    setListLoading(true);
    setListError("");
    try {
      setProfiles((await profileService.list(false)) ?? []);
    } catch (err) {
      setListError(
        formatApiRequestError(err, {
          generic: t("permissionsAssignment.error.loadProfiles"),
        }),
      );
    } finally {
      setListLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void loadProfiles();
  }, [loadProfiles]);

  /* ── Load permissions for the selected profile ─────────────── */
  const loadPermissions = useCallback(
    async (profileId: string) => {
      setPermLoading(true);
      setSaveError("");
      setRejectedPerms([]);
      try {
        const res = await profileService.getPermissions(profileId);
        const next = buildEmptyPermState();
        for (const item of res?.items ?? []) {
          if (item.permissionKey in next) next[item.permissionKey] = !!item.isAllowed;
        }
        setPermState(next);
      } catch (err) {
        setPermState(buildEmptyPermState());
        setSaveError(formatApiRequestError(err, { generic: t("profiles.perms.error.load") }));
      } finally {
        setPermLoading(false);
      }
    },
    [buildEmptyPermState, t],
  );

  useEffect(() => {
    if (!catalog) return;
    if (selectedProfileId) void loadPermissions(selectedProfileId);
    else setPermState(buildEmptyPermState());
  }, [selectedProfileId, loadPermissions, buildEmptyPermState, catalog]);

  const selectedProfile = useMemo(
    () => profiles.find((p) => p.id === selectedProfileId) ?? null,
    [profiles, selectedProfileId],
  );

  const onSelectProfile = (profileId: string) => {
    if (profileId) setSearchParams({ profileId });
    else setSearchParams({});
  };

  /* ── Toggle con cascada por ítem: cualquier acción distinta de la principal
     (actions[0], el permiso de acceso) exige que la principal esté activa; apagar la
     principal apaga el resto de acciones del mismo ítem. Sin cambios de comportamiento. ── */
  const toggleAction = (item: PermissionCatalogItem, actionCode: string, checked: boolean) => {
    setPermState((state) => {
      const next = { ...state, [actionCode]: checked };
      const baseCode = item.actions[0]?.code;
      if (baseCode) {
        if (checked && actionCode !== baseCode) next[baseCode] = true;
        if (!checked && actionCode === baseCode) {
          for (const action of item.actions) next[action.code] = false;
        }
      }
      return next;
    });
  };

  /* ── Acciones rápidas: conveniencias de edición masiva sobre el mismo permState,
     mismo mecanismo que togglear una a una — no llaman a ningún servicio nuevo. ── */
  const markAll = () => {
    setPermState((state) => {
      const next = { ...state };
      for (const code of allActionCodes) next[code] = true;
      return next;
    });
  };

  const unmarkAll = () => {
    setPermState(buildEmptyPermState());
  };

  const onlyAccess = () => {
    setPermState(() => {
      const next = buildEmptyPermState();
      for (const item of allItems) {
        const baseCode = item.actions[0]?.code;
        if (baseCode) next[baseCode] = true;
      }
      return next;
    });
  };

  const toggleCollapsed = (itemId: string) => {
    setCollapsedIds((prev) => {
      const next = new Set(prev);
      if (next.has(itemId)) next.delete(itemId);
      else next.add(itemId);
      return next;
    });
  };

  /* ── Save ────────────────────────────────────────────────────── */
  const onSave = async () => {
    if (!selectedProfileId || !selectedProfile || saving) return;

    const activeCount = allItems.reduce(
      (sum, item) => sum + item.actions.filter((action) => permState[action.code]).length,
      0,
    );

    const confirmed = await message.confirm({
      title: `Guardar permisos de "${selectedProfile.name}"`,
      message: (
        <>
          <p className="zh-confirm-message">
            Vas a actualizar los permisos del perfil <strong>{selectedProfile.name}</strong>. Todos
            los usuarios con este perfil pueden ver sus accesos y acciones disponibles cambiar de
            inmediato.
          </p>
          <p className="zh-confirm-message">
            Permisos que quedarán activos: <strong>{activeCount}</strong> de {totalActionsCount}.
          </p>
        </>
      ),
      variant: "warning",
      confirmLabel: t("permissionsAssignment.saveAction"),
      cancelLabel: t("common.cancel"),
    });
    if (!confirmed) return;

    setSaving(true);
    setSaveError("");
    try {
      // Bloqueo local defensivo: nunca enviar un código que no venga del catálogo cargado,
      // aunque estructuralmente la UI ya solo renderiza toggles por código de catálogo.
      const permItems = Object.entries(permState)
        .filter(([permissionKey]) => allActionCodes.has(permissionKey))
        .map(([permissionKey, isAllowed]) => ({ permissionKey, isAllowed }));
      const upsertResult = await profileService.upsertPermissions(selectedProfileId, permItems);
      const planRejections = (upsertResult?.rejected ?? []).filter(
        (r) => r.rejectionCode === "blocked_by_plan",
      );
      setRejectedPerms(planRejections);
      message.success(
        t("permissionsAssignment.saveSuccess", "Permisos guardados correctamente."),
      );
    } catch (err) {
      setSaveError(formatApiRequestError(err, { generic: t("profiles.perms.error.save") }));
    } finally {
      setSaving(false);
    }
  };

  /* ── Filtro por texto sobre grupo/pantalla ───────────────────── */
  const normalizedFilter = filterText.trim().toLowerCase();
  const visibleGroups = useMemo(() => {
    if (!catalog) return [];
    if (!normalizedFilter) return catalog.groups;
    return catalog.groups
      .map((g) => {
        const groupLabel = t(g.labelKey).toLowerCase();
        if (groupLabel.includes(normalizedFilter)) return g;
        const items = g.items.filter((i) => t(i.labelKey).toLowerCase().includes(normalizedFilter));
        return items.length > 0 ? { ...g, items } : null;
      })
      .filter((g): g is NonNullable<typeof g> => g !== null);
  }, [catalog, normalizedFilter, t]);

  /* ── Resumen: pantallas totales, permisos totales, acceso vs. acciones ─ */
  const totalActionsCount = allItems.reduce((sum, item) => sum + item.actions.length, 0);
  const accessCount = allItems.length;
  const relatedActionsCount = totalActionsCount - accessCount;

  if (!user || (!isAdminRole && !canManage)) {
    return <NoAccessPage title={t("permissionsAssignment.title")} />;
  }

  return (
    <ErpPageTemplate
      kicker={t("app.nav.group.admin")}
      title={t("permissionsAssignment.title")}
      subtitle={t("permissionsAssignment.subtitle")}
      action={
        <div className="pa-header-toolbar">
          <ZHField className="pa-header-field" label={t("permissionsAssignment.selectProfile")}>
            <ZhSelect
              className="zh-input"
              value={selectedProfileId}
              disabled={listLoading}
              onChange={(e) => onSelectProfile(e.target.value)}
            >
              <option value="">{t("permissionsAssignment.selectProfilePlaceholder")}</option>
              {profiles.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </ZhSelect>
          </ZHField>
          <ZhTextInput
            className="zh-input pa-filter-input"
            placeholder={t("permissionsAssignment.filterPlaceholder")}
            value={filterText}
            onChange={(e) => setFilterText(e.target.value)}
            aria-label={t("permissionsAssignment.filterPlaceholder")}
          />
        </div>
      }
    >
      <ZHPageNotice variant="info" message={t("permissionsAssignment.sourceNotice")} />

      {listError ? (
        <ZHPageNotice variant="error" message={t("common.errorPrefix")} detail={listError} />
      ) : null}
      {catalogError ? (
        <ZHPageNotice variant="error" message={t("common.errorPrefix")} detail={catalogError} />
      ) : null}

      {!selectedProfile ? (
        <p className="subtle pg-state-pad">{t("permissionsAssignment.noProfileSelected")}</p>
      ) : catalogLoading ? (
        <p className="subtle pg-state-pad">{t("common.loading")}</p>
      ) : allItems.length === 0 ? (
        <p className="subtle pg-state-pad">{t("permissionsAssignment.emptyCatalog")}</p>
      ) : (
        <>
          <div className="pa-quick-actions">
            <span className="pa-quick-actions-label">
              {t("permissionsAssignment.quickActions.label")}
            </span>
            <ZHBtn variant="secondary" size="xs" type="button" onClick={markAll}>
              {t("permissionsAssignment.quickActions.markAll")}
            </ZHBtn>
            <ZHBtn variant="secondary" size="xs" type="button" onClick={unmarkAll}>
              {t("permissionsAssignment.quickActions.unmarkAll")}
            </ZHBtn>
            <ZHBtn variant="secondary" size="xs" type="button" onClick={onlyAccess}>
              {t("permissionsAssignment.quickActions.onlyAccess")}
            </ZHBtn>
            <span className="pa-legend">
              <span className="pa-legend-item">
                <span className="pa-legend-dot pa-legend-dot--access" />
                {t("permissionsAssignment.legend.access")}
              </span>
              <span className="pa-legend-item">
                <span className="pa-legend-dot pa-legend-dot--action" />
                {t("permissionsAssignment.legend.action")}
              </span>
            </span>
          </div>

          {permLoading ? (
            <p className="subtle prf-modal-loading">{t("common.loading")}</p>
          ) : visibleGroups.length === 0 ? (
            <p className="subtle pg-state-pad">{t("permissionsAssignment.noFilterMatches")}</p>
          ) : (
            visibleGroups.map((group) => (
              <div key={group.code} className="pa-group">
                <h4 className="pa-group-title">{t(group.labelKey)}</h4>
                {group.items.map((item) => {
                  const collapsed = collapsedIds.has(item.id);
                  const itemLabel = t(item.labelKey);
                  return (
                    <div key={item.id} className="pg-section pa-item-card">
                      <button
                        type="button"
                        className="pg-section-header pa-item-header"
                        onClick={() => toggleCollapsed(item.id)}
                        aria-expanded={!collapsed}
                        aria-label={t(
                          collapsed
                            ? "permissionsAssignment.expandItem"
                            : "permissionsAssignment.collapseItem",
                          { name: itemLabel },
                        )}
                      >
                        <span className="pg-section-header-left pa-item-header-left">
                          <span className="material-symbols-outlined pg-section-icon pa-item-chevron">
                            {collapsed ? "chevron_right" : "expand_more"}
                          </span>
                          <span className="pa-item-title-wrap">
                            <span className="pa-item-title">{itemLabel}</span>
                            <Badge label={item.route} variant="neutral" code />
                          </span>
                        </span>
                        <Badge
                          label={t("permissionsAssignment.actionsCount", {
                            count: item.actions.length,
                          })}
                          variant="neutral"
                        />
                      </button>
                      {!collapsed && (
                        <div className="pg-section-body pa-actions-grid">
                          {item.actions.map((action, index) => (
                            <div
                              key={action.code}
                              title={action.code}
                              className={
                                index === 0
                                  ? "pa-action-tile pa-action-tile--access"
                                  : "pa-action-tile pa-action-tile--action"
                              }
                            >
                              <ZHToggle
                                label={action.label}
                                description={action.description}
                                value={!!permState[action.code]}
                                onChange={(checked) => toggleAction(item, action.code, checked)}
                              />
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                  );
                })}
              </div>
            ))
          )}

          <div className="pa-summary-bar">
            <span className="material-symbols-outlined pa-summary-icon">verified_user</span>
            <span className="subtle pa-summary-text">
              {t("permissionsAssignment.summary.total", {
                screens: accessCount,
                permissions: totalActionsCount,
              })}
            </span>
            <Badge
              label={t("permissionsAssignment.summary.access", { count: accessCount })}
              variant="info"
            />
            <Badge
              label={t("permissionsAssignment.summary.actions", { count: relatedActionsCount })}
              variant="success"
            />
          </div>

          {rejectedPerms.length > 0 && (
            <ZHPageNotice
              variant="warning"
              message={`${rejectedPerms.length} permiso(s) no guardados -- fuera del plan`}
              detail={rejectedPerms.map((r) => `${r.permissionKey}: ${r.reason}`).join("\n")}
            />
          )}

          {saveError && (
            <ZHPageNotice variant="error" message={t("common.errorPrefix")} detail={saveError} />
          )}

          <ZHFormActions
            hideDraft
            hideCancel
            disableSave={saving || permLoading}
            onSave={() => void onSave()}
            labels={{
              cancel: t("common.cancel"),
              save: saving ? t("common.saving") : t("permissionsAssignment.saveAction"),
            }}
          />
        </>
      )}
    </ErpPageTemplate>
  );
}
