import { Fragment, useEffect, useMemo, useState } from 'react';
import { useDroppable } from '@dnd-kit/core';
import { SortableContext, useSortable, verticalListSortingStrategy } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { isEditorFolder, type EditorMenuItem } from './menuBuilderTypes';
import { ROOT_PARENT, type ParentRef, gapId, sortableTreeId } from './treeOps';
import { Modal } from '../Modal';
import { ZHBtn, ZHField } from '../zh/ZHForm';
import { ZHConfirmModal } from '../zh/ZHConfirmModal';
import { ZHActionsRow } from '../zh/ZHLayout';

function GapZone({ zoneId }: { zoneId: string }) {
  const { setNodeRef, isOver } = useDroppable({ id: zoneId });
  return <div ref={setNodeRef} className={`menu-builder-gap ${isOver ? 'is-over' : ''}`} aria-hidden />;
}

type RowProps = {
  node: EditorMenuItem;
  parentUid: ParentRef;
  depth: number;
  crmLayout?: boolean;
  activeIds?: Set<string>;
  onToggleActive?: (uid: string, checked: boolean) => void;
  expandedIds?: Set<string>;
  onToggleExpand?: (uid: string) => void;
  forceExpandedIds?: Set<string>;
  searchNeedle?: string;
  onPatch: (uid: string, patch: Partial<Pick<EditorMenuItem, 'nombre' | 'ruta' | 'permiso' | 'icono'>>) => void;
  onIndent: (uid: string) => void;
  onOutdent: (uid: string) => void;
  onRemove: (uid: string) => void;
  onAddChildFolder: (parentUid: string) => void;
  onAddChildForm?: (parentUid: string) => void;
  onMoveUp?: (uid: string) => void;
  onMoveDown?: (uid: string) => void;
};

