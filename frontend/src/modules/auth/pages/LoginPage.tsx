import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useNavigate } from "react-router-dom";
import { authService } from "../api/authService";
import { useAuthStore } from "../../../store/authStore";
import { completeLoginNavigation } from "../completeLoginNavigation";
import { useI18n } from "../../../i18n/i18n";
import { useDocumentTitle } from "../../../hooks/useDocumentTitle";
import {
  loginSchema,
  type LoginFormValues,
} from "../../../schemas/auth/loginSchema";
import { formatApiRequestError } from "../../lib/apiError";
import { brandConfig, getCopyrightText } from "../../../shared/branding/brandConfig";
import "./LoginPage.css";

export function LoginPage() {
  const navigate = useNavigate();
  const login = useAuthStore((s) => s.login);
  const { t } = useI18n();
  useDocumentTitle(t("login.title"));

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

  const copyrightText = getCopyrightText();

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
              {brandConfig.companyName}
            </h1>
          </div>
          <p className="lp-brand-sub">{brandConfig.productSubtitle}</p>
          <p className="lp-brand-desc">
            Administra ventas, compras, inventario, caja y facturación
            electrónica en un solo lugar.
          </p>
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
                    placeholder={t("login.username.placeholder", "ej. jperez")}
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
                <label className="zh-auth-label" htmlFor="lp-password">
                  {t("login.password.label")}
                </label>
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
                <button
                  type="button"
                  className="lp-forgot-link"
                  onClick={() => navigate("/forgot-password")}
                >
                  {t("login.forgotPassword")}
                </button>
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
            <span className="lp-footer-copy">{copyrightText}</span>
          </div>
        </div>

        {/* Security badges */}
        <div className="lp-security" aria-label="Seguridad de la conexión">
          <div className="lp-security-badge">
            <span className="material-symbols-outlined" aria-hidden="true">
              verified_user
            </span>
            <span className="lp-security-text">
              {brandConfig.secureAccessText}
            </span>
          </div>
          <span className="lp-security-dot" aria-hidden="true" />
          <div className="lp-security-badge">
            <span className="material-symbols-outlined" aria-hidden="true">
              shield
            </span>
            <span className="lp-security-text">
              {brandConfig.protectedAccessText}
            </span>
          </div>
        </div>
      </div>
    </div>
  );
}
