using ERP.Domain.Common;

namespace ERP.Domain.Products.Entities;

/// <summary>Imagen asociada a un producto. Almacena la URL/ruta y metadatos de control.</summary>
public class ProductImage : BaseEntity
{
    private static readonly HashSet<string> _allowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    public Guid   ProductId    { get; private set; }
    public string Url          { get; private set; } = null!;
    public string? AltText     { get; private set; }
    public bool   IsMain       { get; private set; }
    public bool   IsEcommerce  { get; private set; }
    public int    SortOrder    { get; private set; }
    public bool   IsActive     { get; private set; } = true;

    private ProductImage() { }

    internal static ProductImage Create(
        Guid productId, Guid subscriberId, string url, string? altText,
        bool isMain, bool isEcommerce, int sortOrder)
    {
        var ext = Path.GetExtension(url).ToLowerInvariant();
        if (!_allowedExtensions.Contains(ext))
            throw new ArgumentException($"Formato de imagen no permitido. Use: {string.Join(", ", _allowedExtensions)}");

        return new()
        {
            Id           = Guid.NewGuid(),
            SubscriberId     = subscriberId,
            ProductId    = productId,
            Url          = url,
            AltText      = altText,
            IsMain       = isMain,
            IsEcommerce  = isEcommerce,
            SortOrder    = sortOrder,
            IsActive     = true,
        };
    }

    public void SetAsMain()    => IsMain      = true;
    public void UnsetMain()    => IsMain      = false;
    public void SetEcommerce() => IsEcommerce = true;

    public void Disable()
    {
        if (!IsActive)
            throw new InvalidOperationException("El registro ya está deshabilitado.");
        IsActive = false;
        IsMain = false;
        IsEcommerce = false;
    }

    public void Enable()
    {
        if (IsActive)
            throw new InvalidOperationException("El registro ya está activo.");
        IsActive = true;
    }
}
