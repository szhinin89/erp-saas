using ERP.Application.Modules.Accounting.UseCases.JournalEntries;
using ERP.Domain.Modules.Accounting.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ERP.Infrastructure.Tests.Accounting;

/// <summary>
/// F-14 (API-DOC-V1): el reintento idempotente de <c>Create</c> representa el asiento existente
/// (estado, versión y número reales) sin crear, contabilizar ni modificar nada. PostgreSQL real
/// (fixture desechable compartido con AccountingManual08Tests).
/// </summary>
public sealed class ManualJournalIdempotentCreateReplayTests : IClassFixture<AccountingManual08Fixture>
{
    private readonly AccountingManual08Fixture _fixture;
    public ManualJournalIdempotentCreateReplayTests(AccountingManual08Fixture fixture) => _fixture = fixture;
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Reintento_sin_cambios_conserva_identidad_y_respuesta_y_no_escribe()
    {
        var book = await _fixture.BookAsync();
        await using var db = book.Db();
        var handler = book.Handler(db);
        var create = book.Create();
        var created = (await handler.Handle(create, Ct)).Value!;
        var before = await SnapshotAsync(book, created.Id);

        var replay = (await handler.Handle(create, Ct)).Value!;

        replay.Should().Be(created);
        replay.Status.Should().Be("Draft");
        replay.Version.Should().Be(1);
        replay.EntryNumber.Should().BeNull();
        (await SnapshotAsync(book, created.Id)).Should().Be(before, "el reintento no escribe nada");
    }

    [Fact]
    public async Task Reintento_tras_editar_devuelve_la_version_real_y_no_modifica_el_asiento()
    {
        var book = await _fixture.BookAsync();
        await using var db = book.Db();
        var handler = book.Handler(db);
        var create = book.Create();
        var created = (await handler.Handle(create, Ct)).Value!;
        var edit = new EditManualJournalCommand(created.Id, 1, create.EntryDate, "editado", null, book.Lines());
        (await handler.Handle(edit, Ct)).Value!.Version.Should().Be(2);
        var before = await SnapshotAsync(book, created.Id);

        var replay = (await handler.Handle(create, Ct)).Value!;

        before.Version.Should().Be(2);
        replay.Id.Should().Be(created.Id);
        replay.Status.Should().Be("Draft");
        replay.Version.Should().Be(2, "el reintento no debe reportar la versión 1 de un asiento ya editado");
        replay.EntryNumber.Should().BeNull();
        (await SnapshotAsync(book, created.Id)).Should().Be(before);
    }

    [Fact]
    public async Task Reintento_tras_publicar_devuelve_Posted_con_el_numero_contable_real_sin_recontabilizar()
    {
        var book = await _fixture.BookAsync();
        await using var db = book.Db();
        var handler = book.Handler(db);
        var create = book.Create();
        var created = (await handler.Handle(create, Ct)).Value!;
        var posted = (await handler.Handle(new PostManualJournalCommand(created.Id, created.Version, Guid.NewGuid()), Ct)).Value!;
        posted.EntryNumber.Should().NotBeNull();
        var before = await SnapshotAsync(book, created.Id);

        var replay = (await handler.Handle(create, Ct)).Value!;

        replay.Id.Should().Be(created.Id);
        replay.Status.Should().Be(JournalEntryStatus.Posted.ToString());
        replay.EntryNumber.Should().Be(posted.EntryNumber);
        replay.Version.Should().Be(posted.Version);
        var after = await SnapshotAsync(book, created.Id);
        after.Should().Be(before, "no se vuelve a contabilizar ni se consume otro número");
    }

    [Fact]
    public async Task Reintentos_no_duplican_documentos_ni_intentos_ni_auditoria()
    {
        var book = await _fixture.BookAsync();
        await using var db = book.Db();
        var handler = book.Handler(db);
        var create = book.Create();
        var created = (await handler.Handle(create, Ct)).Value!;
        for (var i = 0; i < 3; i++)
            (await handler.Handle(create, Ct)).Value.Should().Be(created);

        var snapshot = await SnapshotAsync(book, created.Id);
        snapshot.Entries.Should().Be(1);
        snapshot.Audits.Should().Be(1, "solo la auditoría de creación");
        snapshot.Attempts.Should().Be(0);
    }

    [Fact]
    public async Task La_clave_idempotente_no_cruza_empresas()
    {
        var a = await _fixture.BookAsync();
        var b = await _fixture.BookAsync(existingTenant: a.TenantId);
        var create = a.Create();
        await using var dbA = a.Db();
        var createdA = (await a.Handler(dbA).Handle(create, Ct)).Value!;

        await using var dbB = b.Db();
        var createdB = (await b.Handler(dbB).Handle(b.Create() with { IdempotencyKey = create.IdempotencyKey }, Ct)).Value!;

        createdB.Id.Should().NotBe(createdA.Id, "la empresa B no debe ver el asiento de la empresa A");
        (await SnapshotAsync(a, createdA.Id)).Entries.Should().Be(1);
        (await SnapshotAsync(b, createdB.Id)).Entries.Should().Be(1);
        (await a.Handler(dbA).Handle(create, Ct)).Value.Should().Be(createdA);
    }

    [Fact]
    public async Task Creaciones_concurrentes_con_la_misma_clave_producen_un_unico_asiento()
    {
        var book = await _fixture.BookAsync();
        var create = book.Create();

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            await using var db = book.Db();
            return await book.Handler(db).Handle(create, Ct);
        }));

        results.Should().OnlyContain(r => r.IsSuccess);
        results.Select(r => r.Value!.Id).Distinct().Should().ContainSingle();
        (await SnapshotAsync(book, results[0].Value!.Id)).Entries.Should().Be(1);
    }

    private sealed record Snapshot(
        int Entries, int Audits, int Attempts, int SequenceRows, int Version, string Status, int? Number, int Lines);

    private static async Task<Snapshot> SnapshotAsync(ManualBook book, Guid entryId)
    {
        await using var verify = book.Db();
        var entry = await verify.JournalEntries.AsNoTracking().Include(e => e.Lines).SingleAsync(e => e.Id == entryId);
        return new(
            await verify.JournalEntries.CountAsync(),
            await verify.JournalEntryAudits.CountAsync(x => x.EntityId == entryId),
            await verify.ManualJournalAttempts.CountAsync(x => x.JournalEntryId == entryId),
            await verify.JournalEntrySequences.CountAsync(),
            entry.Version, entry.Status.ToString(), entry.EntryNumber, entry.Lines.Count);
    }
}
