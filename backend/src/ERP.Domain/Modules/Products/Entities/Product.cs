using ERP.Domain.Common;
using ERP.Domain.Products.Enums;
using ERP.Domain.Products.ValueObjects;

namespace ERP.Domain.Products.Entities;

/// <summary>
/// Entidad maestra de productos.
/// Soporta: múltiples códigos de barras, categorización 3 niveles,
/// impuestos por catálogo, flags de canal y comportamiento.
/// REGLA: Nunca se elimina. Solo se deshabilita con Disable().
/// </summary>
public class Product : MasterEntity, ISubscriberScopedEntity, ICompanyOperationalEntity
{
    public Guid? CompanyId { get; private set; }
    private readonly List<ProductBarcode> _barcodes = new();
    private readonly List<ProductSupplierCode> _supplierCodes = new();
    private readonly List<ProductUnitConversion> _unitConversions = new();
    private readonly List<ProductColor> _colors = new();
    private readonly List<ProductSize> _sizes = new();
    private readonly List<ProductDimension> _dimensions = new();
    private readonly List<ProductImage> _images = new();
    private readonly List<ProductFeature> _features = new();
    private readonly List<ProductTariffDetail> _tariffDetails = new();
    private readonly List<ProductSubstitute> _substitutes = new();
    private readonly List<ProductCustomField> _customFields = new();

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

    // ── Impuestos (tarifas maestras) ──────────────────────────────
    public bool AppliesVatOnSale { get; private set; }
    public bool AppliesVatOnPurchase { get; private set; }
    public bool AppliesExciseTax { get; private set; }

    // ── Impuestos (catálogos separados) ──────────────────────────
    public Guid? SaleTaxId { get; private set; }                 // IVA venta (TaxRate)
    public Guid? PurchaseTaxId { get; private set; }             // IVA compra (TaxRate)
    public Guid? ExciseTaxId { get; private set; }               // ICE (TaxRate)

    // ── Cuentas contables por impuesto ───────────────────────────
    public Guid? SaleVatAccountId { get; private set; }
    public Guid? PurchaseVatAccountId { get; private set; }
    public Guid? ExciseAccountId { get; private set; }

    // ── Comportamiento de stock ───────────────────────────────────
    public bool IsService { get; private set; }                  // No maneja stock físico
    public bool TracksStock { get; private set; }                // Maneja stock
    public bool TracksLot { get; private set; }                  // Manejo por lote
    public bool TracksSeries { get; private set; }               // Manejo por serie
    public bool HasRecipe { get; private set; }                  // Tiene receta/composición
    public bool StockWithDecimal { get; private set; }           // Stock permite decimales
    public Guid? RecipeId { get; private set; }                  // Referencial (módulo Recipe aún no implementado)
    public bool SaleWithDecimal { get; private set; }            // Venta permite decimales
    public decimal MaxItemDiscountPercent { get; private set; }  // Máximo % de descuento por ítem

    // ── Canales de venta ─────────────────────────────────────────
    public bool AvailableOnWeb { get; private set; }
    public bool AvailableOnMobile { get; private set; }
    public bool IsEcommerceActive { get; private set; }
    public bool IsFavorite { get; private set; }
    public bool IsForSale { get; private set; }                  // Habilitado para venta

    // ── Variantes ────────────────────────────────────────────────
    public string? BaseColor { get; private set; }
    public bool HasMultipleColors { get; private set; }
    public bool HasSizes { get; private set; }

    // ── Aranceles / importación ──────────────────────────────────
    public bool HandlesTariff { get; private set; }

    // ── SRI ───────────────────────────────────────────────────────
    /// <summary>
    /// Tipo de servicio SRI para el XML del comprobante electrónico.
    /// Null / vacío = bien físico.
    /// Valores SRI: "01"=Servicios, "02"=Arrendamiento, "03"=Honorarios,
    /// "04"=Comisiones, "05"=Otros.
    /// </summary>
    public string? SriServiceCode { get; private set; }

