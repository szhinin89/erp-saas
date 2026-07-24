namespace ERP.Infrastructure.Options;

public sealed class SnowflakeOptions
{
    public const string SectionName = "Snowflake";

    /// <summary>0–1023. Debe ser único por proceso worker en producción.</summary>
    public int WorkerId { get; set; } = 1;
}
