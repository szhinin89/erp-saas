import { useCallback, useEffect, useState } from "react";
import { ZHBtn } from "../../../components/zh/ZHForm";
import { ZHConfirmModal } from "../../../components/zh/ZHConfirmModal";
import { ZHModal } from "../../../components/zh/ZHModal";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import { adminCoreService } from "../api/adminCoreService";
import { formatApiRequestError } from "../../lib/apiError";
import type {
  AdminCoreCompany,
  AdminCoreCompanyAdministrator,
  AdminCoreRegeneratedPassword,
} from "../../../types/adminCore";
import { TemporaryCredentialsPanel } from "./TemporaryCredentialsPanel";

interface Props {
  company: AdminCoreCompany | null;
  onClose: () => void;
}

/**
 * Recuperación de credenciales (IAM-V1-F3): lista los administradores empresariales (no globales)
 * y permite «Generar nueva contraseña temporal» con confirmación. La contraseña nueva se muestra una
 * sola vez, en este modal; al cerrarlo el estado se descarta y no puede consultarse de nuevo.
 */
export function CompanyAdministratorsModal({ company, onClose }: Props) {
  const [admins, setAdmins] = useState<AdminCoreCompanyAdministrator[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [confirming, setConfirming] = useState<AdminCoreCompanyAdministrator | null>(null);
  const [working, setWorking] = useState(false);
  const [result, setResult] = useState<AdminCoreRegeneratedPassword | null>(null);

  const companyId = company?.companyId ?? null;

  const load = useCallback(async (id: string) => {
    setLoading(true);
    setError("");
    try {
      setAdmins(await adminCoreService.listCompanyAdministrators(id));
    } catch (e) {
      setError(
        formatApiRequestError(e, {
          offline: "No se pudo conectar con el servidor.",
          generic: "No se pudieron cargar los administradores de la empresa.",
        }),
      );
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    // Cada apertura parte de cero: ninguna contraseña anterior sobrevive.
    setResult(null);
    setConfirming(null);
    setAdmins([]);
    if (companyId) void load(companyId);
  }, [companyId, load]);

  const close = () => {
    setResult(null);
    setConfirming(null);
    onClose();
  };

  const regenerate = async () => {
    if (!companyId || !confirming) return;
    const target = confirming;
    setConfirming(null);
    setWorking(true);
    setError("");
    try {
      setResult(
        await adminCoreService.regenerateAdministratorPassword(companyId, target.identityUserId),
      );
      void load(companyId);
    } catch (e) {
      setError(
        formatApiRequestError(e, {
          offline: "No se pudo conectar con el servidor.",
          generic: "No se pudo generar la contraseña temporal.",
        }),
      );
    } finally {
      setWorking(false);
    }
  };

  return (
    <>
      <ZHModal
        open={company !== null}
        title="Administradores de la empresa"
        subtitle={company?.legalName}
        onClose={close}
        size="md"
        footer={
          <ZHBtn type="button" variant="ghost" onClick={close}>
            Cerrar
          </ZHBtn>
        }
      >
        {error ? <ZHPageNotice variant="error" message={error} /> : null}
        {result ? (
          <TemporaryCredentialsPanel
            title="Nueva contraseña temporal generada. La anterior quedó invalidada y sus sesiones se cerraron."
            username={result.username}
            temporaryPassword={result.temporaryPassword}
          />
        ) : null}
        {loading ? <p>Cargando administradores…</p> : null}
        {!loading && admins.length === 0 && !error ? (
          <p>Esta empresa no tiene administradores propios registrados.</p>
        ) : null}
        <ul>
          {admins.map((a) => (
            <li key={a.identityUserId} className="zh-mb-16">
              <strong>{a.fullName || a.username}</strong> · {a.username}
              {a.email ? ` · ${a.email}` : ""}
              {a.passwordResetPending ? " · cambio de contraseña pendiente" : ""}{" "}
              <ZHBtn
                variant="secondary"
                size="sm"
                type="button"
                disabled={working}
                onClick={() => setConfirming(a)}
              >
                Generar nueva contraseña temporal
              </ZHBtn>
            </li>
          ))}
        </ul>
      </ZHModal>
      <ZHConfirmModal
        open={confirming !== null}
        title="Generar nueva contraseña temporal"
        message={
          <>
            Se generará una contraseña nueva para <strong>{confirming?.username}</strong>. La
            contraseña actual quedará invalidada, se cerrarán sus sesiones activas y deberá
            cambiarla en su próximo ingreso. La nueva contraseña se mostrará una sola vez.
          </>
        }
        confirmLabel="Generar contraseña"
        variant="warning"
        onConfirm={() => void regenerate()}
        onCancel={() => setConfirming(null)}
      />
    </>
  );
}
