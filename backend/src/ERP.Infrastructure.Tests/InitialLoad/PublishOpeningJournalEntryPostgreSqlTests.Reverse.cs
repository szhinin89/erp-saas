using ERP.Application.Common;
using ERP.Application.Modules.InitialLoad.OpeningPosting;
using ERP.Application.Modules.InitialLoad.UseCases.OpeningBalanceDate;
using ERP.Domain.Modules.Accounting.Entities;
using ERP.Domain.Modules.Accounting.Enums;
using ERP.Domain.Modules.Accounting.ValueObjects;
using ERP.Domain.Modules.Caja.Entities;
using ERP.Domain.Modules.Company.Entities;
using ERP.Domain.Modules.Company.Enums;
using ERP.Domain.Modules.InitialLoad.Enums;
using ERP.Domain.Modules.InitialLoad.Interfaces;
using ERP.Infrastructure.Persistence;
using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ERP.Infrastructure.Tests.InitialLoad;

/// <summary>
/// IL-8B — reverso controlado del ASI de apertura (paso 1 de la corrección) sobre PostgreSQL real:
/// reverso completo a la misma fecha conservando el original, versión anterior como historial,
/// nueva versión con SourceEventId nuevo, fecha de apertura inmutable, reintento y concurrencia sin
/// doble reverso ni número extra, versión no vigente/no publicada, períodos de [apertura, hoy]
/// (todos abiertos permite aun con operaciones reales; uno cerrado, sea el de apertura o uno
/// posterior, bloquea sin número ni cambio de versión), aislamiento por empresa y motivo.
/// </summary>
public sealed partial class PublishOpeningJournalEntryPostgreSqlTests
{
    private Task<Result<OpeningJournalEntryReversalDto>> ReverseAsync(Guid postingId, string reason = "Cuenta de capital equivocada") =>
        SendAsync(new ReverseOpeningJournalEntryCommand(postingId, reason));

    /// <summary>Saldo acreedor neto de la puente (Posted + Reversed, mismo criterio que Mayor).</summary>
    private async Task<decimal> NetBridgeBalanceAsync()
    {
        var bridge = await BridgeAsync();
        return await QueryAsync(async db =>
        {
            var lines = await db.JournalEntries
                .Where(e => e.CompanyId == _company
                    && (e.Status == JournalEntryStatus.Posted || e.Status == JournalEntryStatus.Reversed))
                .SelectMany(e => e.Lines).Where(l => l.AccountId == bridge).ToListAsync();
            return lines.Sum(l => l.Credit - l.Debit);
        });
    }

    private Task<List<JournalEntry>> ReversalsAsync() =>
        QueryAsync(db => db.JournalEntries.AsNoTracking().Include(e => e.Lines)
            .Where(e => e.CompanyId == _company && e.SourceModule == "Accounting" && e.SourceEventType == "Reversal")
            .ToListAsync());

    private async Task<OpeningJournalEntryPostingDto> PublishedAsync()
    {
        await LoadBridgeAsync();
        var published = await PublishAsync(await ValidLinesAsync());
        published.IsSuccess.Should().BeTrue(published.Error);
        return published.Value!;
    }

