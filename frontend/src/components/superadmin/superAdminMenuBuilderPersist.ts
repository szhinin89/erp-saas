import type { MenuPreviewLayout } from '../menu-builder/MenuPreview';
import { serializeEditorTreeToMenuJson, type EditorMenuItem } from '../menu-builder/menuBuilderTypes';

export type BuildPersistableMenuJsonParams = {
  crmWorkspace: boolean;
  json: string;
  planId: string;
  planActiveById: Record<string, string[]>;
  previewLayout: MenuPreviewLayout;
  visualLabelKey: string;
  visualTree: EditorMenuItem[];
};

export function buildPersistableMenuJson(params: BuildPersistableMenuJsonParams): string {
  const { crmWorkspace, json, planId, planActiveById, previewLayout, visualLabelKey, visualTree } = params;
  if (!crmWorkspace) return json.trim();
  if (!planId) return '';

  const activeSet = new Set(planActiveById[planId] ?? []);
  const filterTreeByActive = (nodes: EditorMenuItem[]): EditorMenuItem[] => {
    const out: EditorMenuItem[] = [];
    for (const node of nodes) {
      const children = filterTreeByActive(node.children);
      const route = (node.ruta ?? '').trim();
      const perm = (node.permiso ?? '').trim();
      const hasRoute = route.length > 0;
      const hasPerm = perm.length > 0;
      const isStrictFolder = !hasRoute && !hasPerm;
      const isStrictLeaf = hasRoute && hasPerm;
      const isChecked = activeSet.has(node.uid);

      if (isStrictFolder) {
        // Keep active folders even when empty so admins can persist
        // in-progress menu scaffolding (folder/subfolder structure).
        if (children.length > 0 || isChecked) out.push({ ...node, children });
        continue;
      }

      if (isStrictLeaf) {
        if (!isChecked) continue;
        out.push({ ...node, ruta: route, permiso: perm, children: [] });
        continue;
      }

      // Inconsistent node (only route or only permission): normalize safely.
      // If it still has visible descendants, keep it as folder; otherwise skip it.
      if (children.length > 0) {
        out.push({ ...node, ruta: '', permiso: '', children });
      }
    }
    return out;
  };

  const filteredTree = filterTreeByActive(visualTree);
  return serializeEditorTreeToMenuJson(filteredTree, visualLabelKey, previewLayout).trim();
}
