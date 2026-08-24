using ERP.Domain.Modules.InitialLoad.Entities;
using ERP.Domain.Modules.InitialLoad.Enums;
using FluentAssertions;

namespace ERP.Domain.Tests.InitialLoad;

public sealed class ImportBatchTests
{
    private static ImportBatch CreateDraftBatch() =>
        ImportBatch.Create(Guid.NewGuid(), Guid.NewGuid(), ImportType.Customers, Guid.NewGuid());

    private static ImportBatch CreateUploadedBatch()
    {
        var batch = CreateDraftBatch();
        batch.AttachFile("some/path.xlsx", "clientes.xlsx", 1024, Guid.NewGuid());
        batch.MarkUploaded(Guid.NewGuid());
        return batch;
    }

    private static ImportBatch CreateValidatedBatch(int total, int valid, int issueRows, int warningRows)
    {
        var batch = CreateUploadedBatch();
        batch.BeginValidating(Guid.NewGuid());
        batch.CompleteValidation(total, valid, issueRows, warningRows, Guid.NewGuid());
        return batch;
    }

    [Fact]
    public void Create_inicia_en_Draft()
    {
        var batch = CreateDraftBatch();
        batch.Status.Should().Be(ImportStatus.Draft);
    }

    [Fact]
    public void MarkUploaded_sin_archivos_lanza()
    {
        var batch = CreateDraftBatch();
        var act = () => batch.MarkUploaded(Guid.NewGuid());
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Flujo_completo_hasta_Completed_cuando_todas_las_filas_se_importan()
    {
        var batch = CreateValidatedBatch(total: 3, valid: 3, issueRows: 0, warningRows: 0);

        batch.BeginConfirming(Guid.NewGuid());
        batch.CompleteConfirmation(importedRows: 3, anyRowsFailed: false, Guid.NewGuid());

        batch.Status.Should().Be(ImportStatus.Completed);
        batch.ImportedRows.Should().Be(3);
    }

    [Fact]
    public void Confirmacion_parcial_deja_el_lote_en_PartiallyCompleted()
    {
        // Diseño parcial-seguro (INITIAL-LOAD-ARCH-01): filas bloqueadas nunca se confirman,
        // y una falla de confirmación en filas individuales no aborta el lote — el estado
        // final refleja que no todo lo esperado se importó.
        var batch = CreateValidatedBatch(total: 5, valid: 4, issueRows: 1, warningRows: 0);

        batch.BeginConfirming(Guid.NewGuid());
        batch.CompleteConfirmation(importedRows: 3, anyRowsFailed: true, Guid.NewGuid());

        batch.Status.Should().Be(ImportStatus.PartiallyCompleted);
        batch.ImportedRows.Should().Be(3);
    }

    [Fact]
    public void Cancel_no_es_posible_desde_Confirming()
    {
        var batch = CreateValidatedBatch(total: 1, valid: 1, issueRows: 0, warningRows: 0);
        batch.BeginConfirming(Guid.NewGuid());

        var act = () => batch.Cancel(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_es_posible_desde_Validated()
    {
        var batch = CreateValidatedBatch(total: 1, valid: 1, issueRows: 0, warningRows: 0);

        batch.Cancel(Guid.NewGuid());

        batch.Status.Should().Be(ImportStatus.Cancelled);
        batch.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public void BeginConfirming_requiere_estado_Validated()
    {
        var batch = CreateUploadedBatch();

        var act = () => batch.BeginConfirming(Guid.NewGuid());

        act.Should().Throw<InvalidOperationException>();
    }
}