    [Fact]
    public async Task Reverso_completo_conserva_el_original_deja_historial_y_permite_publicar_version_nueva()
    {
        var v1 = await PublishedAsync();
        (await NetBridgeBalanceAsync()).Should().Be(0m);
        var sequencesBefore = (await SequencesAsync()).Sum();

        var reversed = await ReverseAsync(v1.Id);

        reversed.IsSuccess.Should().BeTrue(reversed.Error);
        reversed.Value!.AlreadyReversed.Should().BeFalse();
        reversed.Value.JournalEntryId.Should().Be(v1.JournalEntryId!.Value);
        reversed.Value.Reason.Should().Be("Cuenta de capital equivocada");
        var original = await QueryAsync(db => db.JournalEntries.AsNoTracking().Include(e => e.Lines)
            .SingleAsync(e => e.Id == v1.JournalEntryId));
        original.Status.Should().Be(JournalEntryStatus.Reversed, "el original se conserva, nunca se edita ni borra");
        original.Lines.Sum(l => l.Debit).Should().Be(100m);
        original.ReverseJournalEntryId.Should().Be(reversed.Value.ReversalJournalEntryId);
        var reversal = (await ReversalsAsync()).Should().ContainSingle().Subject;
        reversal.Id.Should().Be(reversed.Value.ReversalJournalEntryId);
        reversal.OriginalJournalEntryId.Should().Be(original.Id);
        reversal.SourceEventId.Should().Be(original.Id);
        reversal.EntryDate.Should().Be(Cutoff, "el reverso usa la misma fecha de apertura");
        reversal.AccountingPeriodId.Should().Be(original.AccountingPeriodId);
        reversal.Status.Should().Be(JournalEntryStatus.Posted);
        reversal.Lines.Select(l => (l.AccountId, l.Debit, l.Credit)).Should().BeEquivalentTo(
            original.Lines.Select(l => (l.AccountId, l.Credit, l.Debit)));
        (await NetBridgeBalanceAsync()).Should().Be(100m, "la puente vuelve a su saldo previo al ASI");
        (await SequencesAsync()).Sum().Should().Be(sequencesBefore + 1);
        (await StateAsync()).Should().BeNull("sin versión vigente IL-8 queda incompleto");
        var v1State = (await VersionsAsync()).Single();
        (v1State.IsCurrent, v1State.Status, v1State.JournalEntryId).Should().Be((false, OpeningBalancePostingStatus.Posted, original.Id));
        v1State.SupersededAt.Should().NotBeNull();
        (await SendAsync(new GetOpeningBalanceDateQuery())).Value!.IsLocked.Should().BeTrue();
        (await SendAsync(new SetOpeningBalanceDateCommand(Cutoff.AddDays(-1)))).IsSuccess
            .Should().BeFalse("la fecha de apertura sigue inmutable para siempre");

        var v2 = await PublishAsync(await ValidLinesAsync());

        v2.IsSuccess.Should().BeTrue(v2.Error);
        v2.Value!.Version.Should().Be(2);
        v2.Value.Id.Should().NotBe(v1.Id, "versión nueva = SourceEventId nuevo");
        v2.Value.EntryDate.Should().Be(Cutoff);
        (await NetBridgeBalanceAsync()).Should().Be(0m);
        (await AsiEntriesAsync()).Should().Be(2, "el historial se conserva");
        (await VersionsAsync()).Select(v => (v.Version, v.IsCurrent)).Should().Equal((1, false), (2, true));
    }

