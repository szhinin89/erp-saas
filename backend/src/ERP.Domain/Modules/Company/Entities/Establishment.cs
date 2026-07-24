using ERP.Domain.Branches.Entities;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Company.Entities;

/// <summary>
/// Establecimiento SRI — unidad fiscal registrada ante el SRI con código 001-999.
/// Pertenece a una <see cref="Branch"/> (sucursal operativa).
/// CompanyId es denormalizado desde Branch.CompanyId para soportar filtros globales multi-tenant.
/// </summary>
public sealed class Establishment : MasterEntity, ITenantScopedEntity, ICompanyScopedEntity
{
    public const int CodeMaxLen    = 3;
    public const int NameMaxLen    = 200;
    public const int AddressMaxLen = 500;
    public const int PhoneMaxLen   = 40;

    /// <summary>Sucursal operativa con la que se agrupa este establecimiento. Opcional � el SRI no requiere sucursal para registrar un establecimiento.</summary>
    public Guid?   BranchId  { get; private set; }
    /// <summary>Denormalizado desde Branch.CompanyId; requerido por filtro global ICompanyScopedEntity.</summary>
    public Guid    CompanyId { get; private set; }
    public string  Code      { get; private set; } = null!;
    public string  Name      { get; private set; } = null!;
    public string  Address   { get; private set; } = null!;
    public string? Phone     { get; private set; }
    public bool    IsMain    { get; private set; }

    // EF navigation � no exponer como colecciones mutables
    public Branch?                    Branch         { get; private set; }
    public Company                    Company        { get; private set; } = null!;
    public ICollection<EmissionPoint> EmissionPoints { get; private set; } = [];

    private Establishment() { }

    public static Establishment Create(
        Guid    tenantId,
        Guid?   branchId,
        Guid    companyId,
        string  code,
        string  name,
        string  address,
        string? phone,
        bool    isMain,
        Guid    createdBy)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("El código de establecimiento es obligatorio.", nameof(code));

        var e = new Establishment
        {
            Id           = Guid.NewGuid(),
            TenantId = tenantId,
            BranchId     = branchId,
            CompanyId = companyId,
            Code         = code.Trim().PadLeft(CodeMaxLen, '0'),
            Name         = name.Trim(),
            Address      = address.Trim(),
            Phone        = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            IsMain       = isMain,
        };
        e.SetCreated(createdBy);
        return e;
    }

    /// <summary>
    /// Fábrica exclusiva del Bootstrap de empresa: idéntica a <see cref="Create"/> pero marca el
    /// registro como sembrado por el sistema (<see cref="MasterEntity.IsSystemSeeded"/>), bloqueando
    /// <see cref="Disable"/> — Tipo A (Establecimiento 001), ver política de Bootstrap en
    /// <c>CLAUDE.md</c>. <see cref="Update"/> permanece abierto.
    /// </summary>
    public static Establishment CreateSystemSeeded(
        Guid    tenantId,
        Guid?   branchId,
        Guid    companyId,
        string  code,
        string  name,
        string  address,
        string? phone,
        bool    isMain,
        Guid    createdBy)
    {
        var e = Create(tenantId, branchId, companyId, code, name, address, phone, isMain, createdBy);
        e.MarkAsSystemSeeded();
        return e;
    }

    public void Update(string name, string address, string? phone, Guid updatedBy)
    {
        Name    = name.Trim();
        Address = address.Trim();
        Phone   = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        SetUpdated(updatedBy);
    }

    public void SetMain(bool isMain, Guid updatedBy)
    {
        IsMain = isMain;
        SetUpdated(updatedBy);
    }

    public override void Disable(Guid updatedBy)
    {
        this.EnsureEditable("El establecimiento", "deshabilitarse");

        base.Disable(updatedBy);
    }
}
