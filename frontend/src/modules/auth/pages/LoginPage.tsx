import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useNavigate } from "react-router-dom";
import { authService } from "../api/authService";
import { useAuthStore } from "../../../store/authStore";
import { completeLoginNavigation } from "../completeLoginNavigation";
import { useI18n } from "../../../i18n/i18n";
import {
  loginSchema,
  type LoginFormValues,
} from "../../../schemas/auth/loginSchema";
import { formatApiRequestError } from "../../lib/apiError";
import "./LoginPage.css";

export function LoginPage() {
  const navigate = useNavigate();
  const login = useAuthStore((s) => s.login);
  const { t } = useI18n();

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
  const [showPassword, setShowPassword] = useState(false);

  const onValid = async (form: LoginFormValues) => {
    setError("");
    setLoading(true);
    try {
      const payload = await authService.loginUser({
        username: form.username.trim().toLowerCase(),
        password: form.password,
      });

      if (payload.requiresPasswordReset) {
        // Sin sesión real (Token viene vacío) — no se llama login(). El token viaja por router
        // state, nunca por URL: es de un solo uso y de vida corta.
        navigate("/complete-password-reset", {
          replace: true,
          state: { passwordResetToken: payload.passwordResetToken },
        });
        return;
      }

      completeLoginNavigation(payload, login, navigate);
    } catch (err: unknown) {
      setError(
        formatApiRequestError(err, {
          offline: t("common.apiUnreachable"),
          generic: t("login.error.default"),
        }),
      );
    } finally {
      setLoading(false);
    }
  };

  /* ── Render ───────────────────────────────────────────────── */

  return (
    <div className="zh-auth-bg">
      {/* Decorative background */}
      <div className="zh-auth-bg-orb zh-auth-bg-orb--tr" aria-hidden="true" />
      <div className="zh-auth-bg-orb zh-auth-bg-orb--bl" aria-hidden="true" />
      <div className="zh-auth-bg-grid" aria-hidden="true" />

      <div className="zh-auth-wrapper">
        {/* ── Brand ── */}
        <div className="lp-brand">
          <div className="lp-brand-row">
            <div className="lp-brand-icon" aria-hidden="true">
              <span className="material-symbols-outlined">dashboard</span>
            </div>
            <h1 className="lp-brand-name" data-testid="erp-brand-title">
              ZH Technologies
            </h1>
          </div>
          <p className="lp-brand-sub">Acceso al Portal ERP Corporativo</p>
        </div>

        {/* ── Login card ── */}
        <div className="zh-auth-card zh-auth-card--flush">
          <div className="zh-auth-card-body">
            {/* Error alert */}
            {error && (
              <div className="lp-error" role="alert">
                <span className="material-symbols-outlined" aria-hidden="true">
                  error
                </span>
                <span>{error}</span>
              </div>
            )}

            {/* Form */}
            <form
              className="zh-auth-form"
              onSubmit={handleSubmit(onValid)}
              noValidate
            >
              {/* Username */}
              <div
                className={`zh-auth-field${errors.username ? " zh-auth-field--error" : ""}`}
              >
                <label className="zh-auth-label" htmlFor="lp-username">
                  {t("login.username.label", "Usuario")}
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
                    id="lp-username"
                    type="text"
                    placeholder={t("login.username.placeholder", "jperez")}
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

              {/* Password */}
              <div
                className={`zh-auth-field${errors.password ? " zh-auth-field--error" : ""}`}
              >
                <div className="zh-auth-field-header">
                  <label className="zh-auth-label" htmlFor="lp-password">
                    {t("login.password.label")}
                  </label>
                  <button
                    type="button"
                    className="zh-auth-link"
                    onClick={() => navigate("/forgot-password")}
                  >
                    {t("login.forgotPassword")}
                  </button>
                </div>
                <div className="zh-auth-input-wrap">
                  <span
                    className="zh-auth-input-icon material-symbols-outlined"
                    aria-hidden="true"
                  >
                    lock
                  </span>
                  <input
                    className="zh-auth-input lp-input-password"
                    id="lp-password"
                    type={showPassword ? "text" : "password"}
                    placeholder={t("login.password.placeholder")}
                    autoComplete="current-password"
                    disabled={loading}
                    {...register("password")}
                  />
                  <button
                    type="button"
                    className="lp-password-toggle"
                    onClick={() => setShowPassword((v) => !v)}
                    aria-label={
                      showPassword ? "Ocultar contraseña" : "Mostrar contraseña"
                    }
                  >
                    <span className="material-symbols-outlined">
                      {showPassword ? "visibility_off" : "visibility"}
                    </span>
                  </button>
                </div>
                {errors.password?.message && (
                  <span className="zh-auth-field-error" role="alert">
                    {errors.password.message}
                  </span>
                )}
              </div>

              {/* Remember me */}
              <div className="lp-remember">
                <input type="checkbox" id="lp-remember" />
                <label className="lp-remember-label" htmlFor="lp-remember">
                  Recordar sesión en este equipo
                </label>
              </div>

              {/* Submit */}
              <button
                type="submit"
                className={`zh-auth-submit${loading ? " zh-auth-submit--loading" : ""}`}
                disabled={loading}
              >
                {loading ? (
                  <>
                    <span className="zh-auth-submit-spinner" aria-hidden="true" />
                    <span>{t("login.button.loading")}</span>
                  </>
                ) : (
                  <>
                    <span>{t("login.button.submit")}</span>
                    <span
                      className="material-symbols-outlined"
                      aria-hidden="true"
                    >
                      login
                    </span>
                  </>
                )}
              </button>
            </form>
          </div>

          {/* Card footer */}
          <div className="lp-card-footer">
            <span className="lp-footer-copy">© 2024 ZH Technologies</span>
            <nav className="lp-footer-links" aria-label="Vínculos legales">
              <a className="zh-auth-footer-link" href="#">
                Soporte
              </a>
              <a className="zh-auth-footer-link" href="#">
                Legal
              </a>
            </nav>
          </div>
        </div>

        {/* Security badges */}
        <div className="lp-security" aria-label="Certificaciones de seguridad">
          <div className="lp-security-badge">
            <span className="material-symbols-outlined" aria-hidden="true">
              verified_user
            </span>
            <span className="lp-security-text">AES-256 Encrypted</span>
          </div>
          <span className="lp-security-dot" aria-hidden="true" />
          <div className="lp-security-badge">
            <span className="material-symbols-outlined" aria-hidden="true">
              shield
            </span>
            <span className="lp-security-text">SOC2 Compliant</span>
          </div>
        </div>
      </div>
    </div>
  );
}
