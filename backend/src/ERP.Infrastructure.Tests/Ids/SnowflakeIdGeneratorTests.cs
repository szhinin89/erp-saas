using ERP.Infrastructure.Ids;
using ERP.Infrastructure.Options;

namespace ERP.Infrastructure.Tests.Ids;

public sealed class SnowflakeIdGeneratorTests
{
    [Fact]
    public void NextId_generates_unique_values()
    {
        var generator = new SnowflakeIdGenerator(Microsoft.Extensions.Options.Options.Create(new SnowflakeOptions { WorkerId = 1 }));
        var ids = Enumerable.Range(0, 1000).Select(_ => generator.NextId()).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void NextId_throws_when_worker_out_of_range()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SnowflakeIdGenerator(Microsoft.Extensions.Options.Options.Create(new SnowflakeOptions { WorkerId = 2048 })));
    }
}
