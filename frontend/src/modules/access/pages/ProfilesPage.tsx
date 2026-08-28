import { useCallback, useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useI18n } from "../../../i18n/i18n";
import { profileService, type Profile } from "../api/profileService";
import {
  ZHField,
  ZHBtn,
  ZHGrid,
  ZHFormActions,
} from "../../../components/zh/ZHForm";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { NoAccessPage } from "../../../components/PageShell";
import {
  ZhTextInput,
  ZhSelect,
  ZhTextarea,
} from "../../../components/zh/inputs";
import { ErpPageTemplate } from "../../../templates/ErpPageTemplate";
import {
  profileCreateSchema,
  type ProfileCreateFormValues,
} from "../../../schemas/access/profileSchema";
import { formatApiRequestError } from "../../lib/apiError";
import { applyServerErrors } from "../../lib/validationErrors";
import "./ProfilesPage.css";
import { usePermissionsUi } from "../../../access/usePermissionsUi";
import { useAuthStore } from "../../../store/authStore";

/**
 * ADMINISTRATION-CLEAN-ACCESS-01: pantalla de responsabilidad única — CRUD de perfiles
 * (nombre/descripción/estado) solamente. La asignación de permisos vive en su propia pantalla
 * (PermissionsAssignmentPage, /admin/permissions) — el botón "Gestionar permisos" de cada fila
 * navega ahí en vez de abrir un formulario mezclado.
 */
