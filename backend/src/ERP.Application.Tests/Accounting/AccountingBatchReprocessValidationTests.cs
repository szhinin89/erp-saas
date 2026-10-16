using ERP.Application.Modules.Accounting.UseCases.Continuity;
using FluentAssertions;

namespace ERP.Application.Tests.Accounting;

public sealed class AccountingBatchReprocessValidationTests
{
    [Fact]
    public void Batch_validates_limit_ids_and_reason()
    {
        var validator = new ReprocessAccountingPostingsCommandValidator();
        validator.Validate(new ReprocessAccountingPostingsCommand(null, "reason")).IsValid.Should().BeFalse();
        validator.Validate(new ReprocessAccountingPostingsCommand(Array.Empty<Guid>(), "reason")).IsValid.Should().BeFalse();
        validator.Validate(new ReprocessAccountingPostingsCommand(Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToArray(), "reason")).IsValid.Should().BeFalse();
        validator.Validate(new ReprocessAccountingPostingsCommand(Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).ToArray(), "reason")).IsValid.Should().BeTrue();
        validator.Validate(new ReprocessAccountingPostingsCommand(new[] { Guid.NewGuid() }, " ")).IsValid.Should().BeFalse();
        validator.Validate(new ReprocessAccountingPostingsCommand(new[] { Guid.Empty }, "reason")).IsValid.Should().BeFalse();
        validator.Validate(new ReprocessAccountingPostingsCommand(new[] { Guid.NewGuid() }, new string('x', 501))).IsValid.Should().BeFalse();
        new ReprocessAccountingPostingCommandValidator().Validate(new ReprocessAccountingPostingCommand(Guid.NewGuid(), null)).IsValid.Should().BeFalse();
    }
}
