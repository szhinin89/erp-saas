import { arrayMove } from '@dnd-kit/sortable';
import { isEditorFolder, type EditorMenuItem } from './menuBuilderTypes';

/** Raíz del árbol en operaciones de inserción / gaps. */
export const ROOT_PARENT = 'ROOT' as const;
export type ParentRef = typeof ROOT_PARENT | (string & {});

export function gapId(parent: ParentRef, index: number): string {
  return `gap|${parent}|${index}`;
}

export function parseGapId(id: string): { parent: ParentRef; index: number } | null {
  if (!id.startsWith('gap|')) return null;
  const parts = id.split('|');
  if (parts.length < 3) return null;
  const parent = parts[1] as ParentRef;
  const index = Number(parts[parts.length - 1]);
  if (!Number.isFinite(index) || index < 0) return null;
  return { parent, index };
}

export function sortableTreeId(uid: string): string {
  return `t|${uid}`;
}

export function parseSortableTreeId(id: string): string | null {
  if (!id.startsWith('t|')) return null;
  return id.slice(2) || null;
}

export function libDragId(funcId: string): string {
  return `lib|${funcId}`;
}

export function parseLibDragId(id: string): string | null {
  if (!id.startsWith('lib|')) return null;
  return id.slice(4) || null;
}

export function findLocation(
  nodes: EditorMenuItem[],
  uid: string,
  parent: ParentRef = ROOT_PARENT,
): { parent: ParentRef; index: number } | null {
  for (let i = 0; i < nodes.length; i++) {
    if (nodes[i].uid === uid) return { parent, index: i };
    const inner = findLocation(nodes[i].children, uid, nodes[i].uid);
    if (inner) return inner;
  }
  return null;
}

export function findNodeByUid(nodes: EditorMenuItem[], uid: string): EditorMenuItem | null {
  for (const n of nodes) {
    if (n.uid === uid) return n;
    const inner = findNodeByUid(n.children, uid);
    if (inner) return inner;
  }
  return null;
}

export function removeByUid(
  nodes: EditorMenuItem[],
  uid: string,
): { next: EditorMenuItem[]; removed: EditorMenuItem | null } {
  const idx = nodes.findIndex((n) => n.uid === uid);
  if (idx >= 0) {
    const removed = nodes[idx];
    const next = [...nodes.slice(0, idx), ...nodes.slice(idx + 1)];
    return { next, removed };
  }
  for (let i = 0; i < nodes.length; i++) {
    const ch = nodes[i].children;
    const r = removeByUid(ch, uid);
    if (r.removed) {
      const next = nodes.map((n, j) => (j === i ? { ...n, children: r.next } : n));
      return { next, removed: r.removed };
    }
  }
  return { next: nodes, removed: null };
}

export function insertChildAt(
  nodes: EditorMenuItem[],
  parentUid: ParentRef,
  index: number,
  newNode: EditorMenuItem,
): EditorMenuItem[] {
  if (parentUid !== ROOT_PARENT) {
    const parentNode = findNodeByUid(nodes, parentUid as string);
    if (parentNode && !isEditorFolder(parentNode)) return nodes;
  }
  if (parentUid === ROOT_PARENT) {
    const next = [...nodes];
    const at = Math.max(0, Math.min(index, next.length));
    next.splice(at, 0, newNode);
    return next;
  }
  return nodes.map((n) => {
    if (n.uid === parentUid) {
      const ch = [...n.children];
      const at = Math.max(0, Math.min(index, ch.length));
      ch.splice(at, 0, newNode);
      return { ...n, children: ch };
    }
    if (n.children.length === 0) return n;
    return { ...n, children: insertChildAt(n.children, parentUid, index, newNode) };
  });
}

/** Quita el nodo y lo inserta en la lista del padre indicado en el índice. */
export function moveNodeToGap(
  root: EditorMenuItem[],
  nodeUid: string,
  parentUid: ParentRef,
  index: number,
): EditorMenuItem[] {
  const loc = findLocation(root, nodeUid);
  const { next: without, removed } = removeByUid(root, nodeUid);
  if (!removed || !loc) return root;
  let idx = index;
  const sameList =
    (loc.parent === ROOT_PARENT && parentUid === ROOT_PARENT) ||
    (loc.parent !== ROOT_PARENT && loc.parent === parentUid);
  if (sameList && loc.index < index) idx -= 1;
  return insertChildAt(without, parentUid, idx, removed);
}