    // ── Códigos de barras ─────────────────────────────────────────
    public IReadOnlyList<ProductBarcode> Barcodes => _barcodes.AsReadOnly();
    public IReadOnlyList<ProductSupplierCode> SupplierCodes => _supplierCodes.AsReadOnly();
    public IReadOnlyList<ProductUnitConversion> UnitConversions => _unitConversions.AsReadOnly();
    public IReadOnlyList<ProductColor> Colors => _colors.AsReadOnly();
    public IReadOnlyList<ProductSize> Sizes => _sizes.AsReadOnly();
    public IReadOnlyList<ProductDimension> Dimensions => _dimensions.AsReadOnly();
    public IReadOnlyList<ProductImage> Images => _images.AsReadOnly();
    public IReadOnlyList<ProductFeature> Features => _features.AsReadOnly();
    public IReadOnlyList<ProductTariffDetail> TariffDetails => _tariffDetails.AsReadOnly();
    public IReadOnlyList<ProductSubstitute> Substitutes => _substitutes.AsReadOnly();
    public IReadOnlyList<ProductCustomField> CustomFields => _customFields.AsReadOnly();

    private Product() { }

    public static Product Create(
        Guid subscriberId,
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
        bool appliesVatOnSale,
        Guid? saleTaxId,
        Guid? saleVatAccountId,
        bool appliesVatOnPurchase,
        Guid? purchaseTaxId,
        Guid? purchaseVatAccountId,
        Guid createdBy,
        string? purchaseCode = null,
        bool appliesExciseTax = false,
        Guid? exciseTaxId = null,
        Guid? exciseAccountId = null,
        bool isService = false,
        bool tracksStock = true,
        bool tracksLot = false,
        bool tracksSeries = false,
        bool hasRecipe = false,
        Guid? recipeId = null,
        bool stockWithDecimal = false,
        bool saleWithDecimal = false,
        decimal maxItemDiscountPercent = 0,
        bool availableOnWeb = false,
        bool availableOnMobile = false,
        bool isEcommerceActive = false,
        string? baseColor = null,
        bool hasMultipleColors = false,
        bool hasSizes = false,
        bool handlesTariff = false,
        bool isForSale = true,
        string? sriServiceCode = null,
        Guid? companyId = null)
    {
        var identity = ProductIdentity.Create(saleCode, shortName, description);

        if (maxItemDiscountPercent < 0 || maxItemDiscountPercent > 100)
            throw new ArgumentException("El descuento máximo por ítem debe estar entre 0 y 100.", nameof(maxItemDiscountPercent));

        if (appliesVatOnSale && saleTaxId is null)
            throw new ArgumentException("Debe seleccionar una tarifa de IVA para la venta.", nameof(saleTaxId));

        if (appliesVatOnPurchase && purchaseTaxId is null)
            throw new ArgumentException("Debe seleccionar una tarifa de IVA para la compra.", nameof(purchaseTaxId));

        if (appliesExciseTax && exciseTaxId is null)
            throw new ArgumentException("Debe seleccionar una tarifa de ICE.", nameof(exciseTaxId));

        var product = new Product
        {
            Id                = Guid.NewGuid(),
            SubscriberId          = subscriberId,
            CompanyId             = companyId,
            SaleCode          = identity.SaleCode,
            PurchaseCode      = purchaseCode,
            ShortName         = identity.ShortName,
            Description       = identity.Description,
            LineId            = lineId,
            CategoryId        = categoryId,
            SubcategoryId     = subcategoryId,
            UnitOfMeasureId   = unitOfMeasureId,
            BrandId           = brandId,
            ProductTypeId     = productTypeId,
            TariffId          = tariffId,
            AppliesVatOnSale      = appliesVatOnSale,
            SaleTaxId             = saleTaxId,
            SaleVatAccountId      = saleVatAccountId,
            AppliesVatOnPurchase  = appliesVatOnPurchase,
            PurchaseTaxId         = purchaseTaxId,
            PurchaseVatAccountId  = purchaseVatAccountId,
            AppliesExciseTax      = appliesExciseTax,
            ExciseTaxId       = exciseTaxId,
            ExciseAccountId   = exciseAccountId,
            IsService         = isService,
            TracksStock       = tracksStock,
            TracksLot         = tracksLot,
            TracksSeries      = tracksSeries,
            HasRecipe         = hasRecipe,
            RecipeId          = recipeId,
            StockWithDecimal  = stockWithDecimal,
            SaleWithDecimal   = saleWithDecimal,
            MaxItemDiscountPercent = maxItemDiscountPercent,
            AvailableOnWeb    = availableOnWeb,
            AvailableOnMobile = availableOnMobile,
            IsEcommerceActive = isEcommerceActive,
            BaseColor         = baseColor,
            HasMultipleColors = hasMultipleColors,
            HasSizes          = hasSizes,
            HandlesTariff     = handlesTariff,
            IsFavorite        = false,
            IsForSale         = isForSale,
            SriServiceCode    = sriServiceCode,
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
        bool appliesVatOnSale,
        Guid? saleTaxId,
        Guid? saleVatAccountId,
        bool appliesVatOnPurchase,
        Guid? purchaseTaxId,
        Guid? purchaseVatAccountId,
        bool appliesExciseTax,
        Guid? exciseTaxId,
        Guid? exciseAccountId,
        bool tracksStock,
        bool isService,
        bool tracksLot,
        bool tracksSeries,
        bool hasRecipe,
        Guid? recipeId,
        bool stockWithDecimal,
        bool saleWithDecimal,
        decimal maxItemDiscountPercent,
        string? baseColor,
        bool hasMultipleColors,
        bool hasSizes,
        bool handlesTariff,
        Guid updatedBy,
        string? sriServiceCode = null)
    {
        var identity = ProductIdentity.Create(SaleCode, shortName, description);

        if (maxItemDiscountPercent < 0 || maxItemDiscountPercent > 100)
            throw new ArgumentException("El descuento máximo por ítem debe estar entre 0 y 100.", nameof(maxItemDiscountPercent));

        if (appliesVatOnSale && saleTaxId is null)
            throw new ArgumentException("Debe seleccionar una tarifa de IVA para la venta.", nameof(saleTaxId));

        if (appliesVatOnPurchase && purchaseTaxId is null)
            throw new ArgumentException("Debe seleccionar una tarifa de IVA para la compra.", nameof(purchaseTaxId));

        if (appliesExciseTax && exciseTaxId is null)
            throw new ArgumentException("Debe seleccionar una tarifa de ICE.", nameof(exciseTaxId));

        ShortName       = identity.ShortName;
        Description     = identity.Description;
        Observations    = observations;
        LineId          = lineId;
        CategoryId      = categoryId;
        SubcategoryId   = subcategoryId;
        UnitOfMeasureId = unitOfMeasureId;
        BrandId         = brandId;
        ProductTypeId   = productTypeId;
        AppliesVatOnSale     = appliesVatOnSale;
        SaleTaxId            = saleTaxId;
        SaleVatAccountId     = saleVatAccountId;
        AppliesVatOnPurchase = appliesVatOnPurchase;
        PurchaseTaxId        = purchaseTaxId;
        PurchaseVatAccountId = purchaseVatAccountId;
        AppliesExciseTax     = appliesExciseTax;
        ExciseTaxId          = exciseTaxId;
        ExciseAccountId      = exciseAccountId;
        TracksStock          = tracksStock;
        IsService            = isService;
        TracksLot            = tracksLot;
        TracksSeries         = tracksSeries;
        HasRecipe            = hasRecipe;
        RecipeId             = recipeId;
        StockWithDecimal     = stockWithDecimal;
        SaleWithDecimal      = saleWithDecimal;
        MaxItemDiscountPercent = maxItemDiscountPercent;
        BaseColor            = baseColor;
        HasMultipleColors    = hasMultipleColors;
        HasSizes             = hasSizes;
        HandlesTariff        = handlesTariff;
        SriServiceCode       = sriServiceCode;
        SetUpdated(updatedBy);
    }

    public void UpdateChannels(
        bool availableOnWeb,
        bool availableOnMobile,
        bool isEcommerceActive,
        bool isForSale,
        Guid updatedBy)
    {
        AvailableOnWeb    = availableOnWeb;
        AvailableOnMobile = availableOnMobile;
        IsEcommerceActive = isEcommerceActive;
        IsForSale         = isForSale;
        SetUpdated(updatedBy);
    }

    public void ToggleFavorite(Guid updatedBy)
    {
        IsFavorite = !IsFavorite;
        SetUpdated(updatedBy);
    }

    public void UpdateTariff(Guid tariffId, Guid updatedBy)
    {
        TariffId = tariffId;
        SetUpdated(updatedBy);
    }

    // ── Códigos de barras ─────────────────────────────────────────

    public void AddBarcode(string code, BarcodeType type, Guid updatedBy)
    {
        if (_barcodes.Any(b => b.Code == code))
            throw new InvalidOperationException($"El código de barras '{code}' ya existe.");

        _barcodes.Add(ProductBarcode.Create(Id, SubscriberId, code, type));
        SetUpdated(updatedBy);
    }

    public void RemoveBarcode(Guid barcodeId, Guid updatedBy)
    {
        var barcode = _barcodes.FirstOrDefault(b => b.Id == barcodeId)
            ?? throw new InvalidOperationException("Código de barras no encontrado.");

        _barcodes.Remove(barcode);
        SetUpdated(updatedBy);
    }

    // ── Colecciones (reemplazo completo) ──────────────────────────

    public void ReplaceSupplierCodes(
        IEnumerable<(Guid SupplierId, string Code, bool IsDefault)> supplierCodes,
        Guid updatedBy)
    {
        _supplierCodes.Clear();
        foreach (var s in supplierCodes)
            _supplierCodes.Add(ProductSupplierCode.Create(Id, SubscriberId, s.SupplierId, s.Code, s.IsDefault));
        SetUpdated(updatedBy);
    }

    public void ReplaceUnitConversions(
        IEnumerable<(Guid AlternateUnitId, decimal ConversionFactor)> conversions,
        Guid updatedBy)
    {
        _unitConversions.Clear();
        foreach (var c in conversions)
            _unitConversions.Add(ProductUnitConversion.Create(Id, SubscriberId, c.AlternateUnitId, c.ConversionFactor));
        SetUpdated(updatedBy);
    }

    public void ReplaceColors(IEnumerable<(string Name, string? HexCode)> colors, Guid updatedBy)
    {
        _colors.Clear();
        foreach (var c in colors)
            _colors.Add(ProductColor.Create(Id, SubscriberId, c.Name, c.HexCode));
        SetUpdated(updatedBy);
    }

    public void ReplaceSizes(IEnumerable<(string Name, int SortOrder)> sizes, Guid updatedBy)
    {
        _sizes.Clear();
        foreach (var s in sizes)
            _sizes.Add(ProductSize.Create(Id, SubscriberId, s.Name, s.SortOrder));
        SetUpdated(updatedBy);
    }

    public void ReplaceDimensions(IEnumerable<(string Name, string Value, string Unit)> dimensions, Guid updatedBy)
    {
        _dimensions.Clear();
        foreach (var d in dimensions)
            _dimensions.Add(ProductDimension.Create(Id, SubscriberId, d.Name, d.Value, d.Unit));
        SetUpdated(updatedBy);
    }

    public void ReplaceFeatures(IEnumerable<(string Name, string Value)> features, Guid updatedBy)
    {
        _features.Clear();
        foreach (var f in features)
            _features.Add(ProductFeature.Create(Id, SubscriberId, f.Name, f.Value));
        SetUpdated(updatedBy);
    }

    public void ReplaceTariffDetails(
        IEnumerable<(string OriginCountry, string TariffCode, decimal Percentage)> tariffDetails,
        Guid updatedBy)
    {
        _tariffDetails.Clear();
        foreach (var t in tariffDetails)
            _tariffDetails.Add(ProductTariffDetail.Create(Id, SubscriberId, t.OriginCountry, t.TariffCode, t.Percentage));
        SetUpdated(updatedBy);
    }

    public void ReplaceSubstitutes(
        IEnumerable<(Guid SubstituteProductId, string? Note)> substitutes,
        Guid updatedBy)
    {
        _substitutes.Clear();
        foreach (var s in substitutes)
            _substitutes.Add(ProductSubstitute.Create(Id, SubscriberId, s.SubstituteProductId, s.Note));
        SetUpdated(updatedBy);
    }

    public void ReplaceCustomFields(
        IEnumerable<(string FieldName, CustomFieldType FieldType, string FieldValue)> customFields,
        Guid updatedBy)
    {
        _customFields.Clear();
        foreach (var c in customFields)
            _customFields.Add(ProductCustomField.Create(Id, SubscriberId, c.FieldName, c.FieldType, c.FieldValue));
        SetUpdated(updatedBy);
    }

    public void ReplaceImages(
        IEnumerable<(string Url, string? AltText, bool IsMain, bool IsEcommerce, int SortOrder)> images,
        Guid updatedBy)
    {
        var imageList = images.ToList();
        var mainCount = imageList.Count(i => i.IsMain);
        if (mainCount > 1)
            throw new InvalidOperationException("Solo una imagen puede estar marcada como principal (IsMain).");

        var ecommerceCount = imageList.Count(i => i.IsEcommerce);
        if (ecommerceCount > 1)
            throw new InvalidOperationException("Solo una imagen puede estar marcada para eCommerce (IsEcommerce).");

        _images.Clear();
        foreach (var i in imageList.OrderBy(x => x.SortOrder))
            _images.Add(ProductImage.Create(Id, SubscriberId, i.Url, i.AltText, i.IsMain, i.IsEcommerce, i.SortOrder));
        SetUpdated(updatedBy);
    }
}
