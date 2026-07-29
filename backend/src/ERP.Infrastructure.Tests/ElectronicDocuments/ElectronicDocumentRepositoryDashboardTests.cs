using ERP.Application.Common;
using ERP.Application.Common.Services;
using ERP.Domain.Modules.ElectronicDocuments.Entities;
using ERP.Domain.Modules.ElectronicDocuments.Enums;
using ERP.Infrastructure.Persistence;
using ERP.Infrastructure.Persistence.Repositories.ElectronicDocuments;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using System.Reflection;

namespace ERP.Infrastructure.Tests.ElectronicDocuments;

public sealed class ElectronicDocumentRepositoryDashboardTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _companyId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task Today_counts_use_the_company_business_day_utc_range()
    {
        var startUtc = new DateTime(2026, 7, 13, 5, 0, 0, DateTimeKind.Utc);
        var endUtc = new DateTime(2026, 7, 14, 5, 0, 0, DateTimeKind.Utc);

        await using var db = NewDbContext();
        db.ElectronicDocuments.AddRange(
            NewDocument(
                ElectronicDocumentState.Authorized,
                createdAt: endUtc,
                authorizationDate: endUtc.AddTicks(-1)),
            NewDocument(
                ElectronicDocumentState.Authorized,
                createdAt: endUtc,
                authorizationDate: endUtc),
            NewDocument(
                ElectronicDocumentState.Failed,
                createdAt: endUtc,
                updatedAt: startUtc),
            NewDocument(
                ElectronicDocumentState.Rejected,
                createdAt: endUtc,
                updatedAt: startUtc.AddTicks(-1)),
            NewDocument(
                ElectronicDocumentState.Draft,
                createdAt: startUtc),
            NewDocument(
                ElectronicDocumentState.Draft,
                createdAt: endUtc));
        await db.SaveChangesAsync();

        var companyClock = new Mock<ICompanyClock>();
        companyClock
            .Setup(c => c.TodayUtcRangeAsync(_companyId, _tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((startUtc, endUtc));

        var repository = new ElectronicDocumentRepository(db, companyClock.Object);

        var authorizedToday = await repository.CountAuthorizedTodayAsync(_tenantId, _companyId);
        var errorsToday = await repository.CountErrorsTodayAsync(_tenantId, _companyId);
        var createdToday = await repository.CountCreatedTodayAsync(_tenantId, _companyId);

        authorizedToday.Should().Be(1);
        errorsToday.Should().Be(1);
        createdToday.Should().Be(1);
        companyClock.Verify(
            c => c.TodayUtcRangeAsync(_companyId, _tenantId, It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    private ErpDbContext NewDbContext()
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ErpDbContext(
            options, new FixedCurrentTenant(_tenantId), new NoOpPublisher(), new FixedCurrentCompany(_companyId));
    }

    private ElectronicDocument NewDocument(
        ElectronicDocumentState state,
        DateTime createdAt,
        DateTime? updatedAt = null,
        DateTime? authorizationDate = null)
    {
        var document = ElectronicDocument.Create(
            _tenantId,
            _companyId,
            ElectronicDocumentType.Invoice,
            "Sales",
            Guid.NewGuid(),
            _userId);

        SetProperty(document, nameof(ElectronicDocument.CurrentState), state);
        SetProperty(document, nameof(ElectronicDocument.CreatedAt), createdAt);
        SetProperty(document, nameof(ElectronicDocument.UpdatedAt), updatedAt);
        SetProperty(document, nameof(ElectronicDocument.AuthorizationDate), authorizationDate);
        return document;
    }

    private static void SetProperty(ElectronicDocument document, string propertyName, object? value)
    {
        var property = FindProperty(typeof(ElectronicDocument), propertyName);
        property.SetMethod!.Invoke(document, new[] { value });
    }

    private static PropertyInfo FindProperty(Type type, string propertyName)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var property = current.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (property is not null)
                return property;
        }

        throw new InvalidOperationException($"Property '{propertyName}' was not found.");
    }

    private sealed class FixedCurrentTenant(Guid tenantId) : ICurrentTenant
    {
        public Guid TenantId => tenantId;
        public string? Slug => null;
    }

    private sealed class FixedCurrentCompany(Guid companyId) : ICurrentCompany
    {
        public Guid CompanyId => companyId;
        public bool IsAuthenticated => true;
        public bool HasCompanyContext => true;
    }

    private sealed class NoOpPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.CompletedTask;
    }
}
