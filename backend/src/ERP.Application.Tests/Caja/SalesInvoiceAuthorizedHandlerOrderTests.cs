using ERP.Application.Modules.Accounting.Posting.Translators;
using ERP.Application.Modules.Caja.UseCases;
using ERP.Domain.Modules.Sales.Events;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Application.Tests.Caja;

/// <summary>
/// ZH-SALES-CASH-CONCURRENCY-HARDENING-01 — orden único de locks: el handler de Caja bloquea la
/// CashSession (FOR UPDATE) ANTES de que los traductores de Accounting tomen sus advisory locks
/// (idempotencia por evento y secuencia de asientos por empresa/ejercicio). Es el mismo orden que ya
/// usan pago a proveedor, reembolso y solicitud de efectivo (CashSession → posting); invertirlo
/// permitiría un deadlock entre una venta y esos flujos. MediatR publica en orden de registro, que
/// sale del escaneo del ensamblado de producción (ERP.Application/DependencyInjection.cs).
/// </summary>
public sealed class SalesInvoiceAuthorizedHandlerOrderTests
{
    [Fact]
    public void Caja_se_registra_antes_que_los_traductores_contables_del_mismo_evento()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(SalesInvoiceAuthorizedHandler).Assembly));

        var order = services
            .Where(d => d.ServiceType == typeof(INotificationHandler<SalesInvoiceAuthorizedEvent>))
            .Select(d => d.ImplementationType)
            .ToList();

        order.Should().Contain(typeof(SalesInvoiceAuthorizedPostingTranslator));
        order.Should().Contain(typeof(SalesInvoiceCogsPostingTranslator));
        order.IndexOf(typeof(SalesInvoiceAuthorizedHandler)).Should().Be(0);
    }
}
