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
import { Badge } from "../../../../components/PageShell";
import { ZHModal } from "../../../../components/zh/ZHModal";
import { applyServerErrors } from "../../../lib/validationErrors";
import { readApiErrorMessage } from "../../../lib/apiError";
import {
  categoryNodeService,
  type CategoryNodeDto,
} from "../api/categoryNodeService";
import "./catalog-wizard.css";

const nodeSchema = z.object({
  code: z
    .string()
    .trim()
    .min(1, "Codigo obligatorio.")
    .max(20)
    .regex(/^[A-Za-z0-9\-_]+$/, "Solo letras, numeros, guiones."),
  name: z.string().trim().min(1, "Nombre obligatorio.").max(120),
  description: z.string().max(500).nullable().optional(),
  level: z.string().min(1),
  sortOrder: z.number().min(0).default(0),
});
type NodeFormValues = z.infer<typeof nodeSchema>;

const LEVEL_ICONS: Record<string, { icon: string; className: string }> = {
  Family: { icon: "account_tree", className: "te-icon--family" },
  Category: { icon: "category", className: "te-icon--category" },
  Subcategory: { icon: "label", className: "te-icon--subcategory" },
  Custom: { icon: "extension", className: "te-icon--custom" },
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
  const [nodes, setNodes] = useState<CategoryNodeDto[]>([]);
  const [maxDepth, setMaxDepth] = useState(3);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const [modal, setModal] = useState<ModalTarget | null>(null);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState("");
  const [showInactive, setShowInactive] = useState(true);

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
      if (expanded.size === 0) {
        setExpanded(
          new Set(result.nodes.filter((n) => !n.parentId).map((n) => n.id)),
        );
      }
    } catch {
      setError("Error al cargar el catalogo.");
    } finally {
      setLoading(false);
    }
  }, []);

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
        setSaveError(readApiErrorMessage(err) ?? "Error al guardar.");
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
      setError("Error al cambiar el estado.");
    }
  };

  const filterNodes = (list: CategoryNodeDto[]) =>
    showInactive ? list : list.filter((n) => n.isActive);

  const renderNode = (node: CategoryNodeDto, depth: number) => {
    const children = filterNodes(childrenOf.get(node.id) ?? []);
    const hasChildren = children.length > 0;
    const levelInfo = LEVEL_ICONS[node.level] ?? LEVEL_ICONS.Custom;

    return (
      <div key={node.id} className="te-node">
        <div
          className={`te-row te-row--depth-${Math.min(depth, 6)} ${!node.isActive ? "te-row--inactive" : ""}`}
        >
          {hasChildren ? (
            <button
              type="button"
              className="te-expand"
              onClick={() => toggle(node.id)}
            >
              <span
                className={`material-symbols-outlined te-expand-icon ${expanded.has(node.id) ? "te-expand-icon--expanded" : ""}`}
              >
                chevron_right
              </span>
            </button>
          ) : (
            <span className="te-expand te-expand--leaf" />
          )}
          <span
            className={`material-symbols-outlined te-icon ${levelInfo.className}`}
          >
            {levelInfo.icon}
          </span>
          <Badge label={node.code} variant="neutral" className="mono" />
          <strong className="te-label">{node.name}</strong>
          <span
            className={`te-level-badge te-level-badge--${node.level.toLowerCase()}`}
          >
            {node.level}
          </span>
          <span
            className={
              node.isActive
                ? "zh-status zh-status--active"
                : "zh-status zh-status--inactive"
            }
          >
            {node.isActive ? "Activo" : "Inactivo"}
          </span>
          <div className="te-actions">
            <ZHBtn
              type="button"
              variant="ghost"
              size="sm"
              title="Editar"
              onClick={() => openEdit(node)}
            >
              <span className="material-symbols-outlined">edit</span>
            </ZHBtn>
            <ZHBtn
              type="button"
              variant="ghost"
              size="sm"
              title={node.isActive ? "Desactivar" : "Activar"}
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
                title="Agregar hijo"
                onClick={() =>
                  openAdd(node.id, node.name, getChildLevel(node.level))
                }
              >
                <span className="material-symbols-outlined">add</span>
              </ZHBtn>
            )}
            {node.isActive && node.depth >= maxDepth && (
              <Badge
                label="Máx. nivel"
                variant="neutral"
                title={`Profundidad máxima alcanzada (${maxDepth} niveles)`}
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
    <ErpPageTemplate kicker="Catalogo" title="Arbol de Catalogo">
      {error && <ZHPageNotice variant="error" message="Error" detail={error} />}

      <div className="te-toolbar">
        <ZHBtn
          variant="primary"
          size="sm"
          type="button"
          onClick={() => openAdd(null, "Raiz", "Family")}
        >
          <span className="material-symbols-outlined">add</span> Nueva Familia
        </ZHBtn>
        <ZHBtn
          variant="secondary"
          size="sm"
          type="button"
          onClick={() => void loadTree()}
          disabled={loading}
        >
          <span className="material-symbols-outlined">refresh</span> Actualizar
        </ZHBtn>
        <label className="zh-checkbox-label te-toolbar__toggle">
          <input
            type="checkbox"
            checked={showInactive}
            onChange={(e) => setShowInactive(e.target.checked)}
          />
          <span>Mostrar inactivos</span>
        </label>
        <span className="te-toolbar__count">{nodes.length} nodos</span>
      </div>

      {loading ? (
        <LoadingState />
      ) : roots.length === 0 ? (
        <EmptyState message="No hay nodos de catalogo. Crea la primera familia." />
      ) : (
        <div className="te-tree">
          {roots.map((node) => renderNode(node, 0))}
        </div>
      )}

      <ZHModal
        open={modal !== null}
        onClose={() => setModal(null)}
        title={
          modal
            ? modal.action === "add"
              ? `Nuevo nodo en ${(modal as { parentName: string }).parentName}`
              : `Editar ${modal.node.name}`
            : ""
        }
        subtitle={
          modal
            ? modal.action === "add"
              ? `Nivel: ${(modal as { level: string }).level}`
              : `Nivel: ${modal.node.level}`
            : ""
        }
        footer={
          <ZHFormActions
            onCancel={() => setModal(null)}
            onSave={() => void handleSave()}
            hideDraft
            disableSave={saving}
            labels={{
              cancel: "Cancelar",
              save: saving
                ? "Guardando..."
                : modal?.action === "edit"
                  ? "Guardar cambios"
                  : "Crear nodo",
            }}
          />
        }
      >
        {saveError && (
          <ZHPageNotice variant="error" message="Error" detail={saveError} />
        )}
        <div className="pg-section-body">
          <ZHGrid cols={2}>
            <ZHField
              label="Codigo *"
              required
              error={form.formState.errors.code?.message}
            >
              <input
                className="zh-input mono zh-input--upper"
                placeholder="CODIGO"
                autoFocus
                disabled={saving || modal?.action === "edit"}
                {...form.register("code")}
              />
            </ZHField>
            <ZHField
              label="Nombre *"
              required
              error={form.formState.errors.name?.message}
            >
              <input
                className="zh-input"
                placeholder="Nombre del nodo"
                disabled={saving}
                {...form.register("name")}
              />
            </ZHField>
          </ZHGrid>
          <ZHGrid cols={2}>
            <ZHField
              label="Descripcion"
              error={form.formState.errors.description?.message}
            >
              <input
                className="zh-input"
                placeholder="Descripcion opcional"
                disabled={saving}
                {...form.register("description")}
              />
            </ZHField>
            <ZHField
              label="Orden"
              error={form.formState.errors.sortOrder?.message}
            >
              <input
                type="number"
                className="zh-input"
                min={0}
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
