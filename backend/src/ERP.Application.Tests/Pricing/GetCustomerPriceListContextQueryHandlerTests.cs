using ERP.Application.Modules.Pricing.Services;
using ERP.Application.Modules.Pricing.UseCases.CustomerPriceListContext;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.Pricing;

/// <summary>
/// SALES-PRICING-UX-TRACEABILITY-07C: el contexto separa SIEMPRE la lista propia del cliente de la
/// default de la empresa — la default nunca se presenta como lista del cliente.
/// </summary>
public sealed class GetCustomerPriceListContextQueryHandlerTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();

    private static GetCustomerPriceListContextQueryHandler Build(
        params PriceListSelectionResult[] candidates
    )
    {
        var selection = new Mock<IPriceListSelectionResolver>();
        selection
            .Setup(s => s.ResolveAsync(CustomerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidates);
        return new GetCustomerPriceListContextQueryHandler(selection.Object);
    }

    [Fact]
    public async Task Cliente_con_lista_propia_y_default_devuelve_ambas_separadas()
    {
        var mayorista = Guid.NewGuid();
        var general = Guid.NewGuid();
        var handler = Build(
            new PriceListSelectionResult(mayorista, "MAYORISTA001", PriceListSelectionSource.Customer),
            new PriceListSelectionResult(general, "Lista General", PriceListSelectionSource.CompanyDefault)
        );

        var result = await handler.Handle(new GetCustomerPriceListContextQuery(CustomerId), default);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.CustomerPriceListId.Should().Be(mayorista);
        result.Value.CustomerPriceListName.Should().Be("MAYORISTA001");
        result.Value.CompanyDefaultPriceListId.Should().Be(general);
        result.Value.CompanyDefaultPriceListName.Should().Be("Lista General");
    }

    [Fact]
    public async Task Cliente_sin_lista_propia_no_recibe_la_default_como_suya()
    {
        var general = Guid.NewGuid();
        var handler = Build(
            new PriceListSelectionResult(general, "Lista General", PriceListSelectionSource.CompanyDefault)
        );

        var result = await handler.Handle(new GetCustomerPriceListContextQuery(CustomerId), default);

        result.Value!.CustomerPriceListId.Should().BeNull();
        result.Value.CustomerPriceListName.Should().BeNull();
        result.Value.CompanyDefaultPriceListName.Should().Be("Lista General");
    }

    [Fact]
    public async Task Lista_propia_que_es_tambien_la_default_se_reporta_solo_como_del_cliente()
    {
        var shared = Guid.NewGuid();
        var handler = Build(
            new PriceListSelectionResult(shared, "MAYORISTA001", PriceListSelectionSource.Customer)
        );

        var result = await handler.Handle(new GetCustomerPriceListContextQuery(CustomerId), default);

        result.Value!.CustomerPriceListId.Should().Be(shared);
        result.Value.CompanyDefaultPriceListId.Should().BeNull();
    }

    [Fact]
    public async Task Sin_candidatos_devuelve_todo_null()
    {
        var result = await Build().Handle(new GetCustomerPriceListContextQuery(CustomerId), default);

        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value.Should().Be(new CustomerPriceListContextDto(null, null, null, null));
    }

    [Fact]
    public async Task Resuelve_una_sola_vez_con_el_cliente_pedido_scope_ambiente_lo_aplica_el_resolver()
    {
        var selection = new Mock<IPriceListSelectionResolver>();
        selection
            .Setup(s => s.ResolveAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<PriceListSelectionResult>());
        var handler = new GetCustomerPriceListContextQueryHandler(selection.Object);

        await handler.Handle(new GetCustomerPriceListContextQuery(CustomerId), default);

        selection.Verify(s => s.ResolveAsync(CustomerId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
