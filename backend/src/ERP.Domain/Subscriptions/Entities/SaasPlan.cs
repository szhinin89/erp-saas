namespace ERP.Domain.Subscriptions.Entities;

/// <summary>Plan comercial global (catálogo).</summary>
public sealed class SaasPlan
{
    public const int CodeMaxLen = 64;
    public const int NameMaxLen = 200;

    public Guid Id { get; private set; }
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public bool IsActive { get; private set; }

    private SaasPlan() { }

    public static SaasPlan Create(string code, string name, bool isActive = true)
    {
        var c = (code ?? string.Empty).Trim().ToLowerInvariant();
        if (c.Length == 0 || c.Length > CodeMaxLen)
            throw new ArgumentException("Código de plan inválido.", nameof(code));

        return new SaasPlan
        {
            Id = Guid.NewGuid(),
            Code = c,
            Name = (name ?? string.Empty).Trim(),
            IsActive = isActive,
        };
    }
}
