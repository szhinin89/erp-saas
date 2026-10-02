using ERP.Domain.Exceptions;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;
using ERP.Domain.Modules.Payables.Entities;
using ERP.Domain.Modules.Payables.Enums;
using ERP.Domain.Modules.Payables.Exceptions;
using ERP.Domain.Modules.Retentions;
using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.Retentions;

/// <summary>
/// ZH-RETENTION-SRI-ANNULMENT-01/01B (ADR-036 D-6…D-8, §24) — invariantes de dominio de la anulación ante
/// el SRI de una retención autorizada: comprobante (AnnulmentPending, Cancelled solo con evidencia),
/// solicitud (máquina de estados, aceptación SOLO con un ANULADO consultado en ConsultaComprobante,
/// idempotencia, plazo ordinario) y retención de la CxP.
/// </summary>
public sealed class RetentionSriAnnulmentDomainTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateOnly IssueDate = new(2026, 9, 17);

    private static ElectronicDocument AuthorizedDocument()
    {
        var document = ElectronicDocument.Create(
            TenantId, CompanyId, ElectronicDocumentType.Retention, "Retentions", Guid.NewGuid(), UserId);
        document.MarkXmlGenerated("draft.xml", "2.0.0", "2.0.0", UserId);
        document.MarkSigned("signed.xml", AccessKey.Create(new string('5', 49)), UserId);
        document.MarkDispatching(UserId);
        document.MarkSent(UserId);
        document.MarkReceived(UserId);
        document.MarkAuthorized(AuthorizationNumber.Create(new string('5', 49)), DateTime.UtcNow, null, UserId);
        return document;
    }

    private static ExternalAnnulmentEvidence Evidence() =>
        new(new DateOnly(2026, 10, 3), "SRI-TRAMITE-0001", UserId);

    private static RetentionDocument IssuedRetention(DateOnly? issueDate = null)
    {
        var retention = RetentionDocument.Create(
            TenantId, CompanyId, Guid.NewGuid(), RetentionSourceDocumentType.PurchaseInvoice,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), UserId);
        retention.AddLine(RetentionDocumentLine.Create(retention.Id, TenantId, RetentionTaxType.Vat, "725", "IVA 30%", 15m, 30m, 4.5m));
        retention.Issue("001-001-000000007", issueDate ?? IssueDate, UserId);
        return retention;
    }

    private static RetentionAnnulmentRequest NewRequest(DateOnly? issueDate = null) =>
        RetentionAnnulmentRequest.Create(
            IssuedRetention(issueDate), Guid.NewGuid(), new string('5', 49), "1791352688001", "Proveedor", "Error de digitación", UserId);

    private static RetentionAnnulmentRequest SubmittedRequest()
    {
        var request = NewRequest();
        request.MarkSubmitted(new DateOnly(2026, 9, 20), "TRAMITE-1", null, UserId);
        return request;
    }

    private static void SriSays(RetentionAnnulmentRequest request, SriFiscalStatus status, string raw) =>
        request.RecordSriCheck(DateTime.UtcNow, SriStatusQueryOutcome.Success, status, raw, UserId);

    private static void SriQueryFails(RetentionAnnulmentRequest request, SriStatusQueryOutcome outcome) =>
        request.RecordSriCheck(DateTime.UtcNow, outcome, SriFiscalStatus.Unknown, "RECHAZADA", UserId);

    // ── Comprobante electrónico ────────────────────────────────────────────────────────────

    [Fact]
    public void Authorized_no_puede_pasar_a_Cancelled_directamente()
    {
        var document = AuthorizedDocument();

        var act = () => document.ConfirmExternalAnnulment(Guid.NewGuid(), Evidence(), UserId);

        act.Should().Throw<DomainRuleViolationException>();
        document.CurrentState.Should().Be(ElectronicDocumentState.Authorized);
    }

    [Fact]
    public void AnnulmentPending_mantiene_el_comprobante_vigente_y_no_reintentable()
    {
        var document = AuthorizedDocument();
        var requestId = Guid.NewGuid();

        document.MarkAnnulmentPending(requestId, UserId);

        document.CurrentState.Should().Be(ElectronicDocumentState.AnnulmentPending);
        document.AnnulmentRequestId.Should().Be(requestId);
        document.AuthorizationNumber.Should().NotBeNull("la evidencia electrónica no se modifica");
        ((Action)(() => document.MarkRetryAttempted(UserId))).Should().Throw<DomainRuleViolationException>();
        ((Action)(() => document.MarkDeadLetter("x", UserId))).Should().Throw<DomainRuleViolationException>();
        ((Action)(() => document.MarkDiscarded("x", UserId))).Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void AnnulmentPending_es_idempotente_para_la_misma_solicitud_y_rechaza_otra()
    {
        var document = AuthorizedDocument();
        var requestId = Guid.NewGuid();
        document.MarkAnnulmentPending(requestId, UserId);

        document.MarkAnnulmentPending(requestId, UserId);
        var other = () => document.MarkAnnulmentPending(Guid.NewGuid(), UserId);

        other.Should().Throw<DomainRuleViolationException>();
        document.AnnulmentRequestId.Should().Be(requestId);
    }

    [Fact]
    public void Confirmacion_exige_la_misma_solicitud_y_evidencia_y_lleva_a_Cancelled()
    {
        var document = AuthorizedDocument();
        var requestId = Guid.NewGuid();
        document.MarkAnnulmentPending(requestId, UserId);

        var wrongRequest = () => document.ConfirmExternalAnnulment(Guid.NewGuid(), Evidence(), UserId);
        wrongRequest.Should().Throw<DomainRuleViolationException>();

        document.ConfirmExternalAnnulment(requestId, Evidence(), UserId);

        document.CurrentState.Should().Be(ElectronicDocumentState.Cancelled);
    }

    [Fact]
    public void Evidencia_sin_referencia_o_sin_fecha_es_invalida()
    {
        ((Action)(() => new ExternalAnnulmentEvidence(new DateOnly(2026, 10, 3), " ", UserId))).Should().Throw<ArgumentException>();
        ((Action)(() => new ExternalAnnulmentEvidence(default, "REF", UserId))).Should().Throw<ArgumentException>();
        // 01B: la evidencia la obtiene el ERP del SRI; Guid.Empty = verificación automática (job).
        new ExternalAnnulmentEvidence(new DateOnly(2026, 10, 3), "REF", Guid.Empty).Describe()
            .Should().Contain("ConsultaComprobante");
    }

    [Fact]
    public void Revertir_la_anulacion_devuelve_el_comprobante_a_Authorized()
    {
        var document = AuthorizedDocument();
        var requestId = Guid.NewGuid();
        document.MarkAnnulmentPending(requestId, UserId);

        document.RevertAnnulment(requestId, "SRI: RECHAZADO", UserId);

        document.CurrentState.Should().Be(ElectronicDocumentState.Authorized);
        document.AnnulmentRequestId.Should().BeNull();
    }

    // ── Solicitud de anulación ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(2026, 9, 17, 2026, 10, 7)]
    [InlineData(2026, 1, 31, 2026, 2, 7)]
    [InlineData(2026, 12, 15, 2027, 1, 7)]
    public void Plazo_ordinario_es_el_dia_7_del_mes_siguiente_a_la_emision(int y, int m, int d, int ey, int em, int ed)
    {
        RetentionAnnulmentDeadline.Ordinary(new DateOnly(y, m, d)).Should().Be(new DateOnly(ey, em, ed));
        NewRequest(new DateOnly(y, m, d)).OrdinaryDeadline.Should().Be(new DateOnly(ey, em, ed));
    }

    [Fact]
    public void Solo_una_retencion_emitida_puede_solicitar_anulacion()
    {
        var retention = IssuedRetention();
        retention.Cancel("x", UserId);

        var act = () => RetentionAnnulmentRequest.Create(retention, Guid.NewGuid(), new string('5', 49), "1", "P", "m", UserId);

        act.Should().Throw<DomainRuleViolationException>();
    }

    [Fact]
    public void Flujo_normal_solicitada_presentada_SRI_ANULADO_y_finalizada()
    {
        var request = NewRequest();
        request.Status.Should().Be(RetentionAnnulmentStatus.PendingSubmission);
        request.IsOpen.Should().BeTrue();

        request.MarkSubmitted(new DateOnly(2026, 9, 20), "TRAMITE-1", null, UserId);
        request.Status.Should().Be(RetentionAnnulmentStatus.PendingSriResolution);

        SriSays(request, SriFiscalStatus.Annulled, "ANULADO");
        request.AcceptSriAnnulment(new DateOnly(2026, 9, 22), "ConsultaComprobante ANULADO", "<soap/>", UserId)
            .Should().BeTrue();
        request.Status.Should().Be(RetentionAnnulmentStatus.Accepted);
        request.SriAnnulmentEvidence.Should().Be("<soap/>");
        request.LastSriRawStatus.Should().Be("ANULADO");
        request.RequiresFinalization.Should().BeTrue();

        request.MarkFinalized(UserId);
        request.MarkFinalized(UserId);
        request.RequiresFinalization.Should().BeFalse();
        request.FinalizationAttempts.Should().Be(1);
    }

    [Fact]
    public void ANULADO_repetido_es_idempotente()
    {
        var request = SubmittedRequest();
        SriSays(request, SriFiscalStatus.Annulled, "ANULADO");
        request.AcceptSriAnnulment(new DateOnly(2026, 9, 22), "REF", null, UserId);

        request.AcceptSriAnnulment(new DateOnly(2026, 9, 23), "OTRA", null, UserId).Should().BeFalse();

        request.EvidenceReference.Should().Be("REF");
        request.ResolvedOn.Should().Be(new DateOnly(2026, 9, 22));
    }

    [Theory]
    [InlineData(SriFiscalStatus.Authorized, "AUTORIZADO")]
    [InlineData(SriFiscalStatus.PendingAnnulment, "PENDIENTE DE ANULAR")]
    [InlineData(SriFiscalStatus.NotAuthorized, "NO AUTORIZADO")]
    public void Sin_ANULADO_consultado_la_solicitud_no_se_acepta(SriFiscalStatus status, string raw)
    {
        var request = SubmittedRequest();
        SriSays(request, status, raw);

        var act = () => request.AcceptSriAnnulment(new DateOnly(2026, 9, 22), "REF", null, UserId);

        act.Should().Throw<DomainRuleViolationException>();
        request.Status.Should().Be(RetentionAnnulmentStatus.PendingSriResolution);
        request.LastSriFiscalStatus.Should().Be(status);
        request.LastSriRawStatus.Should().Be(raw);
    }

    [Theory]
    [InlineData(SriStatusQueryOutcome.Rejected)]
    [InlineData(SriStatusQueryOutcome.Timeout)]
    [InlineData(SriStatusQueryOutcome.Unavailable)]
    [InlineData(SriStatusQueryOutcome.Unknown)]
    public void Consulta_fallida_no_es_estado_fiscal_ni_permite_aceptar(SriStatusQueryOutcome outcome)
    {
        var request = SubmittedRequest();
        SriSays(request, SriFiscalStatus.Annulled, "ANULADO");
        SriQueryFails(request, outcome);

        request.LastSriQueryOutcome.Should().Be(outcome);
        request.LastSriFiscalStatus.Should().Be(SriFiscalStatus.Unknown, "una consulta fallida no informa estado fiscal");
        ((Action)(() => request.AcceptSriAnnulment(new DateOnly(2026, 9, 22), "REF", null, UserId)))
            .Should().Throw<DomainRuleViolationException>("solo la ÚLTIMA consulta, exitosa y ANULADO, acepta");
        ((Action)(() => request.RecordSriCheck(DateTime.UtcNow, outcome, SriFiscalStatus.Annulled, "x", UserId)))
            .Should().Throw<ArgumentException>("una consulta fallida nunca trae estado fiscal");
        request.Status.Should().Be(RetentionAnnulmentStatus.PendingSriResolution);
    }

    [Fact]
    public void Solo_se_consulta_el_SRI_con_la_solicitud_presentada_y_se_audita_solo_al_cambiar()
    {
        var request = NewRequest();
        ((Action)(() => SriSays(request, SriFiscalStatus.Authorized, "AUTORIZADO")))
            .Should().Throw<DomainRuleViolationException>("todavía no se presentó al SRI");

        request.MarkSubmitted(new DateOnly(2026, 9, 20), null, null, UserId);
        request.ClearDomainEvents();
        SriSays(request, SriFiscalStatus.PendingAnnulment, "PENDIENTE DE ANULAR");
        SriSays(request, SriFiscalStatus.PendingAnnulment, "PENDIENTE DE ANULAR");

        request.SriCheckCount.Should().Be(2);
        request.DomainEvents.Should().HaveCount(1, "el polling repetido no inunda la auditoría");
    }

    [Fact]
    public void Desistir_antes_de_presentar_o_si_el_SRI_confirma_AUTORIZADO()
    {
        var pending = NewRequest();
        pending.CanBeAbandoned.Should().BeTrue();
        pending.Abandon("ya no se anulará", UserId);
        pending.Status.Should().Be(RetentionAnnulmentStatus.Abandoned);

        var submitted = SubmittedRequest();
        submitted.CanBeAbandoned.Should().BeFalse("sin consulta exitosa el SRI todavía podría anularlo");
        ((Action)(() => submitted.Abandon("x", UserId))).Should().Throw<DomainRuleViolationException>();

        SriSays(submitted, SriFiscalStatus.PendingAnnulment, "PENDIENTE DE ANULAR");
        ((Action)(() => submitted.Abandon("x", UserId))).Should().Throw<DomainRuleViolationException>();

        SriSays(submitted, SriFiscalStatus.Authorized, "AUTORIZADO");
        submitted.CanBeAbandoned.Should().BeTrue();
        submitted.Abandon("el receptor no aceptó la anulación", UserId);
        submitted.Status.Should().Be(RetentionAnnulmentStatus.Abandoned);
    }

    // ── Retención de la CxP ────────────────────────────────────────────────────────────────

    private static AccountsPayable Payable()
    {
        var payable = AccountsPayable.CreateFromOrigin(
            TenantId, CompanyId, Guid.NewGuid(), Guid.NewGuid(), AccountsPayableOriginType.PurchaseInvoice,
            Guid.NewGuid(), "01", "001-001-000000001", IssueDate, IssueDate, UserId);
        payable.AddInstallment(1, new DateOnly(2026, 10, 17), 115m);
        payable.ApplyRetention(4.5m, UserId);
        return payable;
    }

    [Fact]
    public void CxP_retenida_bloquea_pagos_creditos_notas_y_devoluciones_con_codigo_semantico()
    {
        var payable = Payable();
        payable.PlaceAnnulmentHold(Guid.NewGuid(), UserId);
        var installmentId = payable.Installments.Single().Id;

        var operations = new Action[]
        {
            () => payable.RegisterPayment(10m, UserId),
            () => payable.RegisterPaymentToInstallment(installmentId, 10m, UserId),
            () => payable.ApplySupplierCredit(10m, UserId),
            () => payable.ApplyCreditNote(10m, UserId),
            () => payable.ApplyReturnCredit(10m, UserId),
        };

        foreach (var operation in operations)
            operation.Should().Throw<RetentionAnnulmentPendingException>()
                .Which.ApiCode.Should().Be("RETENTION_ANNULMENT_PENDING");
        payable.PaidAmount.Should().Be(0m);
        payable.OutstandingAmount.Should().Be(110.5m);
    }

    [Fact]
    public void CxP_retenida_permite_la_reversa_de_retencion_y_su_anulacion_que_libera_la_retencion()
    {
        var payable = Payable();
        payable.PlaceAnnulmentHold(Guid.NewGuid(), UserId);

        payable.ReverseRetention(UserId);
        payable.Cancel(UserId);

        payable.Status.Should().Be(AccountsPayableStatus.Cancelled);
        payable.IsOnAnnulmentHold.Should().BeFalse();
    }

    [Fact]
    public void No_se_retiene_una_CxP_con_pagos_ni_con_otra_solicitud()
    {
        var paid = Payable();
        paid.RegisterPayment(10m, UserId);
        ((Action)(() => paid.PlaceAnnulmentHold(Guid.NewGuid(), UserId))).Should().Throw<DomainRuleViolationException>();

        var held = Payable();
        var requestId = Guid.NewGuid();
        held.PlaceAnnulmentHold(requestId, UserId);
        held.PlaceAnnulmentHold(requestId, UserId);
        ((Action)(() => held.PlaceAnnulmentHold(Guid.NewGuid(), UserId))).Should().Throw<RetentionAnnulmentPendingException>();
    }

    [Fact]
    public void Liberar_la_retencion_es_idempotente_y_solo_para_su_solicitud()
    {
        var payable = Payable();
        var requestId = Guid.NewGuid();
        payable.PlaceAnnulmentHold(requestId, UserId);

        payable.ReleaseAnnulmentHold(Guid.NewGuid(), UserId);
        payable.IsOnAnnulmentHold.Should().BeTrue();
        payable.ReleaseAnnulmentHold(requestId, UserId);
        payable.ReleaseAnnulmentHold(requestId, UserId);

        payable.IsOnAnnulmentHold.Should().BeFalse();
        payable.RegisterPayment(10m, UserId);
        payable.PaidAmount.Should().Be(10m);
    }
}
