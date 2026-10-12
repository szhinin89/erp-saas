using ERP.Application.Common;
using Moq;

namespace ERP.Application.Tests.TestSupport;

/// <summary>
/// Doble de <see cref="IUnitOfWork"/> sin BD: <see cref="IUnitOfWork.ExecuteInTransactionAsync"/>
/// ejecuta la operación en línea y propaga sus excepciones (mismo contrato observable que la
/// transacción real, sin commit/rollback). La atomicidad real se prueba contra PostgreSQL.
/// </summary>
public static class UnitOfWorkTestDouble
{
    public static Mock<IUnitOfWork> PassThrough()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork
            .Setup(u =>
                u.ExecuteInTransactionAsync(
                    It.IsAny<Func<CancellationToken, Task>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<Func<CancellationToken, Task>, CancellationToken>(
                async (operation, ct) => await operation(ct)
            );
        return unitOfWork;
    }
}
