using ERP.Application.Access.Caching;
using Moq;

namespace ERP.Application.Tests.Access;

/// <summary>SEC-06 — invalidador cuyo protocolo (Begin/Complete) siempre puede garantizarse.</summary>
internal static class PermissionsCacheInvalidatorMocks
{
    public static Mock<IPermissionsCacheInvalidator> Healthy()
    {
        var mock = new Mock<IPermissionsCacheInvalidator>();
        mock.Setup(c =>
                c.TryBeginAsync(It.IsAny<PermissionsCacheScope>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(true);
        mock.Setup(c =>
                c.TryCompleteAsync(It.IsAny<PermissionsCacheScope>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(true);
        return mock;
    }
}