    [Fact]
    public async Task Reintento_y_reversos_concurrentes_crean_un_solo_reverso_y_un_solo_numero()
    {
        var v1 = await PublishedAsync();
        var sequencesBefore = (await SequencesAsync()).Sum();

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Task.Run(() => ReverseAsync(v1.Id))));

        results.Should().OnlyContain(r => r.IsSuccess);
        results.Count(r => !r.Value!.AlreadyReversed).Should().Be(1);
        results.Select(r => r.Value!.ReversalJournalEntryId).Distinct().Should().ContainSingle();
        (await ReversalsAsync()).Should().ContainSingle();
        (await SequencesAsync()).Sum().Should().Be(sequencesBefore + 1);

        var retry = await ReverseAsync(v1.Id, "otro motivo");

        retry.IsSuccess.Should().BeTrue(retry.Error);
        retry.Value!.AlreadyReversed.Should().BeTrue();
        retry.Value.ReversalJournalEntryId.Should().Be(results[0].Value!.ReversalJournalEntryId);
        retry.Value.Reason.Should().Be("Cuenta de capital equivocada", "el reintento no reescribe el motivo");
        (await ReversalsAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task Version_no_publicada_o_reemplazada_sin_reverso_no_se_reversa()
    {
        (await PublishAsync(Debit(await AccountAsync("3.1.02.001"), 1m), Credit(await BridgeAsync(), 1m)))
            .Code.Should().Be(PublishOpeningJournalEntryCommandHandler.BridgeNotClearedCode);
        var failed = (await StateAsync())!;

        (await ReverseAsync(failed.Id)).Code.Should().Be(ReverseOpeningJournalEntryCommandHandler.NotPostedCode);

        var published = await PublishAsync(await EquityOnlyLinesAsync());
        published.IsSuccess.Should().BeTrue(published.Error);
        await using (var scope = _services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IOpeningJournalEntryPostingRepository>();
            (await repo.FindCurrentAsync(_tenant, _company))!.MarkSuperseded(_user);
            await repo.SaveChangesAsync();
        }

        (await ReverseAsync(published.Value!.Id)).Code.Should().Be(ReverseOpeningJournalEntryCommandHandler.NotCurrentCode);
        (await ReverseAsync(Guid.NewGuid())).Code.Should().Be(ApiResponseCodes.Common.NotFound);
        (await ReversalsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Periodo_cerrado_bloquea_el_reverso_sin_asiento_ni_numero_ni_cambio_de_version()
    {
        var v1 = await PublishedAsync();
        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            (await db.AccountingPeriods.SingleAsync(p => p.CompanyId == _company))
                .Close(_user, new JournalEntryClosureReadiness(false, false, false));
            await db.SaveChangesAsync();
        }
        var sequencesBefore = (await SequencesAsync()).Sum();

        var result = await ReverseAsync(v1.Id);

        result.Code.Should().Be("PERIOD_NOT_OPEN", result.Error);
        (await ReversalsAsync()).Should().BeEmpty();
        (await SequencesAsync()).Sum().Should().Be(sequencesBefore);
        (await StateAsync())!.Id.Should().Be(v1.Id, "la versión sigue vigente");
    }

    [Fact]
    public async Task Operaciones_reales_con_todos_los_periodos_abiertos_permiten_la_correccion()
    {
        var v1 = await PublishedAsync();
        var establishment = Establishment.Create(_tenant, branchId: _branch, _company, code: "001", name: "Matriz",
            address: "Av. 1", phone: null, isMain: true, createdBy: _user);
        var register = CashRegister.Create(_tenant, _company, _branch, "CAJA-01", "Caja", _user);
        await QueryAsync(async db =>
        {
            db.Establishments.Add(establishment);
            db.CashRegisters.Add(register);
            return await db.SaveChangesAsync();
        });
        var emissionPoint = EmissionPoint.Create(_tenant, _company, establishment.Id, code: "001", name: "PE-001",
            emissionType: EmissionType.Electronic, isDefault: true, createdBy: _user);
        await QueryAsync(async db => { db.EmissionPoints.Add(emissionPoint); return await db.SaveChangesAsync(); });
        await QueryAsync(async db =>
        {
            db.CashSessions.Add(CashSession.Open(_tenant, _company, _branch, _user, register.Id, "CAJA-01", "Caja",
                emissionPoint.Id, "001", 0m, _user));
            return await db.SaveChangesAsync();
        });
        (await SendAsync(new GetOpeningBalanceDateQuery())).Value!.HasRealOperations.Should().BeTrue();

        var result = await ReverseAsync(v1.Id);

        result.IsSuccess.Should().BeTrue("las operaciones reales no bloquean por sí solas: " + result.Error);
        (await ReversalsAsync()).Should().ContainSingle();
    }

    /// <summary>
    /// Apertura al 31-mar-2026 (hoy de la empresa = 17-sep-2026, reloj de prueba): el período anual
    /// del bootstrap se acota a ene–mar y se agregan períodos mensuales abr–dic, todos abiertos. ASI
    /// solo patrimonial (sin cargas → la puente sigue en 0). Devuelve el ASI publicado.
    /// </summary>
    private async Task<OpeningJournalEntryPostingDto> PublishedWithMonthlyPeriodsAsync()
    {
        var opening = new DateOnly(2026, 3, 31);
        await QueryAsync(async db =>
        {
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE company SET opening_balance_date = {opening} WHERE id = {_company}");
            var annual = await db.AccountingPeriods.SingleAsync(p => p.CompanyId == _company);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE accounting_periods SET end_date = {opening} WHERE id = {annual.Id}");
            for (var month = 4; month <= 12; month++)
                db.AccountingPeriods.Add(AccountingPeriod.Create(_tenant, _company, 2026, month,
                    new DateOnly(2026, month, 1), new DateOnly(2026, month, DateTime.DaysInMonth(2026, month)), _user));
            return await db.SaveChangesAsync();
        });
        var published = await PublishAsync(await EquityOnlyLinesAsync());
        published.IsSuccess.Should().BeTrue(published.Error);
        published.Value!.EntryDate.Should().Be(opening);
        return published.Value;
    }

    private Task ClosePeriodAsync(Func<AccountingPeriod, bool> which) =>
        QueryAsync(async db =>
        {
            (await db.AccountingPeriods.Where(p => p.CompanyId == _company).ToListAsync()).Single(which)
                .Close(_user, new JournalEntryClosureReadiness(false, false, false));
            return await db.SaveChangesAsync();
        });

    private async Task ShouldBlockWithoutTraceAsync(OpeningJournalEntryPostingDto published)
    {
        var sequencesBefore = (await SequencesAsync()).Sum();

        var result = await ReverseAsync(published.Id);

        result.IsSuccess.Should().BeFalse();
        result.Code.Should().Be("PERIOD_NOT_OPEN", result.Error);
        result.Error.Should().Contain("ajuste contable");
        (await ReversalsAsync()).Should().BeEmpty();
        (await SequencesAsync()).Sum().Should().Be(sequencesBefore, "un bloqueo no consume secuencia");
        var versions = await VersionsAsync();
        versions.Should().ContainSingle();
        (versions[0].Id, versions[0].IsCurrent, versions[0].SupersededAt).Should().Be((published.Id, true, null));
        (await QueryAsync(db => db.JournalEntries.AsNoTracking().SingleAsync(e => e.Id == published.JournalEntryId)))
            .Status.Should().Be(JournalEntryStatus.Posted, "la apertura queda histórica, intacta");
    }

    [Fact]
    public async Task Todos_los_periodos_abiertos_varios_meses_despues_de_la_apertura_permiten_la_correccion()
    {
        var v1 = await PublishedWithMonthlyPeriodsAsync();

        var result = await ReverseAsync(v1.Id);

        result.IsSuccess.Should().BeTrue(result.Error);
        var reversal = (await ReversalsAsync()).Should().ContainSingle().Subject;
        reversal.EntryDate.Should().Be(new DateOnly(2026, 3, 31), "mismo OpeningBalanceDate original");
        var v2 = await PublishAsync(await EquityOnlyLinesAsync());
        v2.IsSuccess.Should().BeTrue(v2.Error);
        v2.Value!.EntryDate.Should().Be(new DateOnly(2026, 3, 31));
        v2.Value.Version.Should().Be(2);
    }

    [Fact]
    public async Task Periodo_de_apertura_abierto_pero_uno_posterior_cerrado_bloquea_el_reverso()
    {
        var v1 = await PublishedWithMonthlyPeriodsAsync();
        await ClosePeriodAsync(p => p.PeriodNumber == 6);

        await ShouldBlockWithoutTraceAsync(v1);
    }

    [Fact]
    public async Task Periodo_de_apertura_cerrado_bloquea_el_reverso()
    {
        var v1 = await PublishedWithMonthlyPeriodsAsync();
        await ClosePeriodAsync(p => p.PeriodNumber == 1);

        await ShouldBlockWithoutTraceAsync(v1);
    }

    [Fact]
    public async Task Version_de_otra_empresa_no_se_encuentra()
    {
        var v1 = await PublishedAsync();
        var companyA = _company;
        var (companyB, _) = await NewCompanyAsync();

        _company = companyB;
        try
        {
            (await ReverseAsync(v1.Id)).Code.Should().Be(ApiResponseCodes.Common.NotFound);
        }
        finally
        {
            _company = companyA;
        }
        (await StateAsync())!.Id.Should().Be(v1.Id);
        (await ReversalsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Motivo_obligatorio_y_con_longitud_maxima_estandar()
    {
        var v1 = await PublishedAsync();

        await FluentActions.Awaiting(() => ReverseAsync(v1.Id, "   "))
            .Should().ThrowAsync<ValidationException>().WithMessage("*motivo*obligatorio*");
        await FluentActions.Awaiting(() => ReverseAsync(v1.Id, new string('x', JournalEntry.ReverseReasonMaxLength + 1)))
            .Should().ThrowAsync<ValidationException>().WithMessage("*como máximo*");

        var longest = await ReverseAsync(v1.Id, new string('x', JournalEntry.ReverseReasonMaxLength));

        longest.IsSuccess.Should().BeTrue(longest.Error);
        (await ReversalsAsync()).Single().Description.Length.Should().BeLessThanOrEqualTo(JournalEntry.DescriptionMaxLength);
    }
}