export function reorderSibling(
  nodes: EditorMenuItem[],
  parentUid: ParentRef,
  activeUid: string,
  overUid: string,
): EditorMenuItem[] {
  if (parentUid === ROOT_PARENT) {
    const ids = nodes.map((n) => n.uid);
    const oi = ids.indexOf(activeUid);
    const ni = ids.indexOf(overUid);
    if (oi < 0 || ni < 0) return nodes;
    return arrayMove(nodes, oi, ni);
  }
  return nodes.map((n) => {
    if (n.uid === parentUid) {
      const ch = n.children;
      const ids = ch.map((c) => c.uid);
      const oi = ids.indexOf(activeUid);
      const ni = ids.indexOf(overUid);
      if (oi < 0 || ni < 0) return n;
      return { ...n, children: arrayMove(ch, oi, ni) };
    }
    if (n.children.length === 0) return n;
    return { ...n, children: reorderSibling(n.children, parentUid, activeUid, overUid) };
  });
}

/** Inserta el nodo quitado antes del ítem objetivo (misma lista padre que el objetivo). */
export function moveNodeBeforeSibling(root: EditorMenuItem[], activeUid: string, targetUid: string): EditorMenuItem[] {
  if (activeUid === targetUid) return root;
  const locT = findLocation(root, targetUid);
  const locA = findLocation(root, activeUid);
  if (!locT || !locA) return root;
  const parentKey = locT.parent;
  const { next: w, removed } = removeByUid(root, activeUid);
  if (!removed) return root;
  let idx = locT.index;
  const same =
    (locA.parent === ROOT_PARENT && locT.parent === ROOT_PARENT) ||
    (locA.parent !== ROOT_PARENT && locA.parent === locT.parent);
  if (same && locA.index < locT.index) idx -= 1;
  return insertChildAt(w, parentKey, idx, removed);
}

export function deleteNode(root: EditorMenuItem[], uid: string): EditorMenuItem[] {
  const { next } = removeByUid(root, uid);
  return next;
}

export function updateNodeFields(
  nodes: EditorMenuItem[],
  uid: string,
  patch: Partial<Pick<EditorMenuItem, 'nombre' | 'ruta' | 'permiso' | 'icono'>>,
): EditorMenuItem[] {
  return nodes.map((n) => {
    if (n.uid === uid) return { ...n, ...patch };
    if (n.children.length === 0) return n;
    return { ...n, children: updateNodeFields(n.children, uid, patch) };
  });
}

/** Convierte el nodo en hijo del hermano anterior (indent). */
export function indentNode(root: EditorMenuItem[], uid: string): EditorMenuItem[] {
  const loc = findLocation(root, uid);
  if (!loc || loc.index === 0) return root;
  const { next: w, removed } = removeByUid(root, uid);
  if (!removed) return root;
  const parentKey = loc.parent;
  if (parentKey === ROOT_PARENT) {
    const prev = w[loc.index - 1];
    if (!isEditorFolder(prev)) return root;
    const ch = [...prev.children, removed];
    return w.map((n, i) => (i === loc.index - 1 ? { ...n, children: ch } : n));
  }
  return insertUnderParent(w, parentKey, loc.index - 1, removed);
}

function insertUnderParent(
  nodes: EditorMenuItem[],
  parentUid: string,
  prevSiblingIndex: number,
  node: EditorMenuItem,
): EditorMenuItem[] {
  return nodes.map((n) => {
    if (n.uid === parentUid) {
      const ch = [...n.children];
      const prev = ch[prevSiblingIndex];
      if (!isEditorFolder(prev)) return n;
      const nextCh = [...ch];
      nextCh[prevSiblingIndex] = { ...prev, children: [...prev.children, node] };
      return { ...n, children: nextCh };
    }
    if (n.children.length === 0) return n;
    return { ...n, children: insertUnderParent(n.children, parentUid, prevSiblingIndex, node) };
  });
}

/** Sube el nodo al padre (outdent): queda después del padre en la lista del abuelo. */
export function outdentNode(root: EditorMenuItem[], uid: string): EditorMenuItem[] {
  const loc = findLocation(root, uid);
  if (!loc || loc.parent === ROOT_PARENT) return root;
  const parentLoc = findParentOf(root, loc.parent);
  if (!parentLoc) return root;
  const { next: w, removed } = removeByUid(root, uid);
  if (!removed) return root;
  const insertParent = parentLoc.parent;
  const insertIndex = parentLoc.index + 1;
  return insertChildAt(w, insertParent, insertIndex, removed);
}

function findParentOf(
  nodes: EditorMenuItem[],
  childParentUid: string,
  grandParent: ParentRef = ROOT_PARENT,
): { parent: ParentRef; index: number } | null {
  for (let i = 0; i < nodes.length; i++) {
    if (nodes[i].uid === childParentUid) return { parent: grandParent, index: i };
    const inner = findParentOf(nodes[i].children, childParentUid, nodes[i].uid);
    if (inner) return inner;
  }
  return null;
}
