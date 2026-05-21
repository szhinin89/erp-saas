import type { ReactNode } from 'react';
import type { FuncionalidadArbolDto } from '../../modules/superadmin/api/superAdminService';
import type { MenuPreviewLayout } from './MenuPreview';
import type { EditorMenuItem, MenuItem } from './menuBuilderTypes';
import type { ParentRef } from './treeOps';

export type MenuBuilderViewMode = 'editor' | 'preview' | 'split';

export type MenuBuilderProps = {
  catalogArbol: FuncionalidadArbolDto[];
  tree: EditorMenuItem[];
  onTreeChange: (next: EditorMenuItem[]) => void;
  viewMode: MenuBuilderViewMode;
  onViewModeChange: (mode: MenuBuilderViewMode) => void;
  previewLayout: MenuPreviewLayout;
  onPreviewLayoutChange: (layout: MenuPreviewLayout) => void;
  /** Mensajes de validación DnD (p. ej. soltar en hoja). */
  onBuilderMessage?: (message: string) => void;
  /** Columnas: árbol | preview | biblioteca (vista configuración menú/plan). */
  workspaceVariant?: 'default' | 'crm';
  /** Oculta la barra de modos editor/preview (el padre fuerza split y radios de layout). */
  hideWorkspaceToolbar?: boolean;
  /** Títulos de paneles (vista CRM). */
  panelTitles?: { library?: string; canvas?: string; preview?: string };
  /** Controles extra encima del árbol (p. ej. deshacer/rehacer). */
  crmToolbar?: ReactNode;
  /** Bloque bajo el título del árbol (p. ej. pastillas de plan y búsqueda). */
  crmMasterStack?: ReactNode;
  /** Bloque bajo el título de biblioteca (filtros del catálogo). */
  crmLibraryStack?: ReactNode;
  /** Pie del panel árbol (acciones y ayuda). */
  crmMasterFooter?: ReactNode;
  /** Columna central: controles encima de la vista previa (toggle de layout). */
  crmPreviewExtras?: ReactNode;
  /** Columna central: contenido debajo de la vista previa (tarjeta de plan, simulación). */
  crmPreviewExtrasBottom?: ReactNode;
  /** Controls rendered inside the browser chrome bar of the preview simulation. */
  previewControls?: ReactNode;
  /** When true in CRM mode, hides the preview column so the caller can render MenuPreview externally. */
  hideCrmPreview?: boolean;
  /** Sustituye el menú simulado en la vista previa (p. ej. menú efectivo de otra empresa). */
  previewItemsOverride?: MenuItem[] | null;
  /** Activaciones por plan para la vista CRM (checkbox por nodo). */
  activeNodeIds?: Set<string>;
  /** Cambio de activación por nodo (uid del editor). */
  onToggleNodeActive?: (uid: string, checked: boolean) => void;
  treeSearchQuery?: string;
};

export type MenuBuilderPromptRequest = {
  kind: 'folder' | 'form';
  parentUid: ParentRef;
  title: string;
  label: string;
  defaultValue: string;
  placeholder?: string;
};
