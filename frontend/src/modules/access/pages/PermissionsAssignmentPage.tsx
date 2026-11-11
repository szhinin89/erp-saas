import { useCallback, useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { useI18n } from "../../../i18n/i18n";
import { profileService, type Profile } from "../api/profileService";
import {
  adminPermissionsService,
  type PermissionCatalog,
  type PermissionCatalogCategory,
  type PermissionCatalogItem,
} from "../api/adminPermissionsService";
import { ZHField, ZHFormActions, ZHBtn } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { NoAccessPage, Badge } from "../../../components/PageShell";
import { ZhSelect, ZhTextInput } from "../../../components/zh/inputs";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import { formatApiRequestError } from "../../lib/apiError";
import { presentPermissionsMutationFailure } from "../permissionsMutationFailure";
import {
  categoryActionCodes,
  categorySelectionState as selectionStateOf,
  isMassAssignment,
  permStateDiff,
  toggleActionState,
} from "../permissionsAssignmentState";
import { PermissionsCatalogTree } from "../components/PermissionsCatalogTree";
import { PermissionGrantSummary } from "../components/PermissionGrantSummary";
import { message } from "../../../lib/messages";
import "./ProfilesPage.css";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { useAuthStore } from "../../../store/authStore";

/**
 * ADMIN-PERMISSIONS-SSOT-KERNEL-02 / NAV-HIERARCHY-UNIFY-01: el árbol de grupos/categorías/
 * pantallas/acciones se carga desde `GET /api/v1/admin/permissions/catalog` — derivado 100% de
 * KernelRegistry en el backend (mismo origen y misma jerarquía Módulo > Categoría > Pantalla que
 * el menú lateral server-driven). No hay ningún catálogo de permisos ni regla de categorización
 * hardcodeada en este archivo: agregar un `[NavItem]` nuevo en el Kernel lo hace aparecer aquí
 * automáticamente, bajo la categoría que le corresponda, sin tocar este componente. No crea
 * usuarios ni perfiles: reutiliza los mismos endpoints ya existentes de AccessProfilesController
 * (GET/PUT .../profiles/{id}/permissions).
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
  /** Estado guardado del perfil (para mostrar las diferencias antes de guardar). */
  const [savedPermState, setSavedPermState] = useState<Record<string, boolean>>({});
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
    () => catalog?.groups.flatMap((g) => g.categories.flatMap((c) => c.items)) ?? [],
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
        setSavedPermState(next);
      } catch (err) {
        setPermState(buildEmptyPermState());
        setSavedPermState(buildEmptyPermState());
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
    else {
      setPermState(buildEmptyPermState());
      setSavedPermState(buildEmptyPermState());
    }
  }, [selectedProfileId, loadPermissions, buildEmptyPermState, catalog]);

  const selectedProfile = useMemo(
    () => profiles.find((p) => p.id === selectedProfileId) ?? null,
    [profiles, selectedProfileId],
  );

  const onSelectProfile = (profileId: string) => {
    if (profileId) setSearchParams({ profileId });
    else setSearchParams({});
  };

  /* ── Toggle con cascada por ítem (regla en permissionsAssignmentState). ── */
  const toggleAction = (item: PermissionCatalogItem, actionCode: string, checked: boolean) => {
    setPermState((state) => toggleActionState(state, item, actionCode, checked));
  };

  /* ── Acciones rápidas: conveniencias de edición masiva sobre el mismo permState,
     mismo mecanismo que togglear una a una — no llaman a ningún servicio nuevo. ── */
  const markAll = async () => {
    // IAM-V1-F2: conceder todo el catálogo a un perfil operativo es excepcional — confirmación
    // explícita (el acceso total corresponde a la función Administrador de empresa).
    const confirmed = await message.confirm({
      title: t("permissionsAssignment.markAll.confirmTitle", "Marcar todos los permisos"),
      message: t(
        "permissionsAssignment.markAll.confirmMessage",
        "Este perfil quedará con acceso a todas las pantallas y acciones de la empresa. Para acceso total usa la función Administrador de empresa en el usuario.",
      ),
      variant: "warning",
      confirmLabel: t("permissionsAssignment.markAll.confirmAction", "Marcar todo"),
      cancelLabel: t("common.cancel"),
    });
    if (!confirmed) return;
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

  /* ── Selector masivo por categoría (Nivel 2): mismo mecanismo que markAll/unmarkAll, pero
     acotado a los actionCodes de una sola categoría — permite marcar/desmarcar toda una
     categoría del menú (p. ej. "Cuentas por pagar") de una sola vez. ── */
  const categorySelectionState = (category: PermissionCatalogCategory) =>
    selectionStateOf(category, permState);

  const toggleCategoryAll = (category: PermissionCatalogCategory) => {
    const codes = categoryActionCodes(category);
    const nextValue = categorySelectionState(category) !== "all";
    setPermState((state) => {
      const next = { ...state };
      for (const code of codes) next[code] = nextValue;
      return next;
    });
  };

  const isGrantedNow = useCallback((code: string) => !!permState[code], [permState]);

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

    const diff = permStateDiff(savedPermState, permState, allActionCodes);
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
            Permisos que quedarán activos: <strong>{activeCount}</strong> de {totalActionsCount}{" "}
            (se agregan <strong>{diff.added}</strong>, se quitan <strong>{diff.removed}</strong>).
          </p>
          {isMassAssignment(activeCount, totalActionsCount) ? (
            <p className="zh-confirm-message">
              <strong>
                {t(
                  "permissionsAssignment.massWarning",
                  "Atención: este perfil concederá más de la mitad de todos los permisos de la empresa. Revisa que sea una función operativa y no un acceso de administrador.",
                )}
              </strong>
            </p>
          ) : null}
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
      setSavedPermState(permState);
      message.success(
        t("permissionsAssignment.saveSuccess", "Permisos guardados correctamente."),
      );
    } catch (err) {
      const handled = await presentPermissionsMutationFailure(err, {
        t,
        show: (text) => setSaveError(text),
        reload: () => loadPermissions(selectedProfileId),
      });
      if (!handled)
        setSaveError(formatApiRequestError(err, { generic: t("profiles.perms.error.save") }));
    } finally {
      setSaving(false);
    }
  };

  /* ── Filtro por texto sobre grupo/categoría/pantalla ───────────────────── */
  const normalizedFilter = filterText.trim().toLowerCase();
  const visibleGroups = useMemo(() => {
    if (!catalog) return [];
    if (!normalizedFilter) return catalog.groups;
    return catalog.groups
      .map((g) => {
        const groupLabel = t(g.labelKey).toLowerCase();
        if (groupLabel.includes(normalizedFilter)) return g;
        const categories = g.categories
          .map((c) => {
            const categoryLabel = t(c.labelKey).toLowerCase();
            if (categoryLabel.includes(normalizedFilter)) return c;
            const items = c.items.filter((i) => t(i.labelKey).toLowerCase().includes(normalizedFilter));
            return items.length > 0 ? { ...c, items } : null;
          })
          .filter((c): c is NonNullable<typeof c> => c !== null);
        return categories.length > 0 ? { ...g, categories } : null;
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
        <div className="access-pa-header-toolbar">
          <ZHField className="access-pa-header-field" label={t("permissionsAssignment.selectProfile")}>
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
            className="zh-input access-pa-filter-input"
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
          <div className="access-pa-quick-actions">
            <span className="access-pa-quick-actions-label">
              {t("permissionsAssignment.quickActions.label")}
            </span>
            <ZHBtn variant="secondary" size="xs" type="button" onClick={() => void markAll()}>
              {t("permissionsAssignment.quickActions.markAll")}
            </ZHBtn>
            <ZHBtn variant="secondary" size="xs" type="button" onClick={unmarkAll}>
              {t("permissionsAssignment.quickActions.unmarkAll")}
            </ZHBtn>
            <ZHBtn variant="secondary" size="xs" type="button" onClick={onlyAccess}>
              {t("permissionsAssignment.quickActions.onlyAccess")}
            </ZHBtn>
            <span className="access-pa-legend">
              <span className="access-pa-legend-item">
                <span className="access-pa-legend-dot access-pa-legend-dot--access" />
                {t("permissionsAssignment.legend.access")}
              </span>
              <span className="access-pa-legend-item">
                <span className="access-pa-legend-dot access-pa-legend-dot--action" />
                {t("permissionsAssignment.legend.action")}
              </span>
            </span>
          </div>

          {permLoading ? (
            <p className="subtle prf-modal-loading">{t("common.loading")}</p>
          ) : visibleGroups.length === 0 ? (
            <p className="subtle pg-state-pad">{t("permissionsAssignment.noFilterMatches")}</p>
          ) : (
            <div className="access-pa-layout">
              <div className="access-pa-layout-main">
                <PermissionsCatalogTree
                  groups={visibleGroups}
                  permState={permState}
                  collapsedIds={collapsedIds}
                  onToggleCollapsed={toggleCollapsed}
                  onToggleAction={toggleAction}
                  onToggleCategory={toggleCategoryAll}
                  categorySelectionState={categorySelectionState}
                />
              </div>
              {catalog ? (
                <aside className="access-pa-layout-aside">
                  <PermissionGrantSummary
                    catalog={catalog}
                    isGranted={isGrantedNow}
                    title={t("permissionsSummary.title", "Este perfil concede")}
                  />
                </aside>
              ) : null}
            </div>
          )}

          <div className="access-pa-summary-bar">
            <span className="material-symbols-outlined access-pa-summary-icon">verified_user</span>
            <span className="subtle access-pa-summary-text">
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
