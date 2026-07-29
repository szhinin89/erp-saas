using ERP.Domain.Common;
using ERP.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace ERP.Infrastructure.Ids;

/// <summary>
/// Snowflake: 41 bits timestamp | 10 bits worker | 12 bits sequence.
/// Epoch: 2024-01-01 UTC.
/// </summary>
public sealed class SnowflakeIdGenerator : ISnowflakeIdGenerator
{
    private const int WorkerIdBits = 10;
    private const int SequenceBits = 12;
    private const long MaxWorkerId = (1L << WorkerIdBits) - 1;
    private const long MaxSequence = (1L << SequenceBits) - 1;
    private const int WorkerIdShift = SequenceBits;
    private const int TimestampShift = SequenceBits + WorkerIdBits;

    private static readonly long EpochMs = new DateTimeOffset(
        2024,
        1,
        1,
        0,
        0,
        0,
        TimeSpan.Zero
    ).ToUnixTimeMilliseconds();

    private readonly long _workerId;
    private readonly object _lock = new();
    private long _lastTimestamp = -1L;
    private long _sequence;

    public SnowflakeIdGenerator(IOptions<SnowflakeOptions> options)
    {
        var workerId = options.Value.WorkerId;
        if (workerId < 0 || workerId > MaxWorkerId)
            throw new ArgumentOutOfRangeException(
                nameof(options),
                $"WorkerId must be between 0 and {MaxWorkerId}."
            );
        _workerId = workerId;
    }

    public long NextId()
    {
        lock (_lock)
        {
            var timestamp = CurrentTimeMillis();
            if (timestamp < _lastTimestamp)
                throw new InvalidOperationException(
                    "Clock moved backwards. Refusing to generate Snowflake id."
                );

            if (timestamp == _lastTimestamp)
            {
                _sequence = (_sequence + 1) & MaxSequence;
                if (_sequence == 0)
                {
                    timestamp = WaitNextMillis(_lastTimestamp);
                }
            }
            else
            {
                _sequence = 0;
            }

            _lastTimestamp = timestamp;

            return ((timestamp - EpochMs) << TimestampShift)
                | (_workerId << WorkerIdShift)
                | _sequence;
        }
    }

    private static long WaitNextMillis(long lastTimestamp)
    {
        var timestamp = CurrentTimeMillis();
        while (timestamp <= lastTimestamp)
            timestamp = CurrentTimeMillis();
        return timestamp;
    }

    private static long CurrentTimeMillis() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
