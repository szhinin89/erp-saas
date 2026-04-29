using ERP.Domain.Common;

namespace ERP.Domain.Products.Entities;

/// <summary>
/// Entidad maestra de productos.
/// Soporta: múltiples códigos de barras, categorización 3 niveles,
/// impuestos por catálogo, flags de canal y comportamiento.
/// REGLA: Nunca se elimina. Solo se deshabilita con Disable().
/// </summary>
public class Product : MasterEntity
{
    private readonly List<ProductBarcode> _barcodes = new();

    // ── Identificación ────────────────────────────────────────────
    public string SaleCode { get; private set; } = null!;        // Código único de venta
    public string? PurchaseCode { get; private set; }            // Código principal de compra
    public string ShortName { get; private set; } = null!;       // Abreviado
    public string Description { get; private set; } = null!;     // Descripción completa
    public string? Observations { get; private set; }

    // ── Categorización (3 niveles) ────────────────────────────────
    public Guid LineId { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid SubcategoryId { get; private set; }

    // ── Catálogos relacionados ────────────────────────────────────
    public Guid UnitOfMeasureId { get; private set; }
    public Guid BrandId { get; private set; }
    public Guid ProductTypeId { get; private set; }
    public Guid TariffId { get; private set; }                   // Arancel

    // ── Impuestos (catálogos separados) ──────────────────────────
    public Guid SaleTaxId { get; private set; }                  // IVA venta
    public Guid PurchaseTaxId { get; private set; }              // IVA compra
    public Guid? ExciseTaxId { get; private set; }               // ICE (opcional)

    // ── Comportamiento de stock ───────────────────────────────────
    public bool IsService { get; private set; }                  // No maneja stock físico
    public bool TracksLot { get; private set; }                  // Manejo por lote
    public bool TracksSeries { get; private set; }               // Manejo por serie
    public bool HasRecipe { get; private set; }                  // Tiene receta/composición
    public bool StockWithDecimal { get; private set; }           // Stock permite decimales

    // ── Canales de venta ─────────────────────────────────────────
    public bool AvailableOnWeb { get; private set; }
    public bool AvailableOnMobile { get; private set; }
    public bool IsFavorite { get; private set; }
    public bool IsForSale { get; private set; }                  // Habilitado para venta

    // ── Códigos de barras ─────────────────────────────────────────
    public IReadOnlyList<ProductBarcode> Barcodes => _barcodes.AsReadOnly();

    private Product() { }

    public static Product Create(
        Guid tenantId,
        string saleCode,
        string shortName,
        string description,
        Guid lineId,
        Guid categoryId,
        Guid subcategoryId,
        Guid unitOfMeasureId,
        Guid brandId,
        Guid productTypeId,
        Guid tariffId,
        Guid saleTaxId,
        Guid purchaseTaxId,
        Guid createdBy,
        string? purchaseCode = null,
        Guid? exciseTaxId = null,
        bool isService = false,
        bool tracksLot = false,
        bool tracksSeries = false,
        bool hasRecipe = false,
        bool stockWithDecimal = false,
        bool availableOnWeb = false,
        bool availableOnMobile = false,
        bool isForSale = true)
    {
        var product = new Product
        {
            Id                = Guid.NewGuid(),
            TenantId          = tenantId,
            SaleCode          = saleCode,
            PurchaseCode      = purchaseCode,
            ShortName         = shortName,
            Description       = description,
            LineId            = lineId,
            CategoryId        = categoryId,
            SubcategoryId     = subcategoryId,
            UnitOfMeasureId   = unitOfMeasureId,
            BrandId           = brandId,
            ProductTypeId     = productTypeId,
            TariffId          = tariffId,
            SaleTaxId         = saleTaxId,
            PurchaseTaxId     = purchaseTaxId,
            ExciseTaxId       = exciseTaxId,
            IsService         = isService,
            TracksLot         = tracksLot,
            TracksSeries      = tracksSeries,
            HasRecipe         = hasRecipe,
            StockWithDecimal  = stockWithDecimal,
            AvailableOnWeb    = availableOnWeb,
            AvailableOnMobile = availableOnMobile,
            IsFavorite        = false,
            IsForSale         = isForSale,
        };

        product.SetCreated(createdBy);
        return product;
    }

    // ── Modificación ──────────────────────────────────────────────

    public void Update(
        string shortName,
        string description,
        string? observations,
        Guid lineId,
        Guid categoryId,
        Guid subcategoryId,
        Guid unitOfMeasureId,
        Guid brandId,
        Guid productTypeId,
        Guid saleTaxId,
        Guid purchaseTaxId,
        Guid? exciseTaxId,
        Guid updatedBy)
    {
        ShortName       = shortName;
        Description     = description;
        Observations    = observations;
        LineId          = lineId;
        CategoryId      = categoryId;
        SubcategoryId   = subcategoryId;
        UnitOfMeasureId = unitOfMeasureId;
        BrandId         = brandId;
        ProductTypeId   = productTypeId;
        SaleTaxId       = saleTaxId;
        PurchaseTaxId   = purchaseTaxId;
        ExciseTaxId     = exciseTaxId;
        SetUpdated(updatedBy);
    }

    public void UpdateChannels(
        bool availableOnWeb,
        bool availableOnMobile,
        bool isForSale,
        Guid updatedBy)
    {
        AvailableOnWeb    = availableOnWeb;
        AvailableOnMobile = availableOnMobile;
        IsForSale         = isForSale;
        SetUpdated(updatedBy);
    }

    public void ToggleFavorite(Guid updatedBy)
    {
        IsFavorite = !IsFavorite;
        SetUpdated(updatedBy);
    }

    // ── Códigos de barras ─────────────────────────────────────────

    public void AddBarcode(string code, BarcodeType type, Guid updatedBy)
    {
        if (_barcodes.Any(b => b.Code == code))
            throw new InvalidOperationException($"El código de barras '{code}' ya existe.");

        _barcodes.Add(ProductBarcode.Create(Id, TenantId, code, type));
        SetUpdated(updatedBy);
    }

    public void RemoveBarcode(Guid barcodeId, Guid updatedBy)
    {
        var barcode = _barcodes.FirstOrDefault(b => b.Id == barcodeId)
            ?? throw new InvalidOperationException("Código de barras no encontrado.");

        _barcodes.Remove(barcode);
        SetUpdated(updatedBy);
    }
}
