namespace ERP.Application.Modules.Caja.DTOs;

/// <param name="Id"></param>
/// <param name="CompanyId"></param>
/// <param name="BranchId"></param>
/// <param name="UserId"></param>
/// <param name="CashRegisterId"></param>
/// <param name="CashRegisterCodeSnapshot"></param>
/// <param name="CashRegisterNameSnapshot"></param>
/// <param name="EmissionPointId"></param>
/// <param name="EmissionPointCodeSnapshot"></param>/// <param name="EmissionType">
/// Resuelto en vivo desde <c>EmissionPoint.EmissionType</c> (nunca un snapshot) usando el
/// <c>EmissionPointId</c> ya fijado en la sesión — null si el punto de emisión ya no existe/está
/// activo, nunca un valor inventado por defecto.
/// </param>
/// <param name="DefaultWarehouseId"></param>
/// <param name="DefaultWarehouseName"></param>
/// <param name="DefaultCustomerId"></param>
/// <param name="DefaultCustomerName"></param>
/// <param name="OpenedAt"></param>
/// <param name="OpeningAmount"></param>
/// <param name="Status"></param>
/// <param name="Notes"></param>
/// <param name="ClosedAt"></param>
/// <param name="ClosedBy"></param>
/// <param name="CloseNotes"></param>
/// <param name="ExpectedAmount"></param>
/// <param name="CountedAmount"></param>
/// <param name="Difference"></param>
/// <param name="TotalIncome"></param>
/// <param name="TotalExpense"></param>
/// <param name="CurrentBalance"></param>
/// <param name="Movements"></param>
/// <param name="ClosingCounts"></param>
/// <param name="CreatedAt"></param>
/// <param name="UpdatedAt"></param>
public sealed record CashSessionDto(
    Guid Id,
    Guid CompanyId,
    Guid BranchId,
    Guid UserId,
    Guid CashRegisterId,
    string CashRegisterCodeSnapshot,
    string CashRegisterNameSnapshot,
    Guid EmissionPointId,
    string EmissionPointCodeSnapshot,
    string? EmissionType,
    Guid? DefaultWarehouseId,
    string? DefaultWarehouseName,
    Guid? DefaultCustomerId,
    string? DefaultCustomerName,
    DateTime OpenedAt,
    decimal OpeningAmount,
    string Status,
    string? Notes,
    DateTime? ClosedAt,
    Guid? ClosedBy,
    string? CloseNotes,
    decimal? ExpectedAmount,
    decimal? CountedAmount,
    decimal? Difference,
    decimal TotalIncome,
    decimal TotalExpense,
    decimal CurrentBalance,
    IReadOnlyList<CashMovementDto> Movements,
    IReadOnlyList<CashClosingCountDto> ClosingCounts,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public sealed record CashMovementDto(
    Guid Id,
    string MovementType,
    decimal Amount,
    string Description,
    DateTime CreatedAt,
    Guid CreatedBy,
    string ReferenceType,
    Guid? ReferenceId,
    string? ReferenceNumber
);

public sealed record CashClosingCountDto(
    Guid Id,
    decimal DenominationValue,
    string DenominationLabel,
    int Quantity,
    decimal Total
);

/// <summary>
/// CASH-SESSION-LIST-SUMMARY-01 — fila del listado de turnos con información operativa y
/// financiera. SSOT sin cambios: <see cref="CurrentBalance"/>/<see cref="ExpectedCash"/>/
/// <see cref="CountedAmount"/>/<see cref="Difference"/> vienen de CashSession/CashMovement
/// (efectivo físico, igual que antes); <see cref="InvoiceCount"/>/<see cref="TotalInvoiced"/>/
/// <see cref="ByPaymentMethod"/> vienen de SalesInvoice+SalesInvoicePayment+PaymentMethod
/// (informativo, calculado en vivo, nunca persistido) — ver <see cref="CashSessionCollectionSummaryDto"/>.
/// </summary>
/// <param name="UserName">Cajero que abrió el turno — resuelto en batch (IAccessRepository), null si el usuario ya no existe.</param>
/// <param name="ClosedByName">Quien cerró el turno — null si sigue abierto o el usuario ya no existe.</param>
/// <param name="ExpectedCash">
/// Efectivo físico esperado ahora mismo: <see cref="CurrentBalance"/> si el turno sigue abierto,
/// o el <c>ExpectedAmount</c> congelado al momento del cierre si ya cerró (mismo valor,
/// distinto momento de lectura — nunca dos cálculos).
/// </param>
/// <param name="CountedAmount">Solo turnos cerrados — monto físico contado en el arqueo.</param>
/// <param name="InvoiceCount">Facturas AUTORIZADAS del turno (Draft/Cancelled excluidas).</param>
/// <param name="TotalInvoiced">Suma de GrandTotal de esas facturas (una vez por factura).</param>
/// <param name="SaleIncomeCash">Solo ventas en Efectivo que movieron el cajón físico (CashMovementType.SaleIncome) — subconjunto de TotalInvoiced/TotalCollected, no un monto adicional.</param>
/// <param name="ManualIncomeCash">Ingresos manuales de caja (CashMovementType.ManualIncome).</param>
/// <param name="ManualExpenseCash">Egresos manuales de caja (ManualExpense + Withdrawal) — no incluye SaleRefund (reverso automático de una devolución, no una acción manual).</param>
/// <param name="ByPaymentMethod">Desglose informativo por forma — mismo criterio que <see cref="CashSessionCollectionByMethodDto"/> pero sin el detalle por factura (ver el detalle del turno para eso).</param>
public sealed record CashSessionListDto(
    Guid Id,
    Guid UserId,
    string? UserName,
    Guid CashRegisterId,
    string CashRegisterCodeSnapshot,
    string CashRegisterNameSnapshot,
    Guid EmissionPointId,
    string EmissionPointCodeSnapshot,
    DateTime OpenedAt,
    decimal OpeningAmount,
    string Status,
    decimal CurrentBalance,
    decimal ExpectedCash,
    decimal? CountedAmount,
    int MovementCount,
    DateTime? ClosedAt,
    Guid? ClosedBy,
    string? ClosedByName,
    decimal? Difference,
    int InvoiceCount,
    decimal TotalInvoiced,
    decimal SaleIncomeCash,
    decimal ManualIncomeCash,
    decimal ManualExpenseCash,
    IReadOnlyList<CashSessionListCollectionByMethodDto> ByPaymentMethod,
    DateTime CreatedAt
);

/// <param name="PaymentMethodId">SSOT del agrupamiento — nunca se agrupa por nombre.</param>
/// <param name="PaymentMethodCode"></param>
/// <param name="PaymentMethodName"></param>
/// <param name="IsCreditAllowed"></param>
/// <param name="InvoiceCount">Facturas distintas que usaron esta forma en el turno.</param>
/// <param name="Amount">Suma de los montos aplicados con esta forma en el turno.</param>
public sealed record CashSessionListCollectionByMethodDto(
    Guid PaymentMethodId,
    string PaymentMethodCode,
    string PaymentMethodName,
    bool IsCreditAllowed,
    int InvoiceCount,
    decimal Amount
);

public sealed record CashSessionListResponse(
    IReadOnlyList<CashSessionListDto> Items,
    int Total,
    int Page,
    int PageSize
);

/// <summary>
/// DTO único de Caja — alimenta a la vez el selector "Abrir Caja" (con tarjeta resumen
/// Sucursal/Establecimiento/Punto de Emisión) y la administración de Cajas, evitando
/// requests adicionales y endpoints duplicados.
/// </summary>
/// <param name="Id"></param>
/// <param name="BranchId"></param>
/// <param name="BranchName"></param>
/// <param name="BranchCode"></param>
/// <param name="EmissionPointId"></param>
/// <param name="EstablishmentCode"></param>
/// <param name="EmissionPointCode"></param>
/// <param name="EmissionPointName"></param>
/// <param name="Code"></param>
/// <param name="Name"></param>
/// <param name="Notes"></param>
/// <param name="IsActive"></param>
/// <param name="HasHistory">
/// true si la Caja ya tiene historial operativo (ver <c>ICashRegisterUsageGuard</c>) — único
/// indicador que el frontend debe usar para bloquear Código/Sucursal/Punto de Emisión en el
/// formulario de edición. Calculado siempre server-side, nunca inferido en el cliente.
/// </param>
/// <param name="DefaultWarehouseId"></param>
/// <param name="DefaultWarehouseCode"></param>
/// <param name="DefaultWarehouseName"></param>
/// <param name="DefaultCustomerId"></param>
/// <param name="DefaultCustomerName"></param>
/// <param name="CreatedAt"></param>
/// <param name="UpdatedAt"></param>
public sealed record CashRegisterDto(
    Guid Id,
    Guid BranchId,
    string BranchName,
    string? BranchCode,
    Guid? EmissionPointId,
    string? EstablishmentCode,
    string? EmissionPointCode,
    string? EmissionPointName,
    string Code,
    string Name,
    string? Notes,
    bool IsActive,
    bool HasHistory,
    Guid? DefaultWarehouseId,
    string? DefaultWarehouseCode,
    string? DefaultWarehouseName,
    Guid? DefaultCustomerId,
    string? DefaultCustomerName,
    Guid? AccountingAccountId,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public sealed record EmissionPointLookupForBranchDto(
    Guid Id,
    string Code,
    string? Name,
    string EstablishmentCode
);

// ── CASH-SESSION-COLLECTION-SUMMARY-01 / UX-02 ───────────────────────────
// SSOT: Efectivo físico vive exclusivamente en CashSession/CashMovement (TotalIncome/
// CurrentBalance arriba, sin cambios). Este resumen es informativo — se calcula en vivo desde
// SalesInvoice+SalesInvoicePayment+PaymentMethod, nunca se persiste ni duplica el estado de Caja.
// "Cobros del turno" (cuánto se facturó/cobró y en qué forma) es un concepto distinto de
// "efectivo físico de caja" (lo único que afecta apertura/cierre/arqueo). UX-02 solo agrega
// profundidad informativa (código, operaciones, destino, detalle de transferencia) — ningún campo
// nuevo participa en TotalCollected/TotalCredit/PhysicalCashApplied ni en el posting contable.

/// <param name="InvoiceCount">Facturas AUTORIZADAS del turno (Draft/Cancelled excluidas). Una factura con varias formas de pago cuenta una sola vez.</param>
/// <param name="TotalInvoiced">Suma de GrandTotal de esas facturas (una vez por factura, no por forma de pago).</param>
/// <param name="TotalCollected">Suma de montos cobrados con formas de pago NO marcadas IsCreditAllowed — dinero real recibido en cualquier forma (incluye no-efectivo), no confundir con efectivo físico.</param>
/// <param name="TotalCredit">Suma de montos registrados con forma de pago IsCreditAllowed (venta a crédito) — informativo, nunca afecta CashSession.</param>
/// <param name="ByPaymentMethod">Desglose por forma de pago, incluyendo Crédito (informativo).</param>
public sealed record CashSessionCollectionSummaryDto(
    int InvoiceCount,
    decimal TotalInvoiced,
    decimal TotalCollected,
    decimal TotalCredit,
    IReadOnlyList<CashSessionCollectionByMethodDto> ByPaymentMethod
);

/// <param name="PaymentMethodId">SSOT del agrupamiento — nunca se agrupa por nombre (dos PaymentMethodId distintos con el mismo nombre, p. ej. uno huérfano/legacy, deben verse como dos filas separadas).</param>
/// <param name="PaymentMethodCode"></param>
/// <param name="PaymentMethodName"></param>
/// <param name="IsCreditAllowed">Informativo — permite al frontend distinguir Crédito del resto sin repetir la regla.</param>
/// <param name="InvoiceCount">Facturas distintas que usaron esta forma (una factura con 2 pagos de la misma forma cuenta 1 vez).</param>
/// <param name="OperationCount">Líneas de pago (operaciones) con esta forma — puede superar InvoiceCount cuando una factura tiene 2 pagos con la misma forma.</param>
/// <param name="Amount">Suma de los montos aplicados con esta forma — nunca el total de la factura si la venta fue mixta.</param>
/// <param name="Destination">
/// CASH-SESSION-COLLECTION-SUMMARY-UX-02 — etiqueta contable genérica derivada de
/// <c>PaymentMethod.DetailType</c>/<c>IsCreditAllowed</c>/<c>AffectsPhysicalCash</c> (ya cargados,
/// sin queries adicionales): "Caja física" (Efectivo), "Cuenta bancaria" (Transferencia — la
/// cuenta específica de cada operación va en el detalle), "Cuenta configurada" (Tarjeta/Cheque),
/// "Cuentas por Cobrar" (Crédito). Nunca inventa una cuenta contable real — es solo la categoría
/// de destino, la misma usada por <c>PaymentMethodAccountSource</c> en Sales.
/// </param>
/// <param name="Details"></param>
public sealed record CashSessionCollectionByMethodDto(
    Guid PaymentMethodId,
    string PaymentMethodCode,
    string PaymentMethodName,
    bool IsCreditAllowed,
    int InvoiceCount,
    int OperationCount,
    decimal Amount,
    string Destination,
    IReadOnlyList<CashSessionCollectionDetailDto> Details
);

/// <param name="InvoiceId"></param>
/// <param name="InvoiceNumber"></param>
/// <param name="AuthorizedAt"></param>
/// <param name="CustomerName"></param>
/// <param name="InvoiceTotal">GrandTotal de la factura — para contrastar visualmente contra <see cref="Amount"/> cuando la venta fue mixta.</param>
/// <param name="Amount">Monto de ESTA forma de pago en ESTA factura — no el GrandTotal de la factura si hubo venta mixta.</param>
/// <param name="IsMixedPayment">true si la factura tiene más de una línea de pago (de cualquier forma) — "Completa" vs "Mixta" en la UI.</param>
/// <param name="Reference">Referencia/comprobante genérico del pago (<c>SalesInvoicePayment.Reference</c>) — usado cuando el pago no es Transferencia (que tiene su propio comprobante, ver <see cref="TransferReceiptNumber"/>).</param>
/// <param name="DestinationBankName">Solo Transferencia — nombre real del banco (resuelto desde <c>CompanyBankAccount</c>/catálogo de bancos, o el texto legacy si la operación es anterior a SALES-TRANSFER-BANK-ACCOUNT-01). Null en cualquier otra forma.</param>
/// <param name="DestinationAccountMasked">Solo Transferencia — alias + número de cuenta enmascarado (últimos 4 dígitos). Null si no hay cuenta bancaria configurada resuelta.</param>
/// <param name="TransferReceiptNumber">Solo Transferencia — comprobante de la operación bancaria.</param>
/// <param name="TransferDate">Solo Transferencia — fecha de la operación bancaria.</param>
public sealed record CashSessionCollectionDetailDto(
    Guid InvoiceId,
    string InvoiceNumber,
    DateTime AuthorizedAt,
    string CustomerName,
    decimal InvoiceTotal,
    decimal Amount,
    bool IsMixedPayment,
    string? Reference,
    string? DestinationBankName,
    string? DestinationAccountMasked,
    string? TransferReceiptNumber,
    DateOnly? TransferDate
);
