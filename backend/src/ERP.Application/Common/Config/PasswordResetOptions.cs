namespace ERP.Application.Common.Config;

/// <summary>Configuración para enlaces de recuperación de contraseña (sección <c>PasswordReset</c>).</summary>
public sealed class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    /// <summary>URL pública del frontend (sin barra final), p. ej. https://app.ejemplo.com o http://localhost:5173</summary>
    public string PublicBaseUrl { get; set; } = "";

    /// <summary>Vigencia del token desde su creación (por defecto 60 minutos).</summary>
    public int TokenLifetimeMinutes { get; set; } = 60;
}
