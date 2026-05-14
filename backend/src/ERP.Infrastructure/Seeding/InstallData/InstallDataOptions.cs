namespace ERP.Infrastructure.Seeding.InstallData;

public sealed class InstallDataOptions
{
    public const string SectionName = "InstallData";
    public const string DestructiveConfirmPhrase = "CONFIRM_INSTALLDATA_DELETE";

    /// <summary>
    /// Habilita la carga automática de scripts de datos de instalación.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Permite ejecutar scripts detectados como destructivos (DELETE/TRUNCATE/DROP o marcador explícito).
    /// Requiere también <see cref="ConfirmDestructive"/> con la frase de confirmación exacta.
    /// </summary>
    public bool AllowDestructive { get; set; } = false;

    /// <summary>
    /// Confirmación explícita para operaciones destructivas.
    /// Debe ser exactamente: CONFIRM_INSTALLDATA_DELETE.
    /// </summary>
    public string ConfirmDestructive { get; set; } = string.Empty;
}
