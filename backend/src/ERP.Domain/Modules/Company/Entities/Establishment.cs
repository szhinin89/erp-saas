using ERP.Domain.Branches.Entities;
using ERP.Domain.Common;

namespace ERP.Domain.Modules.Company.Entities;

/// <summary>
/// Establecimiento SRI â€” unidad fiscal registrada ante el SRI con cÃ³digo 001-999.
/// Pertenece a una <see cref="Branch"/> (sucursal operativa).
/// CompanyId es denormalizado desde Branch.CompanyId para soportar filtros globales multi-tenant.
/// </summary>
public sealed class Establishment : MasterEntity, ICompanyScopedEntity
{
    public const int CodeMaxLen    = 3;
    public const int NameMaxLen    = 200;
    public const int AddressMaxLen = 500;
    public const int PhoneMaxLen   = 40;

    /// <summary>Sucursal operativa propietaria de este establecimiento (FK primaria de ownership).</summary>
    public Guid    BranchId  { get; private set; }
    /// <summary>Denormalizado desde Branch.CompanyId; requerido por filtro global ICompanyScopedEntity.</summary>
    public Guid    CompanyId { get; private set; }
    public string  Code      { get; private set; } = null!;
    public string  Name      { get; private set; } = null!;
    public string  Address   { get; private set; } = null!;
    public string? Phone     { get; private set; }
    public bool    IsMain    { get; private set; }

    // EF navigation â€” no exponer como colecciones mutables
    public Branch                     Branch         { get; private set; } = null!;
    public Company                    Company        { get; private set; } = null!;
    public ICollection<EmissionPoint> EmissionPoints { get; private set; } = [];

    private Establishment() { }

    public static Establishment Create(
        Guid    subscriberId,
        Guid    branchId,
        Guid    companyId,
        string  code,
        string  name,
        string  address,
        string? phone,
        bool    isMain,
        Guid    createdBy)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("El cÃ³digo de establecimiento es obligatorio.", nameof(code));

        var e = new Establishment
        {
            Id           = Guid.NewGuid(),
            SubscriberId = subscriberId,
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
}
