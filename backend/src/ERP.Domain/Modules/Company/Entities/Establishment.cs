using ERP.Domain.Common;

namespace ERP.Domain.Modules.Company.Entities;

/// <summary>
/// Establecimiento SRI — unidad fiscal registrada ante el SRI con código 001-999.
/// Asociado 1:1 con una sucursal operativa (<see cref="ERP.Domain.Branches.Entities.Branch"/>).
/// </summary>
public sealed class Establishment : MasterEntity, ICompanyScopedEntity
{
    public const int CodeMaxLen    = 3;
    public const int NameMaxLen    = 200;
    public const int AddressMaxLen = 500;
    public const int PhoneMaxLen   = 40;

    public Guid    CompanyId { get; private set; }
    public string  Code      { get; private set; } = null!;
    public string  Name      { get; private set; } = null!;
    public string  Address   { get; private set; } = null!;
    public string? Phone     { get; private set; }
    public bool    IsMain    { get; private set; }

    // EF navigation — no exponer como colección mutable
    public Company                    Company        { get; private set; } = null!;
    public ICollection<EmissionPoint> EmissionPoints { get; private set; } = [];

    private Establishment() { }

    public static Establishment Create(
        Guid    subscriberId,
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
            SubscriberId = subscriberId,
            CompanyId    = companyId,
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