export function SortableTreeRow({
  node,
  parentUid,
  depth,
  crmLayout,
  activeIds,
  onToggleActive,
  expandedIds,
  onToggleExpand,
  forceExpandedIds,
  searchNeedle,
  onPatch,
  onIndent,
  onOutdent,
  onRemove,
  onAddChildFolder,
  onAddChildForm,
  onMoveUp,
  onMoveDown,
}: RowProps) {
  type EditDraft = {
    nombre: string;
    icono: string;
    ruta: string;
    permiso: string;
  };
  const folder = isEditorFolder(node);
  const [editOpen, setEditOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [editDraft, setEditDraft] = useState<EditDraft>({
    nombre: node.nombre,
    icono: node.icono ?? '',
    ruta: node.ruta ?? '',
    permiso: node.permiso ?? '',
  });

  useEffect(() => {
    if (!editOpen) {
      setEditDraft({
        nombre: node.nombre,
        icono: node.icono ?? '',
        ruta: node.ruta ?? '',
        permiso: node.permiso ?? '',
      });
    }
  }, [editOpen, node.icono, node.nombre, node.permiso, node.ruta]);

  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: sortableTreeId(node.uid),
    data: { parentUid, uid: node.uid },
  });
  const style = {
    transform: CSS.Translate.toString(transform),
    transition,
  };
  const routePreview = (node.ruta ?? '').trim();
  const routeBadge = routePreview.length > 26 ? `${routePreview.slice(0, 23)}...` : routePreview;
  const hasChildren = node.children.length > 0;
  const isExpanded = forceExpandedIds?.has(node.uid) ? true : (expandedIds?.has(node.uid) ?? true);
  const isActive = activeIds?.has(node.uid) ?? false;
  const descendants = (items: EditorMenuItem[]): number => {
    let count = 0;
    for (const it of items) {
      count += 1;
      count += descendants(it.children);
    }
    return count;
  };
  const childCount = useMemo(() => descendants(node.children), [node.children]);
  const renderName = () => {
    if (!searchNeedle) return node.nombre;
    const idx = node.nombre.toLowerCase().indexOf(searchNeedle.toLowerCase());
    if (idx < 0) return node.nombre;
    const start = node.nombre.slice(0, idx);
    const mid = node.nombre.slice(idx, idx + searchNeedle.length);
    const end = node.nombre.slice(idx + searchNeedle.length);
    return (
      <>
        {start}
        <mark>{mid}</mark>
        {end}
      </>
    );
  };

  return (
    <div ref={setNodeRef} style={style} className={`menu-builder-node-block ${isDragging ? 'is-dragging' : ''}`}>
      <div className={`menu-builder-row${crmLayout ? ' menu-builder-row--crm' : ''}`}>
        <button
          type="button"
          className="menu-builder-handle"
          {...attributes}
          {...listeners}
          aria-label="Arrastrar para reordenar el ítem"
        >
          ⠿
        </button>
        {crmLayout && hasChildren ? (
          <button type="button" className="menu-builder-crm-action" onClick={() => onToggleExpand?.(node.uid)} title={isExpanded ? 'Colapsar' : 'Expandir'}>
            {isExpanded ? '▾' : '▸'}
          </button>
        ) : null}
        {crmLayout ? (
          <label className="menu-builder-crm-check" title={isActive ? 'Activo en plan' : 'Inactivo en plan'}>
            <input
              type="checkbox"
              checked={isActive}
              onChange={(e) => onToggleActive?.(node.uid, e.target.checked)}
              aria-label="Activar o desactivar nodo para el plan actual"
            />
          </label>
        ) : null}
        {!crmLayout ? (
          <input
            className="zh-input menu-builder-row__icon"
            value={node.icono ?? ''}
            placeholder="fa-…"
            title="Icono"
            onChange={(e) => onPatch(node.uid, { icono: e.target.value })}
            aria-label="Icono del ítem"
          />
        ) : (
          <span className="menu-builder-crm-icon" aria-hidden title="Icono">
            {(node.icono ?? '📄').trim().slice(0, 2) || '📄'}
          </span>
        )}
        {!crmLayout ? (
          <input
            className="zh-input menu-builder-row__name"
            value={node.nombre}
            onChange={(e) => onPatch(node.uid, { nombre: e.target.value })}
            aria-label={folder ? 'Nombre de la carpeta' : 'Nombre del formulario'}
          />
        ) : (
          <div className="menu-builder-crm-main">
            <span className="menu-builder-crm-name">{renderName()}</span>
            <span className={`menu-builder-crm-type ${folder ? 'is-folder' : 'is-leaf'}`}>{folder ? 'carpeta' : 'formulario'}</span>
            {!folder && routeBadge ? (
              <span className="menu-builder-crm-route" title={routePreview}>
                {routeBadge}
              </span>
            ) : null}
          </div>
        )}
        {!crmLayout ? (
          <>
            <input
              className="zh-input menu-builder-row__route"
              value={folder ? '' : node.ruta}
              title="Ruta (solo hoja)"
              readOnly={folder}
              disabled={folder}
              placeholder={folder ? '—' : ''}
              onChange={(e) => onPatch(node.uid, { ruta: e.target.value })}
              aria-label="Ruta"
            />
            <input
              className="zh-input menu-builder-row__perm"
              value={folder ? '' : node.permiso}
              title="Permiso (solo hoja)"
              readOnly={folder}
              disabled={folder}
              placeholder={folder ? '—' : ''}
              onChange={(e) => onPatch(node.uid, { permiso: e.target.value })}
              aria-label="Clave de permiso"
            />
          </>
        ) : folder ? null : (
          <>
            <input
              className="zh-input menu-builder-row__perm visually-hidden-crm"
              value={node.permiso}
              title="Permiso"
              onChange={(e) => onPatch(node.uid, { permiso: e.target.value })}
              aria-label="Permiso (avanzado)"
            />
          </>
        )}
        <div className="menu-builder-row__actions">
          {crmLayout ? (
            <>
              <button
                type="button"
                className="menu-builder-crm-action"
                onClick={() => onMoveUp?.(node.uid)}
                aria-label="Subir una posición en la lista"
                title="Subir"
              >
                ↑
              </button>
              <button
                type="button"
                className="menu-builder-crm-action"
                onClick={() => onMoveDown?.(node.uid)}
                aria-label="Bajar una posición en la lista"
                title="Bajar"
              >
                ↓
              </button>
            </>
          ) : null}
          {folder ? (
            <button
              type="button"
              className={crmLayout ? 'menu-builder-crm-action' : 'zh-btn zh-btn--ghost zh-btn--xs'}
              onClick={() => onAddChildFolder(node.uid)}
              aria-label="Agregar carpeta hija"
              title="Agregar carpeta hija"
            >
              {crmLayout ? '📁+' : '+📁'}
            </button>
          ) : null}
          {folder && crmLayout && onAddChildForm ? (
            <button
              type="button"
              className="menu-builder-crm-action"
              onClick={() => onAddChildForm(node.uid)}
              aria-label="Agregar formulario hijo"
              title="Agregar formulario hijo"
            >
              📄+
            </button>
          ) : null}
          <button
            type="button"
            className={crmLayout ? 'menu-builder-crm-action' : 'zh-btn zh-btn--ghost zh-btn--xs'}
            onClick={() => onIndent(node.uid)}
            aria-label="Anidar un nivel"
            title="Anidar"
          >
            →
          </button>
          <button
            type="button"
            className={crmLayout ? 'menu-builder-crm-action' : 'zh-btn zh-btn--ghost zh-btn--xs'}
            onClick={() => onOutdent(node.uid)}
            aria-label="Desanidar un nivel"
            title="Subir nivel"
          >
            ←
          </button>
          {crmLayout ? (
            <button
              type="button"
              className="menu-builder-crm-action"
              onClick={() => setEditOpen(true)}
              aria-label="Editar nombre del ítem"
              title="Editar"
            >
              ✎
            </button>
          ) : null}
          <button
            type="button"
            className={crmLayout ? 'menu-builder-crm-action menu-builder-crm-action--danger' : 'zh-btn zh-btn--ghost zh-btn--xs'}
            onClick={() => {
              if (!crmLayout) {
                onRemove(node.uid);
                return;
              }
              setDeleteOpen(true);
            }}
            aria-label="Eliminar ítem"
            title="Eliminar"
          >
            ✕
          </button>
        </div>
      </div>
      {(folder || node.children.length > 0) && isExpanded ? (
        <SortableTreeBranch
          parentUid={node.uid}
          nodes={node.children}
          depth={depth + 1}
          crmLayout={crmLayout}
          activeIds={activeIds}
          onToggleActive={onToggleActive}
          expandedIds={expandedIds}
          onToggleExpand={onToggleExpand}
          forceExpandedIds={forceExpandedIds}
          onPatch={onPatch}
          onIndent={onIndent}
          onOutdent={onOutdent}
          onRemove={onRemove}
          onAddChildFolder={onAddChildFolder}
          onAddChildForm={onAddChildForm}
          onMoveUp={onMoveUp}
          onMoveDown={onMoveDown}
        />
      ) : null}
      {editOpen ? (
        <Modal title={`Editar ${folder ? 'carpeta' : 'formulario'}`} onClose={() => setEditOpen(false)} size="sm">
          <ZHField label="Nombre">
            <input
              className="zh-input"
              value={editDraft.nombre}
              onChange={(e) => setEditDraft((prev) => ({ ...prev, nombre: e.target.value }))}
            />
          </ZHField>
          <ZHField label="Ícono">
            <input
              className="zh-input"
              value={editDraft.icono}
              onChange={(e) => setEditDraft((prev) => ({ ...prev, icono: e.target.value }))}
              placeholder="Ej. fa-folder"
            />
          </ZHField>
          {!folder ? (
            <>
              <ZHField label="Ruta">
                <input
                  className="zh-input"
                  value={editDraft.ruta}
                  onChange={(e) => setEditDraft((prev) => ({ ...prev, ruta: e.target.value }))}
                  placeholder="/modulo/recurso"
                />
              </ZHField>
              <ZHField label="Permiso">
                <input
                  className="zh-input"
                  value={editDraft.permiso}
                  onChange={(e) => setEditDraft((prev) => ({ ...prev, permiso: e.target.value }))}
                  placeholder="modulo.recurso.accion"
                />
              </ZHField>
            </>
          ) : null}
          <ZHActionsRow className="zh-mt-16">
            <ZHBtn variant="ghost" size="md" type="button" onClick={() => setEditOpen(false)}>
              Cancelar
            </ZHBtn>
            <ZHBtn
              variant="primary"
              size="md"
              type="button"
              onClick={() => {
                const nextName = editDraft.nombre.trim();
                if (!nextName) return;
                onPatch(node.uid, {
                  nombre: nextName,
                  icono: editDraft.icono.trim(),
                  ...(folder
                    ? {}
                    : {
                        ruta: editDraft.ruta.trim(),
                        permiso: editDraft.permiso.trim(),
                      }),
                });
                setEditOpen(false);
              }}
              disabled={!editDraft.nombre.trim()}
            >
              Guardar
            </ZHBtn>
          </ZHActionsRow>
        </Modal>
      ) : null}
      {deleteOpen ? (
        <ZHConfirmModal
          title={`Eliminar ${folder ? 'carpeta' : 'formulario'}`}
          message={
            <>
              ¿Eliminar <strong>{node.nombre}</strong>?
              {childCount > 0 ? <><br />Se eliminarán {childCount} hijo(s).</> : null}
            </>
          }
          confirmLabel="Eliminar"
          cancelLabel="Cancelar"
          variant="destructive"
          onCancel={() => setDeleteOpen(false)}
          onConfirm={() => {
            onRemove(node.uid);
            setDeleteOpen(false);
          }}
        />
      ) : null}
    </div>
  );
}

