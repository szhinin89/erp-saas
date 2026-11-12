import type { ReactNode } from "react";
import { ZHTabBar } from "../zh/ZHTabBar";
import "../../styles/shared/items-catalog.css";

interface Props {
  /** Icono Material Symbols (p. ej. `edit` / `add_circle`). */
  icon: string;
  title: ReactNode;
  /** Segunda línea opcional (p. ej. el código del registro en edición). */
  subtitle?: ReactNode;
  /**
   * Rotula la única pestaña decorativa «General» de los paneles que la muestran. No navega:
   * se renderiza como pestaña activa e inerte (`ZHTabBar`), con el mismo aspecto de siempre.
   */
  tabLabel?: string;
}

const NOOP = () => {};

/**
 * Cabecera común de los paneles de alta/edición de configuración (Sucursales, Cajas, Secuencias
 * documentales, Puntos de emisión, Establecimientos): icono + título + subtítulo y, si el panel
 * la tiene, la pestaña «General». Reemplaza el mismo bloque JSX repetido en cada panel; no
 * sustituye a `ConfigTabsLayout` (FROZEN), que resuelve las pestañas Lista/Editor de la pantalla.
 */
export function ConfigFormPanelHeader({ icon, title, subtitle, tabLabel }: Readonly<Props>) {
  return (
    <>
      <div className="cfg-panel-hd">
        <span className="material-symbols-outlined cfg-panel-hd__icon">{icon}</span>
        <div>
          <p className="cfg-panel-hd__title">{title}</p>
          {subtitle ? <p className="cfg-panel-hd__sub">{subtitle}</p> : null}
        </div>
      </div>
      {tabLabel ? (
        <ZHTabBar
          className="cfg-panel-tabs"
          tabs={[{ id: "general", label: tabLabel, inert: true }]}
          activeTab="general"
          onChange={NOOP}
        />
      ) : null}
    </>
  );
}
