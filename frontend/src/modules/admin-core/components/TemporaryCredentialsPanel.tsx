import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { copyToClipboard } from "../../../lib/clipboard";
import { message } from "../../../lib/messages";

interface Props {
  /** Texto introductorio (qué credenciales son). */
  title: string;
  username: string;
  temporaryPassword: string;
}

/**
 * Entrega inicial de credenciales temporales (IAM-V1-F3). La contraseña llega SOLO en la
 * respuesta de creación o de regeneración y vive únicamente en el estado del componente que la
 * recibió: nunca se escribe en localStorage/sessionStorage/URL/logs. Al cerrar la pantalla o el
 * modal el estado desaparece y no hay forma de consultarla otra vez (solo generar una nueva).
 */
export function TemporaryCredentialsPanel({ title, username, temporaryPassword }: Props) {
  const copy = async (value: string, what: string) => {
    const ok = await copyToClipboard(value);
    if (ok) message.success(`${what} copiado.`);
    else message.error(`No se pudo copiar el ${what.toLowerCase()}. Cópialo manualmente.`);
  };

  return (
    <div role="group" aria-label="Credenciales temporales">
      <ZHPageNotice
        variant="warning"
        message="Esta contraseña se muestra una sola vez."
        detail="Cópiala ahora y entrégala de forma segura. El administrador deberá cambiarla en su primer ingreso. Si se pierde, deberás generar una nueva."
      />
      <p>
        <strong>{title}</strong>
      </p>
      <dl className="zh-mb-16">
        <dt>Usuario</dt>
        <dd>
          <code data-testid="credentials-username">{username}</code>{" "}
          <ZHBtn
            variant="ghost"
            size="sm"
            type="button"
            onClick={() => void copy(username, "Usuario")}
          >
            Copiar usuario
          </ZHBtn>
        </dd>
        <dt>Contraseña temporal</dt>
        <dd>
          <code data-testid="credentials-password">{temporaryPassword}</code>{" "}
          <ZHBtn
            variant="ghost"
            size="sm"
            type="button"
            onClick={() => void copy(temporaryPassword, "Contraseña")}
          >
            Copiar contraseña
          </ZHBtn>
        </dd>
      </dl>
    </div>
  );
}
