using ERP.Application.Common;
using ERP.Application.Common.Interfaces;
using ERP.Application.Modules.ElectronicInvoicing.UseCases.UpsertSriConfiguration;
using ERP.Domain.Configuration.Entities;
using ERP.Domain.Configuration.Interfaces;
using FluentAssertions;
using Moq;

namespace ERP.Application.Tests.ElectronicInvoicing;

/// <summary>
/// CONFIG-FOUNDATION-P2-01 — UpsertSriConfigurationCommandHandler audita Environment/EmissionType/
/// WsdlUrl con valores en claro (no son secretos) y CertPassword como Masked (nunca el valor real).
/// </summary>
public sealed class UpsertSriConfigurationCommandHandlerTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid CompanyId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class Fixture
    {
        public Mock<ISriSettingsRepository> Repo { get; } = new();
        public Mock<IConfigurationChangeLogger> ChangeLogger { get; } = new();
        public Mock<ICurrentTenant> Tenant { get; } = new();
        public Mock<ICurrentCompany> Company { get; } = new();
        public Mock<ICurrentUser> User { get; } = new();
        public Mock<ISecretProtector> SecretProtector { get; } = new();

        public Fixture()
        {
            Tenant.Setup(t => t.TenantId).Returns(TenantId);
            Company.Setup(c => c.CompanyId).Returns(CompanyId);
            User.Setup(u => u.UserId).Returns(UserId);
            SecretProtector.Setup(p => p.Protect(It.IsAny<string>())).Returns("dp1:encrypted");
        }

        public UpsertSriConfigurationCommandHandler BuildHandler() =>
            new(
                Repo.Object,
                ChangeLogger.Object,
                Tenant.Object,
                Company.Object,
                User.Object,
                SecretProtector.Object
            );
    }

    [Fact]
    public async Task Primera_configuracion_audita_environment_emissiontype_wsdlurl_como_null_a_valor()
    {
        var f = new Fixture();
        f.Repo
            .Setup(r => r.GetByCompanyIdAsync(CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SriSettings?)null);

        await f.BuildHandler()
            .Handle(
                new UpsertSriConfigurationCommand(null, 1, 1, "https://wsdl.example/test"),
                default
            );

        f.ChangeLogger.Verify(
            l =>
                l.LogAsync(
                    It.Is<ConfigurationChangeLogEntry>(e =>
                        e.EntityType == "SriSettings" && e.FieldName == "Environment" && e.OldValue == null
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        f.ChangeLogger.Verify(
            l =>
                l.LogAsync(
                    It.Is<ConfigurationChangeLogEntry>(e =>
                        e.FieldName == "WsdlUrl" && e.NewValue == "https://wsdl.example/test"
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Cambiar_ambiente_en_configuracion_existente_genera_log()
    {
        var f = new Fixture();
        var existing = SriSettings.Create(TenantId, CompanyId, 1, 1, "https://wsdl.example/test", UserId);
        f.Repo
            .Setup(r => r.GetByCompanyIdAsync(CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await f.BuildHandler()
            .Handle(
                new UpsertSriConfigurationCommand(null, 2, 1, "https://wsdl.example/test"),
                default
            );

        f.ChangeLogger.Verify(
            l =>
                l.LogAsync(
                    It.Is<ConfigurationChangeLogEntry>(e =>
                        e.FieldName == "Environment" && e.OldValue == "1" && e.NewValue == "2"
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Cambiar_password_registra_Masked_nunca_el_valor_real()
    {
        var f = new Fixture();
        var existing = SriSettings.Create(TenantId, CompanyId, 1, 1, "https://wsdl.example/test", UserId);
        f.Repo
            .Setup(r => r.GetByCompanyIdAsync(CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        const string realSecret = "mi-password-super-secreto";
        await f.BuildHandler()
            .Handle(
                new UpsertSriConfigurationCommand(realSecret, 1, 1, "https://wsdl.example/test"),
                default
            );

        f.ChangeLogger.Verify(
            l =>
                l.LogAsync(
                    It.Is<ConfigurationChangeLogEntry>(e =>
                        e.FieldName == "CertPassword"
                        && e.ValueType == ERP.Domain.Configuration.Enums.ConfigurationChangeValueType.Masked
                        && e.IsSensitive
                        && (e.OldValue == null || !e.OldValue.Contains(realSecret))
                        && (e.NewValue == null || !e.NewValue.Contains(realSecret))
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task No_cambiar_nada_no_genera_ningun_log()
    {
        var f = new Fixture();
        var existing = SriSettings.Create(TenantId, CompanyId, 1, 1, "https://wsdl.example/test", UserId);
        f.Repo
            .Setup(r => r.GetByCompanyIdAsync(CompanyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await f.BuildHandler()
            .Handle(
                new UpsertSriConfigurationCommand(null, 1, 1, "https://wsdl.example/test"),
                default
            );

        f.ChangeLogger.Verify(
            l => l.LogAsync(It.IsAny<ConfigurationChangeLogEntry>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }
}
