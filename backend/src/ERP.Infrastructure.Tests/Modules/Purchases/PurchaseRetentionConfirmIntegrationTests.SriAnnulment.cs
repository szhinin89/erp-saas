using ERP.Application.Common;
using ERP.Application.Common.Interfaces.SRI;
using ERP.Application.Modules.Purchases.DTOs;
using ERP.Application.Modules.Purchases.Services;
using ERP.Application.Modules.Purchases.UseCases;
using ERP.Application.Modules.Retentions.Services;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Exceptions;
using ERP.Domain.Modules.Purchases.Enums;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.Purchases;
using ERP.Infrastructure.Tests.TestData;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Tests.Modules.Purchases;

/// <summary>
/// ZH-RETENTION-SRI-ANNULMENT-01/01B (ADR-036 D-6…D-8, §24) — anulación ante el SRI de la retención
/// AUTORIZADA de una Compra, contra PostgreSQL real: solicitud desde la anulación de la compra (sin
/// reversos), CxP retenida, presentación asistida y VERIFICACIÓN automática en ConsultaComprobante (Ficha
/// Técnica v2.34 §8): solo un ANULADO informado por el SRI finaliza la anulación de la compra, por su flujo
/// oficial y exactamente una vez. Tests 1–8 del ticket 01B y las carreras A–H de 01. La consulta SRI es un
/// doble tipado (<see cref="SriStatusQueryDouble"/>) o el SriSoapClient REAL sobre HTTP simulado.
/// </summary>
public sealed partial class PurchaseRetentionConfirmIntegrationTests
{
    private const string AnnulmentReason = "El proveedor emitió la factura con datos erróneos";

    private readonly SriStatusQueryDouble _sriStatus = new();

    private async Task<(Guid InvoiceId, Guid RetentionId)> ConfirmWithAuthorizedRetentionAsync()
    {
        var sri = new SriBoundaryDouble(_companyId);
        var result = await ConfirmWithIssuedRetentionAsync(sri);
        (await ReadElectronicAsync(result.RetentionId))!.CurrentState.Should().Be(ElectronicDocumentState.Authorized);
        return result;
    }

    private async Task<Result<PurchaseInvoiceDto>> RequestSriAnnulmentAsync(Guid invoiceId)
    {
        await using var db = CreateWiredContext();
        return await CancelHandler(db)
            .Handle(new CancelPurchaseCommand(invoiceId, AnnulmentReason, RequestSriAnnulment: true), CancellationToken.None);
    }

    private IRetentionAnnulmentService Annulments(ErpDbContext db, ISriDocumentStatusQuery? sriStatus = null)
    {
        var company = new FixedCurrentCompany(_companyId);
        return RetentionElectronicTestWiring.AnnulmentService(
            db,
            company,
            sriStatus ?? _sriStatus,
            new PurchaseRetentionOriginCancellation(CancelHandler(db), new PurchaseInvoiceRepository(db, company))
        );
    }

    private async Task<RetentionAnnulmentRequest?> ReadAnnulmentAsync(Guid retentionId)
    {
        await using var db = CreateContext();
        return await db.RetentionAnnulmentRequests.AsNoTracking()
            .Where(r => r.RetentionDocumentId == retentionId)
            .OrderByDescending(r => r.RequestedAtUtc)
            .FirstOrDefaultAsync();
    }

    private async Task<Result<RetentionAnnulmentRequest>> SubmitAnnulmentAsync(Guid requestId)
    {
        await using var db = CreateWiredContext();
        return await Annulments(db).MarkSubmittedAsync(
            _tenantId, _companyId, requestId, new DateOnly(2026, 9, 20), "TRAMITE-SRI-1", null, _userId);
    }

    /// <summary>Verificación en ConsultaComprobante (la misma que dispara la UI y el job).</summary>
    private async Task<Result<RetentionAnnulmentRequest>> VerifyAsync(
        Guid requestId,
        ISriDocumentStatusQuery? sriStatus = null,
        Guid? userId = null
    )
    {
        await using var db = CreateWiredContext();
        return await Annulments(db, sriStatus).VerifyWithSriAsync(_tenantId, _companyId, requestId, userId ?? _userId);
    }

    private async Task<Result<RetentionAnnulmentRequest>> VerifyAnnulledAsync(Guid requestId)
    {
        _sriStatus.FiscalStatus = SriFiscalStatus.Annulled;
        return await VerifyAsync(requestId);
    }