type BranchProps = {
  parentUid: ParentRef;
  nodes: EditorMenuItem[];
  depth: number;
  crmLayout?: boolean;
  activeIds?: Set<string>;
  onToggleActive?: (uid: string, checked: boolean) => void;
  expandedIds?: Set<string>;
  onToggleExpand?: (uid: string) => void;
  forceExpandedIds?: Set<string>;
  searchNeedle?: string;
  onPatch: (uid: string, patch: Partial<Pick<EditorMenuItem, 'nombre' | 'ruta' | 'permiso' | 'icono'>>) => void;
  onIndent: (uid: string) => void;
  onOutdent: (uid: string) => void;
  onRemove: (uid: string) => void;
  onAddChildFolder: (parentUid: string) => void;
  onAddChildForm?: (parentUid: string) => void;
  onMoveUp?: (uid: string) => void;
  onMoveDown?: (uid: string) => void;
};

export function SortableTreeBranch({
  parentUid,
  nodes,
  depth,
  crmLayout,
  activeIds,
  onToggleActive,
  expandedIds,
  onToggleExpand,
  forceExpandedIds,
  searchNeedle,
  onPatch,
  onIndent,
  onOutdent,
  onRemove,
  onAddChildFolder,
  onAddChildForm,
  onMoveUp,
  onMoveDown,
}: BranchProps) {
  const ids = nodes.map((n) => sortableTreeId(n.uid));
  const isRootEmpty = parentUid === ROOT_PARENT && nodes.length === 0;
  return (
    <SortableContext id={`sc-${parentUid}`} items={ids} strategy={verticalListSortingStrategy}>
      <div
        className={`menu-builder-branch ${depth > 0 ? 'menu-builder-branch--nested' : ''} ${isRootEmpty ? 'menu-builder-branch--root-empty' : ''}`}
      >
        <GapZone zoneId={gapId(parentUid, 0)} />
        {nodes.map((node, i) => (
          <Fragment key={node.uid}>
            <SortableTreeRow
              node={node}
              parentUid={parentUid}
              depth={depth}
              crmLayout={crmLayout}
              activeIds={activeIds}
              onToggleActive={onToggleActive}
              expandedIds={expandedIds}
              onToggleExpand={onToggleExpand}
              forceExpandedIds={forceExpandedIds}
              searchNeedle={searchNeedle}
              onPatch={onPatch}
              onIndent={onIndent}
              onOutdent={onOutdent}
              onRemove={onRemove}
              onAddChildFolder={onAddChildFolder}
              onAddChildForm={onAddChildForm}
              onMoveUp={onMoveUp}
              onMoveDown={onMoveDown}
            />
            <GapZone zoneId={gapId(parentUid, i + 1)} />
          </Fragment>
        ))}
        {nodes.length === 0 ? <GapZone zoneId={gapId(parentUid, 1)} /> : null}
      </div>
    </SortableContext>
  );
}
