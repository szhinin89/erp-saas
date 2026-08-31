import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { useSessionIdleStore } from "../../store/sessionIdleStore";
import { useAuthStore } from "../../store/authStore";
import { logoutSession } from "../../lib/session/logoutSession";
import {
  reauthenticate,
  isSessionInvalidError,
} from "../../lib/session/reauthenticate";
import { broadcastIdleUnlock } from "../../lib/session/idleBroadcast";
import { ZHBtn, ZHField } from "./ZHForm";

/**
 * Overlay global de bloqueo por inactividad (Fase 3) + reautenticación en el mismo modal
 * (Fase 4). Reutiliza las clases del modal ZH (`zh-modal-overlay`/`zh-modal`/`zh-confirm-dialog`
 * /`ZHField`) sin CSS nuevo. Deliberadamente NO cierra al hacer click en el backdrop: es una
 * pantalla de bloqueo, no un diálogo cancelable.
 *
 * Éxito de reautenticación: `unlock()` + `broadcastIdleUnlock()`, sin navegar ni recargar — la
 * ruta y cualquier formulario en memoria de la pantalla de abajo quedan intactos.
 */
export function SessionLockOverlay() {
  const isLocked = useSessionIdleStore((s) => s.isLocked);
  const unlock = useSessionIdleStore((s) => s.unlock);
  const user = useAuthStore((s) => s.user);
  const navigate = useNavigate();

  const [password, setPassword] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [sessionInvalid, setSessionInvalid] = useState(false);

  if (!isLocked) return null;

  const displayName = user?.fullName || user?.username || user?.email || "";

  const handleExit = () => {
    void logoutSession().finally(() => navigate("/login"));
  };

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();
    if (!password.trim() || submitting) return;

    setSubmitting(true);
    setError(null);

    reauthenticate(password)
      .then(() => {
        setPassword("");
        unlock();
        broadcastIdleUnlock();
      })
      .catch((err: unknown) => {
        const message =
          err instanceof Error ? err.message : "No se pudo reautenticar.";
        // Nunca dejar la contraseña escrita en el DOM tras un intento, exitoso o no.
        setPassword("");
        setError(message);
        setSessionInvalid(isSessionInvalidError(message));
      })
      .finally(() => setSubmitting(false));
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
        <form onSubmit={handleSubmit}>
          <div className="zh-confirm-body">
            <p className="zh-confirm-message">
              Por seguridad, bloqueamos la pantalla después de un tiempo sin
              uso. Para continuar, vuelve a iniciar sesión.
            </p>

            {displayName ? (
              <ZHField density="compact" label="Usuario">
                <input type="text" value={displayName} readOnly disabled />
              </ZHField>
            ) : null}

            {!sessionInvalid && (
              <ZHField
                density="compact"
                label="Contraseña"
                error={error}
              >
                <input
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  autoFocus
                  autoComplete="current-password"
                  disabled={submitting}
                />
              </ZHField>
            )}

            {sessionInvalid && error ? (
              <p className="zh-field-hint zh-field-hint--error">{error}</p>
            ) : null}
          </div>
          <div className="zh-modal-footer">
            <ZHBtn
              type="button"
              variant={sessionInvalid ? "primary" : "ghost"}
              size="md"
              onClick={handleExit}
            >
              {sessionInvalid ? "Ir al inicio de sesión" : "Salir"}
            </ZHBtn>
            {!sessionInvalid && (
              <ZHBtn
                type="submit"
                variant="primary"
                size="md"
                disabled={!password.trim() || submitting}
              >
                {submitting ? "Verificando…" : "Continuar"}
              </ZHBtn>
            )}
          </div>
        </form>
      </div>
    </div>
  );
}
