import { useCallback, useEffect, useRef, useState } from "react";
import { useAuthStore } from "../store/authStore";
import { useActiveBranchStore } from "../store/activeBranchStore";
import { sessionService } from "../modules/session/api/sessionService";
import { formatApiRequestError } from "../modules/lib/apiError";
import type { SessionBranchDto } from "../types/session";

type BranchGateStatus = "ready" | "checking" | "selecting";

/**
 * Orquesta la selección de sucursal post-login: si el bootstrap (GET /session/context) ya
 * resolvió una sucursal activa, el gate queda abierto de inmediato. Si no, consulta
 * GET /session/available-branches y decide entre switch-branch automático (LoginMode
 * DirectToDefault con DefaultBranchId autorizado) o selección manual bloqueante (AskBranch,
 * o DirectToDefault con default ya revocado). No duplica ninguna regla de autorización — el
 * backend (IBranchAccessGuard) sigue siendo la única fuente de verdad; este hook solo decide
 * cuándo pedirle al usuario que elija.
 */
export function useBranchGate() {
  const companyId = useAuthStore((s) => s.user?.companyId);
  const branch = useActiveBranchStore((s) => s.branch);
  const setBranch = useActiveBranchStore((s) => s.setBranch);

  const [status, setStatus] = useState<BranchGateStatus>("ready");
  const [options, setOptions] = useState<SessionBranchDto[]>([]);
  const [error, setError] = useState("");
  const [switching, setSwitching] = useState(false);
  const checkedForCompanyRef = useRef<string | null>(null);

  const loadOptions = useCallback(async () => {
    setStatus("checking");
    setError("");
    try {
      const available = await sessionService.getAvailableBranches();

      if (
        available.loginMode === "DirectToDefault" &&
        available.defaultBranchId
      ) {
        const match = available.branches.find(
          (b) => b.id === available.defaultBranchId,
        );
        if (match) {
          try {
            const switched = await sessionService.switchBranch(match.id);
            setBranch(switched);
            setStatus("ready");
            return;
          } catch {
            // El default ya no está autorizado (revocado después de configurar la preferencia)
            // — cae a selección manual en vez de bloquear al usuario sin salida.
          }
        }
      }

      setOptions(available.branches);
      setStatus("selecting");
    } catch (err) {
      setError(
        formatApiRequestError(err, {
          generic: "No se pudieron cargar las sucursales disponibles.",
        }),
      );
      setOptions([]);
      setStatus("selecting");
    }
  }, [setBranch]);

  useEffect(() => {
    if (!companyId || branch) {
      checkedForCompanyRef.current = null;
      setStatus("ready");
      return;
    }
    if (checkedForCompanyRef.current === companyId) return;
    checkedForCompanyRef.current = companyId;
    void loadOptions();
  }, [companyId, branch, loadOptions]);

  const selectBranch = useCallback(
    async (branchId: string) => {
      setSwitching(true);
      setError("");
      try {
        const switched = await sessionService.switchBranch(branchId);
        setBranch(switched);
        setStatus("ready");
      } catch (err) {
        setError(
          formatApiRequestError(err, {
            generic: "No se pudo cambiar de sucursal.",
          }),
        );
      } finally {
        setSwitching(false);
      }
    },
    [setBranch],
  );

  const gateOpen = !!companyId && status !== "ready";

  return {
    gateOpen,
    loading: status === "checking",
    options,
    error,
    switching,
    selectBranch,
    retry: loadOptions,
  };
}
