import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useNavigate } from "react-router-dom";
import { api } from "../../lib/api";
import type { ApiResponse } from "../../../types/api";
import { useI18n } from "../../../i18n/i18n";
import { ZHPageNotice } from "../../../components/zh/ZHPageNotice";
import {
  forgotPasswordEmailSchema,
  type ForgotPasswordFormValues,
} from "../../../schemas/auth/forgotPasswordSchema";
import { formatApiRequestError, readApiErrorMessage } from "../../lib/apiError";
import "./ForgotPasswordPage.css";

export function ForgotPasswordPage() {
  const navigate = useNavigate();
  const { t } = useI18n();

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<ForgotPasswordFormValues>({
    resolver: zodResolver(forgotPasswordEmailSchema),
    defaultValues: { email: "" },
  });

  const [error, setError] = useState("");
  const [success, setSuccess] = useState("");
  const [loading, setLoading] = useState(false);

  const onValid = async (form: ForgotPasswordFormValues) => {
    setError("");
    setSuccess("");
    setLoading(true);
    try {
      await api.post<ApiResponse<object>>("/api/v1/auth/forgot-password", {
        email: form.email.trim().toLowerCase(),
      });
      setSuccess(t("forgot.success"));
    } catch (err: unknown) {
      const fromApi = readApiErrorMessage(err);
      if (fromApi) {
        setError(fromApi);
      } else {
        setError(
          formatApiRequestError(err, {
            offline: t("common.apiUnreachable"),
            generic: t("login.error.default"),
          }),
        );
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="zh-auth-bg">
      <div className="zh-auth-bg-orb zh-auth-bg-orb--tr" aria-hidden="true" />
      <div className="zh-auth-bg-orb zh-auth-bg-orb--bl" aria-hidden="true" />
      <div className="zh-auth-bg-grid" aria-hidden="true" />

      <div className="zh-auth-wrapper">
        {/* Marca */}
        <header className="zh-auth-brand">
          <div className="zh-auth-brand-icon" aria-hidden="true">
            <span className="material-symbols-outlined">shield_lock</span>
          </div>
          <h1 className="fp-brand-name">ZH Technologies</h1>
          <p className="fp-brand-sub">Portal de Administración</p>
        </header>

        {/* Card */}
        <div className="zh-auth-card">
          <div className="zh-auth-card-header">
            <h2 className="zh-auth-card-title">{t("forgot.title")}</h2>
            <p className="zh-auth-card-desc">
              No te preocupes. Introduce tu dirección de correo electrónico y te
              enviaremos las instrucciones para restablecerla.
            </p>
          </div>

          {error && (
            <ZHPageNotice
              variant="error"
              message={t("common.errorPrefix")}
              detail={error}
            />
          )}
          {success && <ZHPageNotice variant="success" message={success} />}

          <form
            className="zh-auth-form"
            onSubmit={handleSubmit(onValid)}
            noValidate
          >
            <div
              className={`zh-auth-field${errors.email ? " zh-auth-field--error" : ""}`}
            >
              <label className="zh-auth-label" htmlFor="email">
                {t("reset.email.label")}
              </label>
              <div className="zh-auth-input-wrap">
                <span
                  className="zh-auth-input-icon material-symbols-outlined"
                  aria-hidden="true"
                >
                  mail
                </span>
                <input
                  className="zh-auth-input"
                  id="email"
                  type="email"
                  placeholder={t("login.email.placeholder")}
                  autoComplete="username"
                  disabled={loading}
                  {...register("email")}
                />
              </div>
              {errors.email?.message && (
                <span className="zh-auth-field-error">
                  {errors.email.message}
                </span>
              )}
            </div>

            <button type="submit" className="zh-auth-submit" disabled={loading}>
              <span>
                {loading
                  ? t("forgot.button.loading")
                  : t("forgot.button.submit")}
              </span>
              {!loading && (
                <span className="material-symbols-outlined" aria-hidden="true">
                  send
                </span>
              )}
            </button>
          </form>

          <div className="zh-auth-divider">
            <div className="zh-auth-info-box">
              <span
                className="material-symbols-outlined zh-auth-info-icon"
                aria-hidden="true"
              >
                info
              </span>
              <div>
                <p className="zh-auth-info-title">¿Qué sucede después?</p>
                <ul className="zh-auth-info-list">
                  <li>Recibirás un enlace seguro en tu bandeja de entrada.</li>
                  <li>El enlace caducará en 60 minutos por seguridad.</li>
                  <li>Podrás elegir una nueva contraseña de acceso.</li>
                </ul>
              </div>
            </div>
          </div>

          <button
            type="button"
            className="zh-auth-back"
            onClick={() => navigate("/login")}
          >
            <span
              className="material-symbols-outlined zh-auth-back-arrow"
              aria-hidden="true"
            >
              arrow_back
            </span>
            Volver al inicio de sesión
          </button>
        </div>

        <div className="zh-auth-banner" aria-hidden="true">
          <span className="material-symbols-outlined zh-auth-banner-icon">
            security
          </span>
          <div className="zh-auth-banner-overlay" />
        </div>

        <footer className="zh-auth-footer">
          <div className="zh-auth-footer-links">
            <a href="#" className="zh-auth-footer-link">
              Centro de ayuda
            </a>
            <span className="zh-auth-footer-sep" aria-hidden="true">
              |
            </span>
            <a href="#" className="zh-auth-footer-link">
              Privacidad
            </a>
          </div>
          <p className="zh-auth-footer-copy">
            © 2024 ZH Technologies. Todos los derechos reservados.
          </p>
        </footer>
      </div>
    </div>
  );
}
