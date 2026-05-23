import { useDraggable } from '@dnd-kit/core';
import { CSS } from '@dnd-kit/utilities';
import type { FuncionalidadArbolDto } from '../../modules/platform/api/platformService';
import { libDragId } from './treeOps';

type Props = {
  node: FuncionalidadArbolDto;
  dense?: boolean;
  onPreview?: (node: FuncionalidadArbolDto) => void;
};

export function MenuBuilderLibraryRow({ node, dense, onPreview }: Props) {
  const { attributes, listeners, setNodeRef, transform, isDragging } = useDraggable({
    id: libDragId(node.id),
  });
  const style = transform ? { transform: CSS.Translate.toString(transform) } : undefined;
  const iconChar = (node.icon ?? '').trim().slice(0, 2) || '◇';
  return (
    <div
      ref={setNodeRef}
      style={style}
      className={`menu-builder-lib-row ${isDragging ? 'is-dragging' : ''}${dense ? ' menu-builder-lib-row--crm' : ''}`}
      {...listeners}
      {...attributes}
    >
      <span className="menu-builder-lib-icon" aria-hidden>
        {iconChar}
      </span>
      <div className="menu-builder-lib-text">
        <div className="menu-builder-lib-name">{node.name}</div>
        <div className="menu-builder-lib-perm" title={node.permission}>
          {node.path?.trim() ? `${node.path} · ` : ''}
          {node.permission}
        </div>
      </div>
      {dense ? (
        <div className="menu-builder-lib-previewRow">
          <button
            type="button"
            className="zh-btn zh-btn--ghost zh-btn--xs"
            onPointerDown={(e) => e.stopPropagation()}
            onClick={(e) => {
              e.stopPropagation();
              onPreview?.(node);
            }}
            aria-label="Previsualizar formulario del catálogo"
          >
            👁 Previsualizar
          </button>
        </div>
      ) : null}
    </div>
  );
}