    private async Task<RetentionAnnulmentRequest> RequestAndSubmitAsync(Guid invoiceId, Guid retentionId)
    {
        (await RequestSriAnnulmentAsync(invoiceId)).IsSuccess.Should().BeTrue();
        var request = (await ReadAnnulmentAsync(retentionId))!;
        (await SubmitAnnulmentAsync(request.Id)).IsSuccess.Should().BeTrue();
        return request;
    }

    /// <summary>Escritor de pagos sobre el agregado real (mismo guard y mismo <c>xmin</c> que SupplierPaymentRegistrar).</summary>
    private async Task<Exception?> TryPayAsync(Guid invoiceId, decimal amount)
    {
        await using var db = CreateContext();
        var payable = await db.AccountsPayables.Include(p => p.Installments).SingleAsync(p => p.OriginId == invoiceId);
        try
        {
            payable.RegisterPaymentToInstallment(payable.Installments.Single().Id, amount, _userId);
            await db.SaveChangesAsync();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private async Task<AccountsPayable> ReadPayableAsync(Guid invoiceId)
    {
        await using var db = CreateContext();
        return await db.AccountsPayables.AsNoTracking().Include(p => p.Installments).SingleAsync(p => p.OriginId == invoiceId);
    }

    private async Task AssertCancelledExactlyOnceAsync(Guid invoiceId, Guid retentionId, int stockMovementsBefore)
    {
        var s = await ReadAsync(invoiceId);
        s.Status.Should().Be(PurchaseStatus.Cancelled);
        s.Retentions.Single().Status.Should().Be(RetentionStatus.Cancelled);
        var payable = s.Payables.Single();
        payable.Status.Should().Be(ERP.Domain.Modules.Payables.Enums.AccountsPayableStatus.Cancelled);
        payable.RetainedAmount.Should().Be(0m);
        var accounting = await RetentionAccountingAsync(retentionId);
        accounting.IssuedStatus.Should().Be(JournalEntryStatus.Reversed);
        accounting.Reversals.Should().Be(1, "el reverso contable ocurre exactamente una vez");
        (await StockMovementCountAsync(invoiceId)).Should().Be(stockMovementsBefore * 2, "el Kardex se revierte una sola vez");
        (await ReadElectronicAsync(retentionId))!.CurrentState.Should().Be(ElectronicDocumentState.Cancelled);
    }

    /// <summary>Sin cambio fiscal: compra intacta, comprobante AnnulmentPending, solicitud abierta, CxP retenida.</summary>
    private async Task AssertNoFiscalChangeAsync(Guid invoiceId, Guid retentionId, int stockMovements)
    {
        await ShouldBeUntouchedConfirmedAsync(invoiceId, retentionId, stockMovements);
        (await ReadElectronicAsync(retentionId))!.CurrentState.Should().Be(ElectronicDocumentState.AnnulmentPending);
        var request = (await ReadAnnulmentAsync(retentionId))!;
        request.Status.Should().Be(RetentionAnnulmentStatus.PendingSriResolution);
        request.FinalizedAtUtc.Should().BeNull();
        (await ReadPayableAsync(invoiceId)).AnnulmentHoldRequestId.Should().Be(request.Id, "la CxP sigue retenida");
        (await TryPayAsync(invoiceId, 10m)).Should().BeOfType<RetentionAnnulmentPendingException>();
    }

    private async Task<int> AuditCountAsync(Guid requestId, string action)
    {
        await using var db = CreateContext();
        return await db.RetentionAnnulmentRequestAudits.AsNoTracking()
            .CountAsync(a => a.EntityId == requestId && a.Action == action);
    }

    // ── Solicitud desde la anulación de la compra ─────────────────────────────────────────────

    [Fact]
    public async Task Anular_compra_con_retencion_autorizada_sin_flag_responde_el_error_que_invita_a_iniciar_la_anulacion_SRI()
    {
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();

        var cancel = await CancelPurchaseAsync(invoiceId);

        cancel.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.SourceCancellationRequiresSriAnnulment);
        (await ReadAnnulmentAsync(retentionId)).Should().BeNull("sin confirmación explícita no se crea ninguna solicitud");
    }

    [Fact]
    public async Task Iniciar_la_anulacion_SRI_registra_la_solicitud_sin_reversos_y_retiene_la_CxP()
    {
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();
        var stockMovements = await StockMovementCountAsync(invoiceId);

        var result = await RequestSriAnnulmentAsync(invoiceId);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Code.Should().Be(ApiResponseCodes.Retentions.AnnulmentRequested);
        result.Value!.Status.Should().Be("Confirmed", "la compra NO queda anulada todavía");
        await ShouldBeUntouchedConfirmedAsync(invoiceId, retentionId, stockMovements);
        (await ReadElectronicAsync(retentionId))!.CurrentState.Should().Be(ElectronicDocumentState.AnnulmentPending);
        var request = (await ReadAnnulmentAsync(retentionId))!;
        request.Status.Should().Be(RetentionAnnulmentStatus.PendingSubmission);
        request.Reason.Should().Be(AnnulmentReason);
        request.RequestedBy.Should().Be(_userId);
        request.AccessKey.Should().HaveLength(49);
        request.RetentionNumber.Should().NotBeNullOrWhiteSpace();
        request.ReceptorIdentification.Should().Be("1791352688001");
        request.OrdinaryDeadline.Should().Be(new DateOnly(2026, 10, 7));
        (await ReadPayableAsync(invoiceId)).AnnulmentHoldRequestId.Should().Be(request.Id);
        (await AuditCountAsync(request.Id, "Requested")).Should().Be(1);
    }

    [Fact]
    public async Task Con_solicitud_abierta_la_compra_no_se_anula_ni_admite_pagos()
    {
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();
        (await RequestSriAnnulmentAsync(invoiceId)).IsSuccess.Should().BeTrue();

        var cancel = await CancelPurchaseAsync(invoiceId);
        var payment = await TryPayAsync(invoiceId, 10m);

        cancel.Code.Should().Be(ApiResponseCodes.ElectronicDocuments.AnnulmentPending);
        payment.Should().BeOfType<RetentionAnnulmentPendingException>();
        (await ReadPayableAsync(invoiceId)).PaidAmount.Should().Be(0m);
    }

    // ── 01B — verificación en ConsultaComprobante ─────────────────────────────────────────────

    [Fact]
    public async Task Sin_presentar_no_se_consulta_el_SRI()
    {
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();
        (await RequestSriAnnulmentAsync(invoiceId)).IsSuccess.Should().BeTrue();
        var request = (await ReadAnnulmentAsync(retentionId))!;

        var result = await VerifyAsync(request.Id);

        result.IsSuccess.Should().BeFalse();
        _sriStatus.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Test1_AUTORIZADO_el_SRI_mantiene_vigente_el_comprobante_y_no_hay_reversos()
    {
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();
        var stockMovements = await StockMovementCountAsync(invoiceId);
        var request = await RequestAndSubmitAsync(invoiceId, retentionId);
        _sriStatus.FiscalStatus = SriFiscalStatus.Authorized;

        var result = await VerifyAsync(request.Id);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Code.Should().Be(ApiResponseCodes.Retentions.SriStillAuthorized);
        await AssertNoFiscalChangeAsync(invoiceId, retentionId, stockMovements);
        var stored = (await ReadAnnulmentAsync(retentionId))!;
        stored.LastSriQueryOutcome.Should().Be(SriStatusQueryOutcome.Success);
        stored.LastSriFiscalStatus.Should().Be(SriFiscalStatus.Authorized);
        stored.LastSriRawStatus.Should().Be("AUTORIZADO");
        stored.LastSriCheckAtUtc.Should().NotBeNull();
        stored.SriCheckCount.Should().Be(1);
        stored.CanBeAbandoned.Should().BeTrue("el SRI confirma que sigue vigente: el usuario puede desistir");
    }

    [Fact]
    public async Task Test2_PENDIENTE_DE_ANULAR_queda_AnnulmentPending_sin_reversos_y_se_reconsulta()
    {
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();
        var stockMovements = await StockMovementCountAsync(invoiceId);
        var request = await RequestAndSubmitAsync(invoiceId, retentionId);
        _sriStatus.FiscalStatus = SriFiscalStatus.PendingAnnulment;

        var first = await VerifyAsync(request.Id);
        var second = await VerifyAsync(request.Id);

        first.Code.Should().Be(ApiResponseCodes.Retentions.SriAnnulmentPending);
        second.Code.Should().Be(ApiResponseCodes.Retentions.SriAnnulmentPending);
        await AssertNoFiscalChangeAsync(invoiceId, retentionId, stockMovements);
        var stored = (await ReadAnnulmentAsync(retentionId))!;
        stored.LastSriFiscalStatus.Should().Be(SriFiscalStatus.PendingAnnulment);
        stored.SriCheckCount.Should().Be(2);
        stored.CanBeAbandoned.Should().BeFalse("el SRI todavía puede anularlo");
        (await AuditCountAsync(request.Id, "SriChecked")).Should().Be(1, "la misma respuesta repetida no se re-audita");
    }

    [Fact]
    public async Task NO_AUTORIZADO_no_tiene_accion_automatica()
    {
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();
        var stockMovements = await StockMovementCountAsync(invoiceId);
        var request = await RequestAndSubmitAsync(invoiceId, retentionId);
        _sriStatus.FiscalStatus = SriFiscalStatus.NotAuthorized;

        var result = await VerifyAsync(request.Id);

        result.Code.Should().Be(ApiResponseCodes.Retentions.SriNotAuthorized);
        await AssertNoFiscalChangeAsync(invoiceId, retentionId, stockMovements);
        (await ReadAnnulmentAsync(retentionId))!.LastSriRawStatus.Should().Be("NO AUTORIZADO");
    }

    [Fact]
    public async Task Test3_ANULADO_finaliza_la_anulacion_de_la_compra_por_su_flujo_oficial_una_vez_con_evidencia()
    {
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();
        var stockMovements = await StockMovementCountAsync(invoiceId);
        var request = await RequestAndSubmitAsync(invoiceId, retentionId);

        var result = await VerifyAnnulledAsync(request.Id);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Code.Should().Be(ApiResponseCodes.Retentions.AnnulmentFinalized);
        await AssertCancelledExactlyOnceAsync(invoiceId, retentionId, stockMovements);
        var stored = (await ReadAnnulmentAsync(retentionId))!;
        stored.Status.Should().Be(RetentionAnnulmentStatus.Accepted);
        stored.FinalizedAtUtc.Should().NotBeNull();
        stored.EvidenceReference.Should().StartWith("ConsultaComprobante ANULADO");
        stored.SriAnnulmentEvidence.Should().Contain("<estadoAutorizacion>ANULADO</estadoAutorizacion>").And.Contain(stored.AccessKey);
        stored.LastSriRawStatus.Should().Be("ANULADO");
        stored.ResolvedOn.Should().NotBeNull();
        (await ReadAsync(invoiceId)).Payables.Single().AnnulmentHoldRequestId.Should().BeNull();
        (await AuditCountAsync(request.Id, "ResolvedAccepted")).Should().Be(1);
    }

    [Fact]
    public async Task Test4_ANULADO_repetido_es_idempotente_y_no_vuelve_a_consultar_el_SRI()
    {
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();
        var stockMovements = await StockMovementCountAsync(invoiceId);
        var request = await RequestAndSubmitAsync(invoiceId, retentionId);
        (await VerifyAnnulledAsync(request.Id)).IsSuccess.Should().BeTrue();
        var evidence = (await ReadAnnulmentAsync(retentionId))!.EvidenceReference;

        var repeated = await VerifyAnnulledAsync(request.Id);

        repeated.IsSuccess.Should().BeTrue();
        repeated.Code.Should().Be(ApiResponseCodes.Retentions.AnnulmentFinalized);
        _sriStatus.Calls.Should().Be(1, "una solicitud ya aceptada no se vuelve a consultar");
        await AssertCancelledExactlyOnceAsync(invoiceId, retentionId, stockMovements);
        (await ReadAnnulmentAsync(retentionId))!.EvidenceReference.Should().Be(evidence);
        (await AuditCountAsync(request.Id, "ResolvedAccepted")).Should().Be(1);
    }

    [Fact]
    public async Task Test5_consulta_RECHAZADA_codigo_99_no_cambia_el_estado_fiscal()
    {
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();
        var stockMovements = await StockMovementCountAsync(invoiceId);
        var request = await RequestAndSubmitAsync(invoiceId, retentionId);
        var http = SriHttpDouble.Soap(
            $"""
                <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body>
                  <ns2:consultarEstadoAutorizacionComprobanteResponse xmlns:ns2="http://ec.gob.sri.ws.consultas">
                    <EstadoAutorizacionComprobante>
                      <claveAcceso>{request.AccessKey}</claveAcceso>
                      <mensajes><mensaje><identificador>99</identificador><mensaje>ERROR AL CONSULTAR DATOS DEL SERVICIO WEB</mensaje>
                        <informacionAdicional>No es posible validar la clave de acceso ya que la fecha de emisión está fuera del rango permitido.</informacionAdicional>
                        <tipo>ERROR</tipo></mensaje></mensajes>
                      <estadoConsulta>RECHAZADA</estadoConsulta>
                    </EstadoAutorizacionComprobante>
                  </ns2:consultarEstadoAutorizacionComprobanteResponse>
                </soap:Body></soap:Envelope>
                """
        );

        var result = await VerifyAsync(request.Id, RetentionElectronicTestWiring.SoapStatusQuery(http));

        result.IsSuccess.Should().BeTrue();
        result.Code.Should().Be(ApiResponseCodes.Retentions.SriVerificationFailed);
        http.Calls.Should().Be(1);
        await AssertNoFiscalChangeAsync(invoiceId, retentionId, stockMovements);
        var stored = (await ReadAnnulmentAsync(retentionId))!;
        stored.LastSriQueryOutcome.Should().Be(SriStatusQueryOutcome.Rejected);
        stored.LastSriFiscalStatus.Should().Be(SriFiscalStatus.Unknown, "RECHAZADA nunca es NO AUTORIZADO");
        stored.LastSriRawStatus.Should().Be("RECHAZADA");
    }

    [Fact]
    public async Task Test6_timeout_no_cambia_el_estado_fiscal()
    {
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();
        var stockMovements = await StockMovementCountAsync(invoiceId);
        var request = await RequestAndSubmitAsync(invoiceId, retentionId);
        var http = SriHttpDouble.Throwing(() => new TaskCanceledException("HttpClient.Timeout"));

        var result = await VerifyAsync(request.Id, RetentionElectronicTestWiring.SoapStatusQuery(http));

        result.Code.Should().Be(ApiResponseCodes.Retentions.SriVerificationFailed);
        await AssertNoFiscalChangeAsync(invoiceId, retentionId, stockMovements);
        var stored = (await ReadAnnulmentAsync(retentionId))!;
        stored.LastSriQueryOutcome.Should().Be(SriStatusQueryOutcome.Timeout);
        stored.LastSriFiscalStatus.Should().Be(SriFiscalStatus.Unknown, "un timeout nunca es ANULADO");
    }

    [Fact]
    public async Task Test7_error_de_red_no_cambia_el_estado_fiscal()
    {
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();
        var stockMovements = await StockMovementCountAsync(invoiceId);
        var request = await RequestAndSubmitAsync(invoiceId, retentionId);
        var http = SriHttpDouble.Throwing(() => new HttpRequestException("no route to host"));

        var result = await VerifyAsync(request.Id, RetentionElectronicTestWiring.SoapStatusQuery(http));

        result.Code.Should().Be(ApiResponseCodes.Retentions.SriVerificationFailed);
        await AssertNoFiscalChangeAsync(invoiceId, retentionId, stockMovements);
        (await ReadAnnulmentAsync(retentionId))!.LastSriQueryOutcome.Should().Be(SriStatusQueryOutcome.Unavailable);
    }

    [Fact]
    public async Task ANULADO_por_el_parser_SOAP_real_finaliza_la_compra()
    {
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();
        var stockMovements = await StockMovementCountAsync(invoiceId);
        var request = await RequestAndSubmitAsync(invoiceId, retentionId);
        var http = SriHttpDouble.Soap(
            $"""
                <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body>
                  <ns2:consultarEstadoAutorizacionComprobanteResponse xmlns:ns2="http://ec.gob.sri.ws.consultas">
                    <EstadoAutorizacionComprobante>
                      <claveAcceso>{request.AccessKey}</claveAcceso><mensajes/>
                      <estadoAutorizacion>ANULADO</estadoAutorizacion>
                      <tipoComprobante>COMPROBANTE DE RETENCION</tipoComprobante>
                      <rucEmisor>1790000000001</rucEmisor>
                      <fechaAutorizacion>2026-09-17T10:49:37-05:00</fechaAutorizacion>
                    </EstadoAutorizacionComprobante>
                  </ns2:consultarEstadoAutorizacionComprobanteResponse>
                </soap:Body></soap:Envelope>
                """
        );

        var result = await VerifyAsync(request.Id, RetentionElectronicTestWiring.SoapStatusQuery(http));

        result.Code.Should().Be(ApiResponseCodes.Retentions.AnnulmentFinalized);
        await AssertCancelledExactlyOnceAsync(invoiceId, retentionId, stockMovements);
        (await ReadAnnulmentAsync(retentionId))!.SriAnnulmentEvidence.Should().Contain("ns2:consultarEstadoAutorizacionComprobanteResponse");
    }

    // ── Desistimiento ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Desistir_antes_de_presentar_libera_todo_sin_tocar_la_compra()
    {
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();
        (await RequestSriAnnulmentAsync(invoiceId)).IsSuccess.Should().BeTrue();
        var request = (await ReadAnnulmentAsync(retentionId))!;

        await using (var db = CreateWiredContext())
            (await Annulments(db).AbandonAsync(_tenantId, _companyId, request.Id, "Se decidió no anular", _userId))
                .IsSuccess.Should().BeTrue();

        (await ReadAnnulmentAsync(retentionId))!.Status.Should().Be(RetentionAnnulmentStatus.Abandoned);
        (await ReadElectronicAsync(retentionId))!.CurrentState.Should().Be(ElectronicDocumentState.Authorized);
        (await ReadAsync(invoiceId)).Status.Should().Be(PurchaseStatus.Confirmed);
        (await ReadPayableAsync(invoiceId)).AnnulmentHoldRequestId.Should().BeNull();
    }

    [Fact]
    public async Task Presentada_solo_se_desiste_cuando_el_SRI_confirma_AUTORIZADO_y_entonces_libera_todo()
    {
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();
        var stockMovements = await StockMovementCountAsync(invoiceId);
        var request = await RequestAndSubmitAsync(invoiceId, retentionId);

        _sriStatus.FiscalStatus = SriFiscalStatus.PendingAnnulment;
        await VerifyAsync(request.Id);
        await using (var db = CreateWiredContext())
        {
            // Regla de dominio (DomainRuleBehavior la traduce a 422 en la API).
            var abandon = () => Annulments(db).AbandonAsync(_tenantId, _companyId, request.Id, "x", _userId);
            await abandon.Should().ThrowAsync<ERP.Domain.Exceptions.DomainRuleViolationException>(
                "PENDIENTE DE ANULAR: el SRI todavía puede anularlo");
        }
        (await ReadAnnulmentAsync(retentionId))!.Status.Should().Be(RetentionAnnulmentStatus.PendingSriResolution);

        _sriStatus.FiscalStatus = SriFiscalStatus.Authorized;
        await VerifyAsync(request.Id);
        await using (var db = CreateWiredContext())
            (await Annulments(db).AbandonAsync(_tenantId, _companyId, request.Id, "El receptor no aceptó la anulación", _userId))
                .IsSuccess.Should().BeTrue();

        await ShouldBeUntouchedConfirmedAsync(invoiceId, retentionId, stockMovements);
        (await ReadAnnulmentAsync(retentionId))!.Status.Should().Be(RetentionAnnulmentStatus.Abandoned);
        (await ReadElectronicAsync(retentionId))!.CurrentState.Should().Be(ElectronicDocumentState.Authorized);
        (await ReadPayableAsync(invoiceId)).AnnulmentHoldRequestId.Should().BeNull("la CxP se libera");
        (await TryPayAsync(invoiceId, 10m)).Should().BeNull("sin solicitud abierta los pagos vuelven a admitirse");
    }

    // ── Concurrencia (PostgreSQL real) ────────────────────────────────────────────────────────

    [Fact]
    public async Task A_solicitud_vs_pago_el_pago_cargado_antes_falla_por_xmin_y_no_hay_pago_con_solicitud_abierta()
    {
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();
        await using var paymentDb = CreateContext();
        var payable = await paymentDb.AccountsPayables.Include(p => p.Installments).SingleAsync(p => p.OriginId == invoiceId);

        (await RequestSriAnnulmentAsync(invoiceId)).IsSuccess.Should().BeTrue();
        payable.RegisterPaymentToInstallment(payable.Installments.Single().Id, 10m, _userId);
        var save = () => paymentDb.SaveChangesAsync();

        await save.Should().ThrowAsync<DbUpdateConcurrencyException>();
        var stored = await ReadPayableAsync(invoiceId);
        stored.PaidAmount.Should().Be(0m);
        stored.AnnulmentHoldRequestId.Should().NotBeNull();
    }

    [Fact]
    public async Task A_pago_primero_impide_iniciar_la_anulacion()
    {
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();
        (await TryPayAsync(invoiceId, 10m)).Should().BeNull();

        var result = await RequestSriAnnulmentAsync(invoiceId);

        result.IsSuccess.Should().BeFalse("la compra con pagos no puede terminar anulándose");
        (await ReadAnnulmentAsync(retentionId)).Should().BeNull();
        (await ReadElectronicAsync(retentionId))!.CurrentState.Should().Be(ElectronicDocumentState.Authorized);
    }

    [Fact]
    public async Task B_solicitud_vs_credito_de_proveedor_el_credito_cargado_antes_falla_y_despues_se_bloquea()
    {
        var (invoiceId, _) = await ConfirmWithAuthorizedRetentionAsync();
        await using var creditDb = CreateContext();
        var payable = await creditDb.AccountsPayables.Include(p => p.Installments).SingleAsync(p => p.OriginId == invoiceId);

        (await RequestSriAnnulmentAsync(invoiceId)).IsSuccess.Should().BeTrue();
        payable.ApplySupplierCredit(10m, _userId);
        var save = () => creditDb.SaveChangesAsync();
        await save.Should().ThrowAsync<DbUpdateConcurrencyException>();

        await using var lateDb = CreateContext();
        var late = await lateDb.AccountsPayables.Include(p => p.Installments).SingleAsync(p => p.OriginId == invoiceId);
        var apply = () => late.ApplySupplierCredit(10m, _userId);
        apply.Should().Throw<RetentionAnnulmentPendingException>();
        (await ReadPayableAsync(invoiceId)).SupplierCreditAmount.Should().Be(0m);
    }

    [Fact]
    public async Task C_ANULADO_del_SRI_vs_pago_concurrente_el_pago_nunca_se_aplica_y_la_finalizacion_completa()
    {
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();
        var stockMovements = await StockMovementCountAsync(invoiceId);
        var request = await RequestAndSubmitAsync(invoiceId, retentionId);
        _sriStatus.FiscalStatus = SriFiscalStatus.Annulled;

        var outcomes = await Task.WhenAll(
            VerifyAsync(request.Id).ContinueWith(t => (object?)t.Result),
            TryPayAsync(invoiceId, 10m).ContinueWith(t => (object?)t.Result)
        );

        outcomes[1].Should().NotBeNull("el pago siempre encuentra la CxP retenida o ya anulada");
        await AssertCancelledExactlyOnceAsync(invoiceId, retentionId, stockMovements);
        (await ReadPayableAsync(invoiceId)).PaidAmount.Should().Be(0m);
    }

    [Fact]
    public async Task Test8_D_polling_y_verificacion_en_linea_simultaneos_con_ANULADO_anulan_la_compra_una_sola_vez()
    {
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();
        var stockMovements = await StockMovementCountAsync(invoiceId);
        var request = await RequestAndSubmitAsync(invoiceId, retentionId);
        _sriStatus.FiscalStatus = SriFiscalStatus.Annulled;

        var results = await Task.WhenAll(
            TryVerifyAsync(request.Id, Guid.Empty), // job
            TryVerifyAsync(request.Id, _userId), // UI
            TryVerifyAsync(request.Id, Guid.Empty)
        );

        results.Should().Contain(r => r == null, string.Join(" | ", results));
        await AssertCancelledExactlyOnceAsync(invoiceId, retentionId, stockMovements);
        (await AuditCountAsync(request.Id, "ResolvedAccepted")).Should().Be(1);
        (await AuditCountAsync(request.Id, "OriginFinalized")).Should().Be(1);
    }

    private async Task<string?> TryVerifyAsync(Guid requestId, Guid userId)
    {
        try
        {
            var r = await VerifyAsync(requestId, userId: userId);
            return r.IsSuccess ? null : r.Code + ":" + r.Error;
        }
        catch (Exception ex)
        {
            return ex.GetType().Name;
        }
    }

    [Fact]
    public async Task E_dos_solicitudes_simultaneas_crean_una_sola_solicitud_abierta()
    {
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();

        var results = await Task.WhenAll(TryRequestAsync(invoiceId), TryRequestAsync(invoiceId));

        results.Count(r => r).Should().Be(1);
        await using var db = CreateContext();
        (await db.RetentionAnnulmentRequests.AsNoTracking().CountAsync(r => r.RetentionDocumentId == retentionId))
            .Should().Be(1);
    }

    private async Task<bool> TryRequestAsync(Guid invoiceId)
    {
        try
        {
            return (await RequestSriAnnulmentAsync(invoiceId)).IsSuccess;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    [Fact]
    public async Task H_finalizacion_reintentada_y_concurrente_anula_el_origen_exactamente_una_vez()
    {
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();
        var stockMovements = await StockMovementCountAsync(invoiceId);
        var request = await RequestAndSubmitAsync(invoiceId, retentionId);
        (await VerifyAnnulledAsync(request.Id)).IsSuccess.Should().BeTrue();

        await Task.WhenAll(FinalizeAsync(request.Id), FinalizeAsync(request.Id));
        await FinalizeAsync(request.Id);

        await AssertCancelledExactlyOnceAsync(invoiceId, retentionId, stockMovements);
        (await CancelPurchaseAsync(invoiceId)).IsSuccess.Should().BeFalse("la compra ya fue anulada");
        (await RetentionAccountingAsync(retentionId)).Reversals.Should().Be(1);
    }

    private async Task FinalizeAsync(Guid requestId)
    {
        try
        {
            await using var db = CreateWiredContext();
            await Annulments(db).FinalizeAsync(_tenantId, _companyId, requestId, _userId);
        }
        catch (DbUpdateException)
        {
            // Carrera esperada sobre el marcador de finalización (xmin): el estado final lo verifica el test.
        }
    }

    [Fact]
    public async Task Finalizacion_pendiente_tras_un_fallo_queda_registrada_y_se_completa_al_reintentar()
    {
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();
        var stockMovements = await StockMovementCountAsync(invoiceId);
        var request = await RequestAndSubmitAsync(invoiceId, retentionId);
        _sriStatus.FiscalStatus = SriFiscalStatus.Annulled;

        // ANULADO informado por el SRI con un origen sin flujo de anulación disponible → la finalización falla.
        await using (var db = CreateWiredContext())
        {
            var noOrigins = RetentionElectronicTestWiring.AnnulmentService(db, new FixedCurrentCompany(_companyId), _sriStatus);
            var verified = await noOrigins.VerifyWithSriAsync(_tenantId, _companyId, request.Id, _userId);
            verified.Code.Should().Be(ApiResponseCodes.Retentions.AnnulmentFinalizationPending);
        }
        var pending = (await ReadAnnulmentAsync(retentionId))!;
        pending.RequiresFinalization.Should().BeTrue();
        pending.LastFinalizationError.Should().NotBeNullOrWhiteSpace();
        (await ReadAsync(invoiceId)).Status.Should().Be(PurchaseStatus.Confirmed);

        await FinalizeAsync(request.Id);

        await AssertCancelledExactlyOnceAsync(invoiceId, retentionId, stockMovements);
        (await ReadAnnulmentAsync(retentionId))!.FinalizedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Recuperacion_encuentra_las_finalizaciones_pendientes_entre_tenants()
    {
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();
        var request = await RequestAndSubmitAsync(invoiceId, retentionId);
        _sriStatus.FiscalStatus = SriFiscalStatus.Annulled;
        await using (var db = CreateWiredContext())
            await RetentionElectronicTestWiring.AnnulmentService(db, new FixedCurrentCompany(_companyId), _sriStatus)
                .VerifyWithSriAsync(_tenantId, _companyId, request.Id, _userId);

        await using var jobDb = CreateJobContext();
        var pending = await new ERP.Infrastructure.Persistence.Repositories.Retentions.RetentionAnnulmentRequestRepository(jobDb)
            .GetPendingFinalizationAsync(50);

        pending.Should().Contain((_tenantId, _companyId, request.Id));
    }

    [Fact]
    public async Task Polling_encuentra_entre_tenants_solo_las_presentadas_no_verificadas_recientemente()
    {
        var (invoiceId, retentionId) = await ConfirmWithAuthorizedRetentionAsync();
        (await RequestSriAnnulmentAsync(invoiceId)).IsSuccess.Should().BeTrue();
        var request = (await ReadAnnulmentAsync(retentionId))!;
        await using var jobDb = CreateJobContext();
        var repository = new ERP.Infrastructure.Persistence.Repositories.Retentions.RetentionAnnulmentRequestRepository(jobDb);

        (await repository.GetDueForSriVerificationAsync(DateTime.UtcNow, 50))
            .Should().NotContain((_tenantId, _companyId, request.Id), "sin presentar no se consulta el SRI");

        (await SubmitAnnulmentAsync(request.Id)).IsSuccess.Should().BeTrue();
        (await repository.GetDueForSriVerificationAsync(DateTime.UtcNow, 50))
            .Should().Contain((_tenantId, _companyId, request.Id), "presentada y nunca verificada");

        _sriStatus.FiscalStatus = SriFiscalStatus.PendingAnnulment;
        await VerifyAsync(request.Id, userId: Guid.Empty);
        (await repository.GetDueForSriVerificationAsync(DateTime.UtcNow.AddMinutes(-30), 50))
            .Should().NotContain((_tenantId, _companyId, request.Id), "verificada hace menos del intervalo");
        (await repository.GetDueForSriVerificationAsync(DateTime.UtcNow.AddMinutes(1), 50))
            .Should().Contain((_tenantId, _companyId, request.Id));
    }
}
