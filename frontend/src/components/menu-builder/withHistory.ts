import { useCallback, useReducer } from 'react';
import type { EditorMenuItem } from './menuBuilderTypes';

/** Clon profundo del árbol del editor (JSON-safe). */
export function cloneEditorTree(tree: EditorMenuItem[]): EditorMenuItem[] {
  return JSON.parse(JSON.stringify(tree)) as EditorMenuItem[];
}

type TreeHistoryState = {
  past: EditorMenuItem[][];
  present: EditorMenuItem[];
  future: EditorMenuItem[][];
};

type TreeHistoryAction =
  | { type: 'reset'; tree: EditorMenuItem[] }
  | { type: 'commit'; tree: EditorMenuItem[]; record: boolean }
  | { type: 'undo' }
  | { type: 'redo' };

function treeHistoryReducer(state: TreeHistoryState, action: TreeHistoryAction): TreeHistoryState {
  switch (action.type) {
    case 'reset':
      return { past: [], present: cloneEditorTree(action.tree), future: [] };
    case 'commit': {
      const next = cloneEditorTree(action.tree);
      if (!action.record) {
        return { ...state, present: next };
      }
      return {
        past: [...state.past, cloneEditorTree(state.present)].slice(-80),
        present: next,
        future: [],
      };
    }
    case 'undo': {
      if (!state.past.length) return state;
      const prev = state.past[state.past.length - 1]!;
      return {
        past: state.past.slice(0, -1),
        present: cloneEditorTree(prev),
        future: [cloneEditorTree(state.present), ...state.future],
      };
    }
    case 'redo': {
      if (!state.future.length) return state;
      const nxt = state.future[0]!;
      return {
        past: [...state.past, cloneEditorTree(state.present)],
        present: cloneEditorTree(nxt),
        future: state.future.slice(1),
      };
    }
    default:
      return state;
  }
}

const initialTreeHistory: TreeHistoryState = { past: [], present: [], future: [] };

/**
 * Estado del árbol con deshacer/rehacer solo en cliente (patrón withHistory).
 * `record: true` en commit acumula pasos; `reset` limpia historial (carga desde servidor).
 */
export function useEditorTreeHistory() {
  const [state, dispatch] = useReducer(treeHistoryReducer, initialTreeHistory);

  const reset = useCallback((tree: EditorMenuItem[]) => {
    dispatch({ type: 'reset', tree });
  }, []);

  const commit = useCallback((tree: EditorMenuItem[], record: boolean) => {
    dispatch({ type: 'commit', tree, record });
  }, []);

  const undo = useCallback(() => {
    dispatch({ type: 'undo' });
  }, []);

  const redo = useCallback(() => {
    dispatch({ type: 'redo' });
  }, []);

  return {
    present: state.present,
    reset,
    commit,
    undo,
    redo,
    canUndo: state.past.length > 0,
    canRedo: state.future.length > 0,
  };
}
