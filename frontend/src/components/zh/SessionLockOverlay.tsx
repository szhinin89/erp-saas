import { useNavigate } from "react-router-dom";
import { useSessionIdleStore } from "../../store/sessionIdleStore";
import { logoutSession } from "../../lib/session/logoutSession";
import { ZHBtn } from "./ZHForm";

/**
 * Overlay global de bloqueo por inactividad (Fase 3). Reutiliza las clases del modal ZH
 * (`zh-modal-overlay`/`zh-modal`/`zh-confirm-dialog`) sin duplicar CSS — a diferencia de
 * ZHConfirmModal, deliberadamente NO cierra al hacer click en el backdrop: es una pantalla
 * de bloqueo, no un diálogo cancelable.
 *
 * Preparado para Fase 4 (reautenticación en el mismo modal): el botón hoy hace logout +
 * redirect; ahí se reemplaza/extiende con el formulario de contraseña sin tocar el
 * mecanismo de detección de inactividad ni el store (`isLocked` seguiría siendo la fuente
 * de verdad de "mostrar overlay").
 */
export function SessionLockOverlay() {
  const isLocked = useSessionIdleStore((s) => s.isLocked);
  const navigate = useNavigate();

  if (!isLocked) return null;

  const handleGoToLogin = () => {
    void logoutSession().finally(() => navigate("/login"));
  };

  return (
    <div className="zh-modal-overlay" role="presentation">
      <div
        className="zh-modal zh-modal--sm zh-confirm-dialog"
        role="alertdialog"
        aria-modal="true"
        aria-label="Sesión pausada por inactividad"
      >
        <div className="zh-confirm-header">
          <span className="material-symbols-outlined zh-confirm-icon zh-confirm-icon--warning">
            lock
          </span>
          <h3 className="zh-confirm-title">Sesión pausada por inactividad</h3>
        </div>
        <div className="zh-confirm-body">
          <p className="zh-confirm-message">
            Por seguridad, bloqueamos la pantalla después de un tiempo sin
            uso. Para continuar, vuelve a iniciar sesión.
          </p>
        </div>
        <div className="zh-modal-footer">
          <ZHBtn
            type="button"
            variant="primary"
            size="md"
            onClick={handleGoToLogin}
          >
            Ir al inicio de sesión
          </ZHBtn>
        </div>
      </div>
    </div>
  );
}
