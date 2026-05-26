using ERP.Domain.Common;

namespace ERP.Domain.Modules.Company.Entities;

/// <summary>
/// Punto de emisión SRI — dispositivo o canal de emisión (código 001-999) dentro de un establecimiento.
/// Cada punto mantiene secuenciales independientes por tipo documental (<see cref="DocumentSequence"/>).
/// </summary>
public sealed class EmissionPoint : MasterEntity, ICompanyScopedEntity
{
    public const int CodeMaxLen = 3;
    public const int NameMaxLen = 100;

    public Guid    CompanyId       { get; private set; }
    public Guid    EstablishmentId { get; private set; }
    public string  Code            { get; private set; } = null!;
    public string? Name            { get; private set; }
    /// <summary>Punto de emisión predeterminado del establecimiento; usado cuando el comando no especifica uno.</summary>
    public bool    IsDefault       { get; private set; }

    // EF navigation
    public Company                       Company       { get; private set; } = null!;
    public Establishment                 Establishment { get; private set; } = null!;
    public ICollection<DocumentSequence> Sequences     { get; private set; } = [];

    private EmissionPoint() { }

    public static EmissionPoint Create(
        Guid    subscriberId,
        Guid    companyId,
        Guid    establishmentId,
        string  code,
        string? name,
        bool    isDefault,
        Guid    createdBy)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("El código de punto de emisión es obligatorio.", nameof(code));

        var ep = new EmissionPoint
        {
            Id              = Guid.NewGuid(),
            SubscriberId    = subscriberId,
            CompanyId       = companyId,
            EstablishmentId = establishmentId,
            Code            = code.Trim().PadLeft(CodeMaxLen, '0'),
            Name            = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
            IsDefault       = isDefault,
        };
        ep.SetCreated(createdBy);
        return ep;
    }

    public void SetDefault(bool isDefault, Guid updatedBy)
    {
        IsDefault = isDefault;
        SetUpdated(updatedBy);
    }
}
