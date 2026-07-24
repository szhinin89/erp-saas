namespace ERP.Application.Modules.Inventory.Stock.DTOs;

/// <summary>
/// Información comercial/fiscal del documento origen de un movimiento de Kardex,
/// compuesta en Application desde el módulo dueño del documento (Purchases/Sales/
/// Inventory) — nunca duplicada ni persistida en StockMovement.
/// </summary>
public sealed record KardexSourceDocumentDto(
    string   DocType,
    string?  DocNumber,
    string?  PartnerName,
    decimal? UnitPrice,
    decimal? DiscountPct,
    string?  VatCode,
    decimal? VatRate,
    string?  Reason,
    string?  Notes);

public sealed record KardexActorDto(Guid UserId, string UserName);

/// <summary>
/// Un eslabón de la cadena documental de un movimiento. Hoy solo existe la relación
/// Documento↔Movimiento ("SourceDocument"); el arreglo queda preparado para futuros
/// eslabones (orden de compra, nota de crédito, asiento contable...) sin cambiar de forma.
/// </summary>
public sealed record KardexDocumentChainLinkDto(
    string  RelationType,
    string  DocType,
    string? DocNumber,
    Guid?   DocId,
    bool    IsCurrent);

public sealed record KardexDocumentChainDto(
    IReadOnlyList<KardexDocumentChainLinkDto> Links);

/// <summary>
/// Referencia liviana a un movimiento relacionado (vecino en la secuencia del Kardex
/// para la misma clave Producto+Bodega) — sin volver a traer el detalle completo.
/// </summary>
public sealed record KardexRelatedMovementRef(
    Guid MovementId, long SequenceNumber, string MovementTypeName, DateOnly EffectiveDate);

/// <summary>
/// Contexto de navegación agrupado: el movimiento actual + sus vecinos anterior/siguiente
/// en la secuencia del Kardex. Autocontenido para un widget de línea de tiempo.
/// </summary>
public sealed record KardexMovementRelationsDto(
    KardexRelatedMovementRef Current,
    KardexRelatedMovementRef? Previous,
    KardexRelatedMovementRef? Next);

public sealed record KardexMovementDetailDto(
    StockMovementDto Movement,
    KardexSourceDocumentDto? SourceDocument,
    KardexActorDto Actor,
    KardexDocumentChainDto DocumentChain,
    KardexMovementRelationsDto Relations);
