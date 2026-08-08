using ERP.Domain.Common;
using ERP.Domain.Modules.Company.Enums;

namespace ERP.Domain.Modules.Company.Entities;

/// <summary>
/// Punto de emisión SRI — dispositivo o canal de emisión (código 001-999) dentro de un establecimiento.
/// Cada punto mantiene secuenciales independientes por tipo documental (<see cref="DocumentSequence"/>).
/// </summary>
public sealed class EmissionPoint : MasterEntity, ITenantScopedEntity, ICompanyScopedEntity
{
    public const int CodeMaxLen = 3;
    public const int NameMaxLen = 100;

    public Guid CompanyId { get; private set; }
    public Guid EstablishmentId { get; private set; }
    public string Code { get; private set; } = null!;
    public string? Name { get; private set; }
    public EmissionType EmissionType { get; private set; }

    /// <summary>Punto de emisión predeterminado del establecimiento; usado cuando el comando no especifica uno.</summary>
    public bool IsDefault { get; private set; }

    // EF navigation
    public Company Company { get; private set; } = null!;
    public Establishment Establishment { get; private set; } = null!;
    public ICollection<DocumentSequence> Sequences { get; private set; } = [];

    private EmissionPoint() { }

    public static EmissionPoint Create(
        Guid tenantId,
        Guid companyId,
        Guid establishmentId,
        string code,
        string? name,
        EmissionType emissionType,
        bool isDefault,
        Guid createdBy
    )
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException(
                "El código de punto de emisión es obligatorio.",
                nameof(code)
            );

        var ep = new EmissionPoint
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CompanyId = companyId,
            EstablishmentId = establishmentId,
            Code = code.Trim().PadLeft(CodeMaxLen, '0'),
            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
            EmissionType = emissionType,
            IsDefault = isDefault,
        };
        ep.SetCreated(createdBy);
        return ep;
    }

    /// <summary>
    /// Fábrica exclusiva del Bootstrap de empresa: idéntica a <see cref="Create"/> pero marca el
    /// registro como sembrado por el sistema (<see cref="MasterEntity.IsSystemSeeded"/>), bloqueando
    /// <see cref="Disable"/> — Tipo A (Punto de Emisión 001), ver política de Bootstrap en
    /// <c>CLAUDE.md</c>. <see cref="Update"/> permanece abierto.
    /// </summary>
    public static EmissionPoint CreateSystemSeeded(
        Guid tenantId,
        Guid companyId,
        Guid establishmentId,
        string code,
        string? name,
        EmissionType emissionType,
        bool isDefault,
        Guid createdBy
    )
    {
        var ep = Create(
            tenantId,
            companyId,
            establishmentId,
            code,
            name,
            emissionType,
            isDefault,
            createdBy
        );
        ep.MarkAsSystemSeeded();
        return ep;
    }

    public void Update(string? name, EmissionType emissionType, Guid updatedBy)
    {
        Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        EmissionType = emissionType;
        SetUpdated(updatedBy);
    }

    public void SetDefault(bool isDefault, Guid updatedBy)
    {
        IsDefault = isDefault;
        SetUpdated(updatedBy);
    }

    public override void Disable(Guid updatedBy)
    {
        this.EnsureEditable("El punto de emisión", "deshabilitarse");

        base.Disable(updatedBy);
    }
}
