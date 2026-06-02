namespace ERP.Domain.Common;

/// <summary>
/// Tipo de nota comercial. Concepto de negocio puro — sin conversiones ni mapeos.
/// DB persistence y XML SRI viven en ERP.Infrastructure.Persistence.Converters.NoteTypeConversions.
/// </summary>
public enum NoteType
{
    Credit,
    Debit
}
