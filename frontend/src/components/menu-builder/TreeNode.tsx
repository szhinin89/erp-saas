import { Fragment } from 'react';
import { useDroppable } from '@dnd-kit/core';
import { SortableContext, useSortable, verticalListSortingStrategy } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { isEditorFolder, type EditorMenuItem } from './menuBuilderTypes';
import { ROOT_PARENT, type ParentRef, gapId, sortableTreeId } from './treeOps';

function GapZone({ zoneId }: { zoneId: string }) {
  const { setNodeRef, isOver } = useDroppable({ id: zoneId });
  return <div ref={setNodeRef} className={`menu-builder-gap ${isOver ? 'is-over' : ''}`} aria-hidden />;
}

type RowProps = {
  node: EditorMenuItem;
  parentUid: ParentRef;
  depth: number;
  onPatch: (uid: string, patch: Partial<Pick<EditorMenuItem, 'nombre' | 'ruta' | 'permiso' | 'icono'>>) => void;
  onIndent: (uid: string) => void;
  onOutdent: (uid: string) => void;
  onRemove: (uid: string) => void;
  onAddChildFolder: (parentUid: string) => void;
};

export function SortableTreeRow({
  node,
  parentUid,
  depth,
  onPatch,
  onIndent,
  onOutdent,
  onRemove,
  onAddChildFolder,
}: RowProps) {
  const folder = isEditorFolder(node);
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: sortableTreeId(node.uid),
    data: { parentUid, uid: node.uid },
  });
  const style = {
    transform: CSS.Translate.toString(transform),
    transition,
  };

  return (
    <div ref={setNodeRef} style={style} className={`menu-builder-node-block ${isDragging ? 'is-dragging' : ''}`}>
      <div className="menu-builder-row">
        <button
          type="button"
          className="menu-builder-handle"
          {...attributes}
          {...listeners}
          aria-label="Drag handle"
        >
          ⠿
        </button>
        <input
          className="zh-input menu-builder-row__name"
          value={node.nombre}
          onChange={(e) => onPatch(node.uid, { nombre: e.target.value })}
        />
        <input
          className="zh-input menu-builder-row__icon"
          value={node.icono ?? ''}
          placeholder="fa-…"
          title="Icono"
          onChange={(e) => onPatch(node.uid, { icono: e.target.value })}
        />
        <input
          className="zh-input menu-builder-row__route"
          value={folder ? '' : node.ruta}
          title="Ruta (solo hoja)"
          readOnly={folder}
          disabled={folder}
          placeholder={folder ? '—' : ''}
          onChange={(e) => onPatch(node.uid, { ruta: e.target.value })}
        />
        <input
          className="zh-input menu-builder-row__perm"
          value={folder ? '' : node.permiso}
          title="Permiso (solo hoja)"
          readOnly={folder}
          disabled={folder}
          placeholder={folder ? '—' : ''}
          onChange={(e) => onPatch(node.uid, { permiso: e.target.value })}
        />
        <div className="menu-builder-row__actions">
          {folder ? (
            <button
              type="button"
              className="zh-btn zh-btn--ghost zh-btn--xs"
              onClick={() => onAddChildFolder(node.uid)}
              title="Agregar carpeta hija"
            >
              +📁
            </button>
          ) : null}
          <button type="button" className="zh-btn zh-btn--ghost zh-btn--xs" onClick={() => onIndent(node.uid)} title="Anidar">
            →
          </button>
          <button type="button" className="zh-btn zh-btn--ghost zh-btn--xs" onClick={() => onOutdent(node.uid)} title="Subir nivel">
            ←
          </button>
          <button type="button" className="zh-btn zh-btn--ghost zh-btn--xs" onClick={() => onRemove(node.uid)} title="Eliminar">
            ✕
          </button>
        </div>
      </div>
      {folder || node.children.length > 0 ? (
        <SortableTreeBranch
          parentUid={node.uid}
          nodes={node.children}
          depth={depth + 1}
          onPatch={onPatch}
          onIndent={onIndent}
          onOutdent={onOutdent}
          onRemove={onRemove}
          onAddChildFolder={onAddChildFolder}
        />
      ) : null}
    </div>
  );
}

type BranchProps = {
  parentUid: ParentRef;
  nodes: EditorMenuItem[];
  depth: number;
  onPatch: (uid: string, patch: Partial<Pick<EditorMenuItem, 'nombre' | 'ruta' | 'permiso' | 'icono'>>) => void;
  onIndent: (uid: string) => void;
  onOutdent: (uid: string) => void;
  onRemove: (uid: string) => void;
  onAddChildFolder: (parentUid: string) => void;
};

export function SortableTreeBranch({
  parentUid,
  nodes,
  depth,
  onPatch,
  onIndent,
  onOutdent,
  onRemove,
  onAddChildFolder,
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
              onPatch={onPatch}
              onIndent={onIndent}
              onOutdent={onOutdent}
              onRemove={onRemove}
              onAddChildFolder={onAddChildFolder}
            />
            <GapZone zoneId={gapId(parentUid, i + 1)} />
          </Fragment>
        ))}
        {nodes.length === 0 ? <GapZone zoneId={gapId(parentUid, 1)} /> : null}
      </div>
    </SortableContext>
  );
}
