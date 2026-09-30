import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useNavigate } from "react-router-dom";
import {
  completeLoginNavigation,
  globalAdminAuthFacade,
} from "../../auth/facades/globalAdminAuthFacade";
import { useAuthStore } from "../../../store/authStore";
import { useDocumentTitle } from "../../../hooks/useDocumentTitle";
import {
  loginSchema,
  type LoginFormValues,
} from "../../../schemas/auth/loginSchema";
import { formatApiRequestError } from "../../lib/apiError";

/**
 * Login exclusivo de AdminGlobalCore — llama global-login y siempre navega al dashboard
 * global (nunca a /select-company ni a /dashboard operativo). Usa solo el Design System de
 * pantallas de acceso (zh-auth-* en zh-ui.css), sin CSS privado de LoginPage ni su formulario
 * o navegación de /login normal.
 */
export function AdminCoreLoginPage() {
  const navigate = useNavigate();
  const login = useAuthStore((s) => s.login);
  useDocumentTitle("Admin Core — Iniciar sesión");

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { username: "", password: "" },
  });

  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  const onValid = handleSubmit(async (form) => {
    setError("");
    setLoading(true);
    try {
      const payload = await globalAdminAuthFacade.globalLogin({
        username: form.username.trim().toLowerCase(),
        password: form.password,
      });
      completeLoginNavigation(payload, login, navigate);
    } catch (err: unknown) {
      setError(
        formatApiRequestError(err, {
          offline: "No se pudo conectar con el servidor.",
          generic: "No se pudo iniciar sesión.",
        }),
      );
    } finally {
      setLoading(false);
    }
  });

  return (
    <div className="zh-auth-bg">
      <div className="zh-auth-bg-orb zh-auth-bg-orb--tr" aria-hidden="true" />
      <div className="zh-auth-bg-orb zh-auth-bg-orb--bl" aria-hidden="true" />
      <div className="zh-auth-bg-grid" aria-hidden="true" />

      <div className="zh-auth-wrapper">
        <div className="zh-auth-logo">
          <div className="zh-auth-logo-row">
            <div className="zh-auth-logo-icon" aria-hidden="true">
              <span className="material-symbols-outlined">public</span>
            </div>
            <h1 className="zh-auth-logo-name">AdminGlobalCore</h1>
          </div>
          <p className="zh-auth-logo-sub">Administración global del ERP</p>
        </div>

        <div className="zh-auth-card zh-auth-card--flush">
          <div className="zh-auth-card-body">
            {error && (
              <div className="zh-auth-error" role="alert">
                <span className="material-symbols-outlined" aria-hidden="true">
                  error
                </span>
                <span>{error}</span>
              </div>
            )}

            <form className="zh-auth-form" onSubmit={onValid} noValidate>
              <div
                className={`zh-auth-field${errors.username ? " zh-auth-field--error" : ""}`}
              >
                <label className="zh-auth-label" htmlFor="ac-username">
                  Usuario
                </label>
                <div className="zh-auth-input-wrap">
                  <span
                    className="zh-auth-input-icon material-symbols-outlined"
                    aria-hidden="true"
                  >
                    person
                  </span>
                  <input
                    className="zh-auth-input"
                    id="ac-username"
                    type="text"
                    autoComplete="username"
                    disabled={loading}
                    {...register("username")}
                  />
                </div>
                {errors.username?.message && (
                  <span className="zh-auth-field-error" role="alert">
                    {errors.username.message}
                  </span>
                )}
              </div>

              <div
                className={`zh-auth-field${errors.password ? " zh-auth-field--error" : ""}`}
              >
                <label className="zh-auth-label" htmlFor="ac-password">
                  Contraseña
                </label>
                <div className="zh-auth-input-wrap">
                  <span
                    className="zh-auth-input-icon material-symbols-outlined"
                    aria-hidden="true"
                  >
                    lock
                  </span>
                  <input
                    className="zh-auth-input"
                    id="ac-password"
                    type="password"
                    autoComplete="current-password"
                    disabled={loading}
                    {...register("password")}
                  />
                </div>
                {errors.password?.message && (
                  <span className="zh-auth-field-error" role="alert">
                    {errors.password.message}
                  </span>
                )}
              </div>

              <button
                type="submit"
                className={`zh-auth-submit${loading ? " zh-auth-submit--loading" : ""}`}
                disabled={loading}
              >
                {loading ? (
                  <>
                    <span className="zh-auth-submit-spinner" aria-hidden="true" />
                    <span>Iniciando sesión…</span>
                  </>
                ) : (
                  <>
                    <span>Ingresar</span>
                    <span className="material-symbols-outlined" aria-hidden="true">
                      login
                    </span>
                  </>
                )}
              </button>
            </form>
          </div>
        </div>
      </div>
    </div>
  );
}