export function ProfilesPage() {
  const { t } = useI18n();
  const user = useAuthStore((s) => s.user);
  const { canShow, isAdminRole } = usePermissionsUi();
  const canManage = canShow("access.profiles.view");
  const navigate = useNavigate();

  /* list state */
  const [profiles, setProfiles] = useState<Profile[]>([]);
  const [listLoading, setListLoading] = useState(false);
  const [listError, setListError] = useState("");
  const [search, setSearch] = useState("");

  /* modal state */
  const [modalOpen, setModalOpen] = useState(false);
  const [editTarget, setEditTarget] = useState<Profile | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState("");

  /* form */
  const {
    register,
    handleSubmit,
    reset,
    setError: setFieldError,
    formState: { errors },
  } = useForm<ProfileCreateFormValues>({
    resolver: zodResolver(profileCreateSchema),
    defaultValues: { name: "", description: "", isActive: true },
  });

  /* ── Load list ─────────────────────────────────────────────── */
  const loadProfiles = useCallback(async () => {
    setListLoading(true);
    setListError("");
    try {
      setProfiles((await profileService.list(false)) ?? []);
    } catch (err) {
      setListError(
        formatApiRequestError(err, { generic: t("profiles.error.load") }),
      );
    } finally {
      setListLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void loadProfiles();
  }, [loadProfiles]);

  /* ── Filtered list ─────────────────────────────────────────── */
  const filteredProfiles = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return profiles;
    return profiles.filter((p) =>
      `${p.name} ${p.description ?? ""}`.toLowerCase().includes(q),
    );
  }, [profiles, search]);

  /* ── Open modal (create or edit) ───────────────────────────── */
  const openCreate = () => {
    setEditTarget(null);
    reset({ name: "", description: "", isActive: true });
    setSaveError("");
    setModalOpen(true);
  };

  const openEdit = (p: Profile) => {
    setEditTarget(p);
    reset({
      name: p.name,
      description: p.description ?? "",
      isActive: p.isActive,
    });
    setSaveError("");
    setModalOpen(true);
  };

  const closeModal = () => {
    setModalOpen(false);
    setEditTarget(null);
    setSaveError("");
  };

  const managePermissions = (p: Profile) => {
    navigate(`/admin/permissions?profileId=${p.id}`);
  };

  /* ── Save (create or update) ───────────────────────────────── */
  const onSubmit = handleSubmit(async (values) => {
    setSaving(true);
    setSaveError("");
    try {
      if (editTarget) {
        await profileService.update({
          ...editTarget,
          name: values.name,
          description: values.description ?? null,
          isActive: values.isActive ?? true,
        });
      } else {
        await profileService.create(values.name, values.description || null);
      }
      await loadProfiles();
      closeModal();
    } catch (err) {
      const applied = applyServerErrors(err, setFieldError, (msg) =>
        setSaveError(msg),
      );
      if (!applied)
        setSaveError(
          formatApiRequestError(err, { generic: t("profiles.error.create") }),
        );
    } finally {
      setSaving(false);
    }
  });

  /* ── Quick toggle active/inactive ──────────────────────────── */
  const toggleActive = async (p: Profile) => {
    setListError("");
    try {
      await profileService.update({ ...p, isActive: !p.isActive });
      await loadProfiles();
    } catch {
      setListError(t("profiles.error.update"));
    }
  };

  if (!user || (!isAdminRole && !canManage)) {
    return <NoAccessPage title={t("profiles.title")} />;
  }

  return (
    <ErpPageTemplate
      kicker={t("app.nav.group.admin")}
      title={t("profiles.title")}
      subtitle={t("profiles.subtitle")}
      action={
        <ZHBtn variant="primary" size="md" type="button" onClick={openCreate}>
          + {t("profiles.list.newAction")}
        </ZHBtn>
      }
    >
      {listError ? (
        <ZHPageNotice
          variant="error"
          message={t("common.errorPrefix")}
          detail={listError}
        />
      ) : null}

      {/* ── Table section ──────────────────────────────────────── */}
      <div className="pg-section">
        <div className="pg-table-controls">
          <div className="pg-search">
            <span className="material-symbols-outlined">search</span>
            <ZhTextInput
              className="zh-input"
              placeholder={t("common.zhList.searchPlaceholder")}
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
          <span className="pg-result-count">
            {filteredProfiles.length} {t("common.zhList.entityLabel")}
          </span>
        </div>

        {listLoading ? (
          <p className="subtle pg-state-pad">{t("common.loading")}</p>
        ) : filteredProfiles.length === 0 ? (
          <p className="subtle pg-state-pad">{t("common.noData")}</p>
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>{t("profiles.table.name")}</th>
                <th>{t("profiles.table.active")}</th>
                <th className="pg-th-actions">{t("common.actions")}</th>
              </tr>
            </thead>
            <tbody>
              {filteredProfiles.map((p) => (
                <tr key={p.id}>
                  <td>
                    <strong>{p.name}</strong>
                    {p.description ? (
                      <p className="subtle pg-desc-subtle">{p.description}</p>
                    ) : null}
                  </td>
                  <td>
                    <span
                      className={`zh-status zh-status--${p.isActive ? "active" : "inactive"}`}
                    >
                      {p.isActive ? t("common.active") : t("common.inactive")}
                    </span>
                  </td>
                  <td>
                    <div className="prf-row-actions">
                      <ZHBtn
                        variant="ghost"
                        size="md"
                        type="button"
                        onClick={() => openEdit(p)}
                      >
                        {t("common.edit")}
                      </ZHBtn>
                      <ZHBtn
                        variant="ghost"
                        size="md"
                        type="button"
                        onClick={() => managePermissions(p)}
                      >
                        {t("profiles.actions.permissions")}
                      </ZHBtn>
                      <ZHBtn
                        variant="ghost"
                        size="md"
                        type="button"
                        onClick={() => void toggleActive(p)}
                      >
                        {p.isActive
                          ? t("profiles.actions.disable")
                          : t("profiles.actions.enable")}
                      </ZHBtn>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <ZHModal
        open={modalOpen}
        onClose={closeModal}
        size="md"
        title={editTarget ? t("profiles.form.edit") : t("profiles.form.create")}
        subtitle={t("profiles.subtitle")}
      >
        <form onSubmit={onSubmit}>
          <div className="pg-section prf-modal-section-flush">
            <ZHGrid cols={2}>
              <ZHField
                label={t("profiles.form.name")}
                required
                error={errors.name?.message}
              >
                <ZhTextInput
                  className="zh-input"
                  {...register("name")}
                  placeholder="Ej. Analista de Finanzas Jr."
                />
              </ZHField>
              <ZHField label={t("profiles.form.status")}>
                <ZhSelect
                  {...register("isActive", {
                    setValueAs: (v) => v === "true" || v === true,
                  })}
                >
                  <option value="true">{t("common.active")}</option>
                  <option value="false">{t("common.inactive")}</option>
                </ZhSelect>
              </ZHField>
            </ZHGrid>
            <div className="prf-modal-field-offset">
              <ZHField
                label={t("profiles.form.description")}
                error={errors.description?.message}
              >
                <ZhTextarea
                  className="zh-input"
                  rows={3}
                  placeholder={t("profiles.form.descriptionPlaceholder")}
                  {...register("description")}
                />
              </ZHField>
            </div>
          </div>

          {saveError && (
            <div className="prf-modal-error-wrap">
              <ZHPageNotice
                variant="error"
                message={t("common.errorPrefix")}
                detail={saveError}
              />
            </div>
          )}

          <ZHFormActions
            onCancel={closeModal}
            hideDraft
            saveButtonType="submit"
            disableSave={saving}
            labels={{
              cancel: t("common.cancel"),
              save: saving
                ? t("common.saving")
                : t("profiles.form.saveProfile"),
            }}
          />
        </form>
      </ZHModal>
    </ErpPageTemplate>
  );
}
