namespace ERP.Domain.Exceptions;

/// <summary>
/// Se lanza cuando se intenta una mutación no permitida sobre un registro sembrado automáticamente
/// por el Bootstrap del sistema. Subclase de <see cref="InvalidOperationException"/> para que
/// <c>ExceptionMiddleware</c> la traduzca a HTTP 422 sin requerir un caso nuevo. Ver
/// <c>ERP.Domain.Common.ISystemSeeded</c> / <c>SystemSeedGuard</c>.
/// </summary>
public sealed class SystemSeededRecordException : InvalidOperationException
{
    public SystemSeededRecordException(string entityLabel, string action)
        : base($"{entityLabel} es un registro sembrado por el sistema y no puede {action}.") { }
}
