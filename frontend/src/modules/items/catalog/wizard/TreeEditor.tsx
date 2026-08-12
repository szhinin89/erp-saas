import { useCallback, useEffect, useMemo, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { ErpPageTemplate } from "../../../../templates/ErpPageTemplate";
import { LoadingState, EmptyState } from "../../../../components/PageShell";
import { ZHPageNotice } from "../../../../components/zh/ZHPageNotice";
import {
  ZHField,
  ZHGrid,
  ZHFormActions,
  ZHBtn,
} from "../../../../components/zh/ZHForm";
import { ZhNumberInput, ZhTextInput } from "../../../../components/zh/inputs";
import { Badge } from "../../../../components/PageShell";
import { ZHModal } from "../../../../components/zh/ZHModal";
import { useI18n } from "../../../../i18n/i18n";
import { applyServerErrors } from "../../../lib/validationErrors";
import { readApiErrorMessage } from "../../../lib/apiError";
import {
  categoryNodeService,
  type CategoryNodeDto,
} from "../api/categoryNodeService";
import "./catalog-wizard.css";

type NodeFormValues = {
  code: string;
  name: string;
  description?: string | null;
  level: string;
  sortOrder: number;
};

const LEVEL_ICONS: Record<string, { icon: string; className: string }> = {
  Family: { icon: "account_tree", className: "cat-te-icon--family" },
  Category: { icon: "category", className: "cat-te-icon--category" },
  Subcategory: { icon: "label", className: "cat-te-icon--subcategory" },
  Custom: { icon: "extension", className: "cat-te-icon--custom" },
};

type ModalTarget =
  | {
      action: "add";
      parentId: string | null;
      parentName: string;
      level: string;
    }
  | { action: "edit"; node: CategoryNodeDto };

export function TreeEditorPage() {
  const { t } = useI18n();
  const [nodes, setNodes] = useState<CategoryNodeDto[]>([]);
  const [maxDepth, setMaxDepth] = useState(3);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const [modal, setModal] = useState<ModalTarget | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState("");
  const [showInactive, setShowInactive] = useState(true);
  const nodeSchema = useMemo(
    () =>
      z.object({
        code: z
          .string()
          .trim()
          .min(1, t("catalog.tree.validation.codeRequired", "Código obligatorio."))
          .max(20)
          .regex(
            /^[A-Za-z0-9\-_]+$/,
            t(
              "catalog.tree.validation.codeFormat",
              "Solo letras, números y guiones.",
            ),
          ),
        name: z
          .string()
          .trim()
          .min(1, t("catalog.tree.validation.nameRequired", "Nombre obligatorio."))
          .max(120),
        description: z.string().max(500).nullable().optional(),
        level: z.string().min(1),
        sortOrder: z.number().min(0).default(0),
      }),
    [t],
  );

  const form = useForm<NodeFormValues>({
    resolver: zodResolver(nodeSchema),
    defaultValues: {
      code: "",
      name: "",
      description: null,
      level: "Family",
      sortOrder: 0,
    },
  });

  const loadTree = useCallback(async () => {
    setError("");
    setLoading(true);
    try {
      const result = await categoryNodeService.getTree(true);
      setNodes(result.nodes);
      setMaxDepth(result.maxDepth);
      setExpanded((prev) =>
        prev.size === 0
          ? new Set(result.nodes.filter((n) => !n.parentId).map((n) => n.id))
          : prev,
      );
    } catch {
      setError(t("catalog.tree.loadError", "Error al cargar el catálogo."));
    } finally {
      setLoading(false);
    }
  }, [t]);

  useEffect(() => {
    void loadTree();
  }, [loadTree]);

  const childrenOf = useMemo(() => {
    const map = new Map<string | null, CategoryNodeDto[]>();
    for (const n of nodes) {
      const key = n.parentId ?? "__root__";
      const arr = map.get(key) ?? [];
      arr.push(n);
      map.set(key, arr);
    }
    return map;
  }, [nodes]);

  const toggle = (id: string) =>
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });

  const getChildLevel = (parentLevel: string): string => {
    if (parentLevel === "Family") return "Category";
    if (parentLevel === "Category") return "Subcategory";
    return "Custom";
  };

  const openAdd = (
    parentId: string | null,
    parentName: string,
    level: string,
  ) => {
    form.reset({ code: "", name: "", description: null, level, sortOrder: 0 });
    setSaveError("");
    setModal({ action: "add", parentId, parentName, level });
  };

  const openEdit = (node: CategoryNodeDto) => {
    form.reset({
      code: node.code,
      name: node.name,
      description: node.description,
      level: node.level,
      sortOrder: node.sortOrder,
    });
    setSaveError("");
    setModal({ action: "edit", node });
  };

  const handleSave = form.handleSubmit(async (values) => {
    if (!modal) return;
    setSaveError("");
    setSaving(true);
    try {
      if (modal.action === "add") {
        await categoryNodeService.create({
          parentId: modal.parentId,
          code: values.code,
          name: values.name,
          description: values.description,
          level: values.level,
          sortOrder: values.sortOrder,
        });
      } else {
        await categoryNodeService.update(modal.node.id, {
          id: modal.node.id,
          code: values.code,
          name: values.name,
          description: values.description,
          sortOrder: values.sortOrder,
        });
      }
      setModal(null);
      await loadTree();
    } catch (err: unknown) {
      const applied = applyServerErrors(err, form.setError, (msg) =>
        setSaveError(msg),
      );
      if (!applied)
        setSaveError(
          readApiErrorMessage(err) ??
            t("catalog.tree.saveError", "Error al guardar."),
        );
    } finally {
      setSaving(false);
    }
  });

  const handleToggleStatus = async (id: string, isActive: boolean) => {
    setError("");
    try {
      if (isActive) await categoryNodeService.disable(id);
      else await categoryNodeService.enable(id);
      await loadTree();
    } catch {
      setError(
        t("catalog.tree.toggleError", "Error al cambiar el estado."),
      );
    }
  };

  const filterNodes = (list: CategoryNodeDto[]) =>
    showInactive ? list : list.filter((n) => n.isActive);

  const renderNode = (node: CategoryNodeDto, depth: number) => {
    const children = filterNodes(childrenOf.get(node.id) ?? []);
    const hasChildren = children.length > 0;
    const levelInfo = LEVEL_ICONS[node.level] ?? LEVEL_ICONS.Custom;

    return (
      <div key={node.id} className="cat-te-node">
        <div
          className={`cat-te-row cat-te-row--depth-${Math.min(depth, 6)} ${!node.isActive ? "cat-te-row--inactive" : ""}`}
        >
          {hasChildren ? (
            <button
              type="button"
              className="cat-te-expand"
              onClick={() => toggle(node.id)}
            >
              <span
                className={`material-symbols-outlined cat-te-expand-icon ${expanded.has(node.id) ? "cat-te-expand-icon--expanded" : ""}`}
              >
                chevron_right
              </span>
            </button>
          ) : (
            <span className="cat-te-expand cat-te-expand--leaf" />
          )}
          <span
            className={`material-symbols-outlined cat-te-icon ${levelInfo.className}`}
          >
            {levelInfo.icon}
          </span>
          <Badge label={node.code} variant="neutral" className="mono" />
          <strong className="cat-te-label">{node.name}</strong>
          <span
            className={`cat-te-level-badge cat-te-level-badge--${node.level.toLowerCase()}`}
          >
            {t(`catalog.tree.level.${node.level}`, node.level)}
          </span>
          <span
            className={
              node.isActive
                ? "zh-status zh-status--active"
                : "zh-status zh-status--inactive"
            }
          >
            {node.isActive
              ? t("common.active", "Activo")
              : t("common.inactive", "Inactivo")}
          </span>
          <div className="cat-te-actions">
            <ZHBtn
              type="button"
              variant="ghost"
              size="sm"
              title={t("common.edit", "Editar")}
              onClick={() => openEdit(node)}
            >
              <span className="material-symbols-outlined">edit</span>
            </ZHBtn>
            <ZHBtn
              type="button"
              variant="ghost"
              size="sm"
              title={
                node.isActive
                  ? t("common.deactivate", "Desactivar")
                  : t("common.activate", "Activar")
              }
              onClick={() => void handleToggleStatus(node.id, node.isActive)}
            >
              <span className="material-symbols-outlined">
                {node.isActive ? "block" : "check_circle"}
              </span>
            </ZHBtn>
            {node.isActive && node.depth < maxDepth && (
              <ZHBtn
                type="button"
                variant="ghost"
                size="sm"
                title={t("catalog.tree.addChild", "Agregar hijo")}
                onClick={() =>
                  openAdd(node.id, node.name, getChildLevel(node.level))
                }
              >
                <span className="material-symbols-outlined">add</span>
              </ZHBtn>
            )}
            {node.isActive && node.depth >= maxDepth && (
              <Badge
                label={t("catalog.tree.maxLevel", "Máx. nivel")}
                variant="neutral"
                title={t("catalog.tree.maxDepthReached", {
                  maxDepth,
                })}
              />
            )}
          </div>
        </div>
        {expanded.has(node.id) &&
          children.map((child) => renderNode(child, depth + 1))}
      </div>
    );
  };

  const roots = filterNodes(childrenOf.get("__root__") ?? []);

  return (
    <ErpPageTemplate
      kicker={t("catalog.kicker", "Catálogo")}
      title={t("catalog.tree.title", "Árbol de catálogo")}
    >
      {error && (
        <ZHPageNotice
          variant="error"
          message={t("common.error", "Error")}
          detail={error}
        />
      )}

      <div className="cat-te-toolbar">
        <ZHBtn
          variant="primary"
          size="sm"
          type="button"
          onClick={() =>
            openAdd(null, t("catalog.tree.root", "Raíz"), "Family")
          }
        >
          <span className="material-symbols-outlined">add</span>
          {t("catalog.tree.newFamily", "Nueva familia")}
        </ZHBtn>
        <ZHBtn
          variant="secondary"
          size="sm"
          type="button"
          onClick={() => void loadTree()}
          disabled={loading}
        >
          <span className="material-symbols-outlined">refresh</span>
          {t("common.refresh", "Actualizar")}
        </ZHBtn>
        <label className="zh-checkbox-label cat-te-toolbar__toggle">
          <input
            type="checkbox"
            checked={showInactive}
            onChange={(e) => setShowInactive(e.target.checked)}
          />
          <span>{t("catalog.tree.showInactive", "Mostrar inactivos")}</span>
        </label>
        <span className="cat-te-toolbar__count">
          {t("catalog.tree.nodesCount", { count: nodes.length })}
        </span>
      </div>

      {loading ? (
        <LoadingState />
      ) : roots.length === 0 ? (
        <EmptyState
          message={t(
            "catalog.tree.empty",
            "No hay nodos de catálogo. Cree la primera familia.",
          )}
        />
      ) : (
        <div className="cat-te-tree">
          {roots.map((node) => renderNode(node, 0))}
        </div>
      )}

      <ZHModal
        open={modal !== null}
        onClose={() => setModal(null)}
        title={
          modal
            ? modal.action === "add"
              ? t("catalog.tree.modal.newNodeIn", {
                  parent: (modal as { parentName: string }).parentName,
                })
              : t("catalog.tree.modal.editNode", { name: modal.node.name })
            : ""
        }
        subtitle={
          modal
            ? modal.action === "add"
              ? t("catalog.tree.modal.level", {
                  level: t(
                    `catalog.tree.level.${(modal as { level: string }).level}`,
                    (modal as { level: string }).level,
                  ),
                })
              : t("catalog.tree.modal.level", {
                  level: t(
                    `catalog.tree.level.${modal.node.level}`,
                    modal.node.level,
                  ),
                })
            : ""
        }
        footer={
          <ZHFormActions
            onCancel={() => setModal(null)}
            onSave={() => void handleSave()}
            hideDraft
            disableSave={saving}
            labels={{
              cancel: t("common.cancel", "Cancelar"),
              save: saving
                ? t("common.saving", "Guardando...")
                : modal?.action === "edit"
                  ? t("common.saveChanges", "Guardar cambios")
                  : t("catalog.tree.createNode", "Crear nodo"),
            }}
          />
        }
      >
        {saveError && (
          <ZHPageNotice
            variant="error"
            message={t("common.error", "Error")}
            detail={saveError}
          />
        )}
        <div className="pg-section-body">
          <ZHGrid cols={2}>
            <ZHField
              label={t("catalog.form.codeRequired", "Código *")}
              required
              error={form.formState.errors.code?.message}
            >
              <ZhTextInput
                className="zh-input mono zh-input--upper"
                placeholder={t("catalog.tree.codePlaceholder", "CÓDIGO")}
                autoFocus
                disabled={saving || modal?.action === "edit"}
                {...form.register("code")}
              />
            </ZHField>
            <ZHField
              label={t("catalog.form.nameRequired", "Nombre *")}
              required
              error={form.formState.errors.name?.message}
            >
              <ZhTextInput
                className="zh-input"
                placeholder={t(
                  "catalog.tree.namePlaceholder",
                  "Nombre del nodo",
                )}
                disabled={saving}
                {...form.register("name")}
              />
            </ZHField>
          </ZHGrid>
          <ZHGrid cols={2}>
            <ZHField
              label={t("catalog.tree.description", "Descripción")}
              error={form.formState.errors.description?.message}
            >
              <ZhTextInput
                className="zh-input"
                placeholder={t(
                  "catalog.tree.descriptionPlaceholder",
                  "Descripción opcional",
                )}
                disabled={saving}
                {...form.register("description")}
              />
            </ZHField>
            <ZHField
              label={t("catalog.col.sortOrder", "Orden")}
              error={form.formState.errors.sortOrder?.message}
            >
              <ZhNumberInput
                className="zh-input"
                positiveOnly
                disabled={saving}
                {...form.register("sortOrder", { valueAsNumber: true })}
              />
            </ZHField>
          </ZHGrid>
        </div>
      </ZHModal>
    </ErpPageTemplate>
  );
}
