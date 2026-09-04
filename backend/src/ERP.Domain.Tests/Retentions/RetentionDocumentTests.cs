using ERP.Domain.Modules.Retentions.Entities;
using ERP.Domain.Modules.Retentions.Enums;
using ERP.Domain.Modules.Retentions.Events;
using FluentAssertions;

namespace ERP.Domain.Tests.Retentions;

public sealed class RetentionDocumentTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid BranchId = Guid.NewGuid();
    private static readonly Guid SourceDocumentId = Guid.NewGuid();
    private static readonly Guid SubjectBusinessPartnerId = Guid.NewGuid();
    private static readonly Guid EmissionPointId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private static RetentionDocument CreateDraftDocument() =>
        RetentionDocument.Create(
            TenantId,
            CompanyId,
            BranchId,
            RetentionSourceDocumentType.ExpenseDocument,
            SourceDocumentId,
            SubjectBusinessPartnerId,
            EmissionPointId,
            UserId
        );

    private static RetentionDocumentLine CreateVatLine(
        RetentionDocument document,
        decimal baseAmount = 100m,
        decimal rate = 70m,
        decimal retained = 10.50m
    ) =>
        RetentionDocumentLine.Create(
            document.Id,
            TenantId,
            RetentionTaxType.Vat,
            "721",
            baseAmount,
            rate,
            retained
        );

    private static RetentionDocumentLine CreateIncomeLine(
        RetentionDocument document,
        decimal baseAmount = 100m,
        decimal rate = 1m,
        decimal retained = 1m
    ) =>
        RetentionDocumentLine.Create(
            document.Id,
            TenantId,
            RetentionTaxType.Income,
            "303",
            baseAmount,
            rate,
            retained
        );

    // 1. Create con IDs válidos inicia Draft y totales 0.
    [Fact]
    public void Create_con_ids_validos_inicia_Draft_y_totales_cero()
    {
        var document = CreateDraftDocument();

        document.Status.Should().Be(RetentionStatus.Draft);
        document.TotalRetainedVat.Should().Be(0m);
        document.TotalRetainedIncome.Should().Be(0m);
        document.TotalRetained.Should().Be(0m);
        document.Lines.Should().BeEmpty();
    }

    // 2. Create con TenantId empty falla.
    [Fact]
    public void Create_con_TenantId_empty_falla()
    {
        var act = () =>
            RetentionDocument.Create(
                Guid.Empty,
                CompanyId,
                BranchId,
                RetentionSourceDocumentType.ExpenseDocument,
                SourceDocumentId,
                SubjectBusinessPartnerId,
                EmissionPointId,
                UserId
            );

        act.Should().Throw<ArgumentException>().WithMessage("*tenant*");
    }

    // 3. Create con CompanyId empty falla.
    [Fact]
    public void Create_con_CompanyId_empty_falla()
    {
        var act = () =>
            RetentionDocument.Create(
                TenantId,
                Guid.Empty,
                BranchId,
                RetentionSourceDocumentType.ExpenseDocument,
                SourceDocumentId,
                SubjectBusinessPartnerId,
                EmissionPointId,
                UserId
            );

        act.Should().Throw<ArgumentException>().WithMessage("*empresa*");
    }

    // 4. Create con BranchId empty falla.
    [Fact]
    public void Create_con_BranchId_empty_falla()
    {
        var act = () =>
            RetentionDocument.Create(
                TenantId,
                CompanyId,
                Guid.Empty,
                RetentionSourceDocumentType.ExpenseDocument,
                SourceDocumentId,
                SubjectBusinessPartnerId,
                EmissionPointId,
                UserId
            );

        act.Should().Throw<ArgumentException>().WithMessage("*sucursal*");
    }

    // 5. AddLine válida (Vat) recalcula TotalRetainedVat.
    [Fact]
    public void AddLine_valida_Vat_recalcula_TotalRetainedVat()
    {
        var document = CreateDraftDocument();
        var line = CreateVatLine(document, retained: 10.50m);

        document.AddLine(line);

        document.TotalRetainedVat.Should().Be(10.50m);
        document.TotalRetainedIncome.Should().Be(0m);
        document.TotalRetained.Should().Be(10.50m);
    }

    // 6. AddLine válida (Income) recalcula TotalRetainedIncome.
    [Fact]
    public void AddLine_valida_Income_recalcula_TotalRetainedIncome()
    {
        var document = CreateDraftDocument();
        var line = CreateIncomeLine(document, retained: 1m);

        document.AddLine(line);

        document.TotalRetainedIncome.Should().Be(1m);
        document.TotalRetainedVat.Should().Be(0m);
        document.TotalRetained.Should().Be(1m);
    }

    // 7. AddLine con código vacío falla.
    [Fact]
    public void AddLine_con_codigo_vacio_falla()
    {
        var document = CreateDraftDocument();

        var act = () =>
            RetentionDocumentLine.Create(
                document.Id,
                TenantId,
                RetentionTaxType.Vat,
                "   ",
                100m,
                70m,
                10m
            );

        act.Should().Throw<ArgumentException>().WithMessage("*código de retención*");
    }

    // 8. AddLine con BaseAmount <= 0 falla.
    [Fact]
    public void AddLine_con_BaseAmount_menor_o_igual_a_cero_falla()
    {
        var document = CreateDraftDocument();

        var act = () =>
            RetentionDocumentLine.Create(
                document.Id,
                TenantId,
                RetentionTaxType.Vat,
                "721",
                0m,
                70m,
                10m
            );

        act.Should().Throw<ArgumentException>().WithMessage("*base imponible*");
    }

    // 9. AddLine con RetentionRate <= 0 falla.
    [Fact]
    public void AddLine_con_RetentionRate_menor_o_igual_a_cero_falla()
    {
        var document = CreateDraftDocument();

        var act = () =>
            RetentionDocumentLine.Create(
                document.Id,
                TenantId,
                RetentionTaxType.Vat,
                "721",
                100m,
                0m,
                10m
            );

        act.Should().Throw<ArgumentException>().WithMessage("*porcentaje de retención*");
    }

    // 10. AddLine con RetainedAmount <= 0 falla.
    [Fact]
    public void AddLine_con_RetainedAmount_menor_o_igual_a_cero_falla()
    {
        var document = CreateDraftDocument();

        var act = () =>
            RetentionDocumentLine.Create(
                document.Id,
                TenantId,
                RetentionTaxType.Vat,
                "721",
                100m,
                70m,
                0m
            );

        act.Should().Throw<ArgumentException>().WithMessage("*monto retenido*");
    }

    // 11. AddLine con RetainedAmount > BaseAmount falla.
    [Fact]
    public void AddLine_con_RetainedAmount_mayor_a_BaseAmount_falla()
    {
        var document = CreateDraftDocument();

        var act = () =>
            RetentionDocumentLine.Create(
                document.Id,
                TenantId,
                RetentionTaxType.Vat,
                "721",
                100m,
                70m,
                150m
            );

        act.Should().Throw<ArgumentException>().WithMessage("*no puede ser mayor*");
    }

    // 12. Issue sin líneas falla.
    [Fact]
    public void Issue_sin_lineas_falla()
    {
        var document = CreateDraftDocument();

        var act = () => document.Issue("001-001-000000001", new DateOnly(2026, 9, 3), UserId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*sin líneas*");
    }

    // 13. Issue con número vacío falla.
    [Fact]
    public void Issue_con_numero_vacio_falla()
    {
        var document = CreateDraftDocument();
        document.AddLine(CreateVatLine(document));

        var act = () => document.Issue("   ", new DateOnly(2026, 9, 3), UserId);

        act.Should().Throw<ArgumentException>().WithMessage("*número de retención*");
    }

    // 14. Issue con líneas válidas cambia a Issued y levanta RetentionDocumentIssuedEvent.
    [Fact]
    public void Issue_con_lineas_validas_cambia_a_Issued_y_levanta_evento()
    {
        var document = CreateDraftDocument();
        document.AddLine(CreateVatLine(document, retained: 10.50m));
        var issueDate = new DateOnly(2026, 9, 3);

        document.Issue("001-001-000000001", issueDate, UserId);

        document.Status.Should().Be(RetentionStatus.Issued);
        document.RetentionNumber.Should().Be("001-001-000000001");
        document.IssueDate.Should().Be(issueDate);

        var raised = document.DomainEvents.OfType<RetentionDocumentIssuedEvent>().Single();
        raised.RetentionDocumentId.Should().Be(document.Id);
        raised.TotalRetained.Should().Be(10.50m);
        raised.RetentionNumber.Should().Be("001-001-000000001");
    }

    // 15. Issue dos veces falla.
    [Fact]
    public void Issue_dos_veces_falla()
    {
        var document = CreateDraftDocument();
        document.AddLine(CreateVatLine(document));
        document.Issue("001-001-000000001", new DateOnly(2026, 9, 3), UserId);

        var act = () => document.Issue("001-001-000000002", new DateOnly(2026, 9, 3), UserId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*borrador*");
    }

    // 16. AddLine después de Issued falla.
    [Fact]
    public void AddLine_despues_de_Issued_falla()
    {
        var document = CreateDraftDocument();
        document.AddLine(CreateVatLine(document));
        document.Issue("001-001-000000001", new DateOnly(2026, 9, 3), UserId);

        var act = () => document.AddLine(CreateIncomeLine(document));

        act.Should().Throw<InvalidOperationException>().WithMessage("*borrador*");
    }

    // 17. Cancel Draft falla.
    [Fact]
    public void Cancel_sobre_Draft_falla()
    {
        var document = CreateDraftDocument();
        document.AddLine(CreateVatLine(document));

        var act = () => document.Cancel("Error", UserId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*emitidas*");
    }

    // 18. Cancel Issued con motivo válido cambia a Cancelled y levanta RetentionDocumentCancelledEvent.
    [Fact]
    public void Cancel_Issued_con_motivo_valido_cambia_a_Cancelled_y_levanta_evento()
    {
        var document = CreateDraftDocument();
        document.AddLine(CreateVatLine(document, retained: 10.50m));
        document.Issue("001-001-000000001", new DateOnly(2026, 9, 3), UserId);

        document.Cancel("Error de digitación", UserId);

        document.Status.Should().Be(RetentionStatus.Cancelled);
        document.CancelReason.Should().Be("Error de digitación");
        document.CancelledBy.Should().Be(UserId);
        document.CancelledAt.Should().NotBeNull();

        var raised = document.DomainEvents.OfType<RetentionDocumentCancelledEvent>().Single();
        raised.RetentionDocumentId.Should().Be(document.Id);
        raised.CancelReason.Should().Be("Error de digitación");
        raised.TotalRetained.Should().Be(10.50m);
    }

    // 19. Cancel sin motivo falla.
    [Fact]
    public void Cancel_sin_motivo_falla()
    {
        var document = CreateDraftDocument();
        document.AddLine(CreateVatLine(document));
        document.Issue("001-001-000000001", new DateOnly(2026, 9, 3), UserId);

        var act = () => document.Cancel("   ", UserId);

        act.Should().Throw<ArgumentException>().WithMessage("*motivo de anulación*");
    }

    // 20. Cancel dos veces falla.
    [Fact]
    public void Cancel_dos_veces_falla()
    {
        var document = CreateDraftDocument();
        document.AddLine(CreateVatLine(document));
        document.Issue("001-001-000000001", new DateOnly(2026, 9, 3), UserId);
        document.Cancel("Error de digitación", UserId);

        var act = () => document.Cancel("Otro motivo", UserId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*emitidas*");
    }

    // 21. Issue sobre Cancelled falla.
    [Fact]
    public void Issue_sobre_Cancelled_falla()
    {
        var document = CreateDraftDocument();
        document.AddLine(CreateVatLine(document));
        document.Issue("001-001-000000001", new DateOnly(2026, 9, 3), UserId);
        document.Cancel("Error de digitación", UserId);

        var act = () => document.Issue("001-001-000000002", new DateOnly(2026, 9, 3), UserId);

        act.Should().Throw<InvalidOperationException>().WithMessage("*borrador*");
    }

    // 22. Totales separan Vat/Income correctamente.
    [Fact]
    public void Totales_separan_Vat_e_Income_correctamente()
    {
        var document = CreateDraftDocument();
        document.AddLine(CreateVatLine(document, baseAmount: 100m, rate: 70m, retained: 10.50m));
        document.AddLine(CreateIncomeLine(document, baseAmount: 200m, rate: 1m, retained: 2m));

        document.TotalRetainedVat.Should().Be(10.50m);
        document.TotalRetainedIncome.Should().Be(2m);
        document.TotalRetained.Should().Be(12.50m);
    }
}
