using ERP.API.Tests.Support;
using ERP.Application.Common;
using ERP.Application.MasterData.UseCases.CreateBusinessPartner;
using ERP.Application.MasterData.UseCases.UpsertCompanyBpSettings;
using ERP.Domain.Access.Entities;
using ERP.Domain.MasterData.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Subscribers.Entities;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.API.Tests.Integration;

/// <summary>
/// Concurrencia real en PostgreSQL (Testcontainers). Requiere Docker.
/// </summary>
[Trait("Category", "PostgreSql")]
public sealed class PostgreSqlConcurrencyIntegrationTests : IAsyncLifetime
{
    private PostgreSqlTestWebAppFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new PostgreSqlTestWebAppFactory();
        await _factory.InitializeAsync();
        await _factory.MigrateAsync();
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task PG_concurrent_UpsertCompanyBpSettings_one_wins_other_returns_conflict_not_500()
    {
        var (subscriberId, companyId, userId, bpId) = await SeedTenantWithBusinessPartnerAsync();

        _factory.MutableSubscriber.SubscriberId = subscriberId;
        _factory.MutableCompany.CompanyId = companyId;
        _factory.MutableUser.UserId = userId;
        _factory.MutableUser.Role = "Admin";

        var command = new UpsertCompanyBpSettingsCommand(bpId, 5000m, 30, false);

        var results = await Task.WhenAll(
            SendUpsertAsync(command),
            SendUpsertAsync(command));

        results.Count(r => r.IsSuccess).Should().Be(1);
        results.Count(r => !r.IsSuccess).Should().Be(1);
        foreach (var failed in results.Where(r => !r.IsSuccess))
        {
            failed.ErrorCode.Should().BeOneOf(
                ResultErrorCodes.UniqueViolation,
                ResultErrorCodes.Conflict);
        }
    }

    [Fact]
    public async Task PG_concurrent_CreateBusinessPartner_same_identification_one_wins_other_conflict()
    {
        var (subscriberId, _, userId, _) = await SeedTenantWithBusinessPartnerAsync();

        _factory.MutableSubscriber.SubscriberId = subscriberId;
        _factory.MutableCompany.CompanyId = Guid.Empty;
        _factory.MutableUser.UserId = userId;

        var command = new CreateBusinessPartnerCommand(
            "RUC",
            "1790099999001",
            "Concurrent BP SA",
            null,
            null,
            null,
            null,
            AsCustomer: true,
            AsSupplier: false);

        var results = await Task.WhenAll(
            SendCreateBpAsync(command),
            SendCreateBpAsync(command));

        results.Count(r => r.IsSuccess).Should().Be(1);
        results.Count(r => !r.IsSuccess).Should().Be(1);
        foreach (var failed in results.Where(r => !r.IsSuccess))
        {
            failed.ErrorCode.Should().BeOneOf(
                ResultErrorCodes.UniqueViolation,
                ResultErrorCodes.Conflict,
                ResultErrorCodes.ValidationFailure);
        }
    }

    private async Task<Result<bool>> SendUpsertAsync(UpsertCompanyBpSettingsCommand command)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(command);
    }

    private async Task<Result<ERP.Application.MasterData.DTOs.BusinessPartnerDto>> SendCreateBpAsync(
        CreateBusinessPartnerCommand command)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(command);
    }

    private async Task<(Guid SubscriberId, Guid CompanyId, Guid UserId, Guid BusinessPartnerId)>
        SeedTenantWithBusinessPartnerAsync()
    {
        var userId = Guid.NewGuid();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        await TestDataFactory.SeedCommercialPlansAndLimitsAsync(db);

        var subscriber = Subscriber.Create("PG-Conc", "pg-conc", userId);
        db.Subscribers.Add(subscriber);
        await db.SaveChangesAsync();

        var company = Company.CreateFromSubscriber(
            subscriber.Id, "1790016919002", "PG Conc Company", "Av. Test");
        db.Companies.Add(company);

        var user = IdentityUser.CreateCompanyUser(
            "PG", "Test", "pg-conc@test.local",
            "$2a$11$placeholder.hash.placeholder.hash.placeholder.hash0000", userId);
        db.IdentityUsers.Add(user);

        db.CompanyUserMemberships.Add(
            CompanyUserMembership.Create(company.Id, user.Id, "Admin", null, user.Id));

        var bp = BusinessPartner.Create(
            subscriber.Id, "RUC", "1790016919003", "BP Seed", user.Id);
        db.BusinessPartners.Add(bp);

        await db.SaveChangesAsync();
        return (subscriber.Id, company.Id, user.Id, bp.Id);
    }
}
