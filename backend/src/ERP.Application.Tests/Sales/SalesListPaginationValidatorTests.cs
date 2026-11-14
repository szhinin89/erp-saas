using ERP.Application.Behaviors;
using ERP.Application.Common;
using ERP.Application.Modules.Sales.UseCases;
using FluentAssertions;
using FluentValidation;
using MediatR;

namespace ERP.Application.Tests.Sales;

/// <summary>
/// API-DOC-F-17: pageNumber/pageSize fuera de rango en SalesReceivables y SalesReturn se rechazan
/// antes de llegar a la base (antes: OFFSET/LIMIT negativo en PostgreSQL → error de servidor).
/// Los valores que envía el frontend (25 y 50, página 1) siguen siendo válidos.
/// </summary>
public sealed class SalesListPaginationValidatorTests
{
    [Theory]
    [InlineData(1, 25, true)]
    [InlineData(1, 50, true)]
    [InlineData(3, 1, true)]
    [InlineData(1, 100, true)]
    [InlineData(0, 25, false)]
    [InlineData(-1, 25, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, -5, false)]
    [InlineData(1, 101, false)]
    public void Receivables_valida_rango(int page, int size, bool valid) =>
        new GetReceivablesListQueryValidator()
            .Validate(new GetReceivablesListQuery(null, null, page, size))
            .IsValid.Should().Be(valid);

    [Theory]
    [InlineData(1, 25, true)]
    [InlineData(1, 100, true)]
    [InlineData(0, 25, false)]
    [InlineData(1, -5, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 101, false)]
    public void Returns_valida_rango(int page, int size, bool valid) =>
        new GetSalesReturnListQueryValidator()
            .Validate(new GetSalesReturnListQuery(null, null, page, size))
            .IsValid.Should().Be(valid);

    [Theory]
    [InlineData("pending")]
    [InlineData("cancelled")]
    [InlineData("desconocido")]
    public void Status_no_se_valida_en_ninguno_de_los_dos_listados(string status)
    {
        new GetReceivablesListQueryValidator()
            .Validate(new GetReceivablesListQuery(null, status)).IsValid.Should().BeTrue();
        new GetSalesReturnListQueryValidator()
            .Validate(new GetSalesReturnListQuery(null, status)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Pipeline_corta_antes_del_handler_con_ValidationException()
    {
        var behavior = new ValidationBehavior<GetReceivablesListQuery, Result<ReceivablesListResponse>>(
            new IValidator<GetReceivablesListQuery>[] { new GetReceivablesListQueryValidator() });
        var handlerCalled = false;

        var act = () => behavior.Handle(
            new GetReceivablesListQuery(null, null, 0, 25),
            _ => { handlerCalled = true; throw new InvalidOperationException(); },
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        handlerCalled.Should().BeFalse();
    }
}
