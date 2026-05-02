using System.Text.Json;
using System.Text.Json.Serialization;
using ERP.Application.Common;
using Microsoft.Extensions.Hosting;

namespace ERP.Infrastructure.Deployment;

/// <summary>
/// Lectura/escritura de cuotas de instancia en <c>App_Data/instance-quota.json</c> (sin secretos).
/// </summary>
public sealed class InstanceQuotaFileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHostEnvironment _env;
    private readonly object _lock = new();
    private InstanceQuotaFileModel? _cache;
    private DateTime _cacheWriteUtc;

    public InstanceQuotaFileStore(IHostEnvironment env) => _env = env;

    public string GetFilePath() => Path.Combine(_env.ContentRootPath, "App_Data", "instance-quota.json");

    public InstanceQuotaFileModel? Read()
    {
        var path = GetFilePath();
        lock (_lock)
        {
            if (!File.Exists(path))
            {
                _cache = null;
                _cacheWriteUtc = default;
                return null;
            }

            var w = File.GetLastWriteTimeUtc(path);
            if (_cache is not null && w == _cacheWriteUtc)
                return _cache;

            try
            {
                var json = File.ReadAllText(path);
                _cache = JsonSerializer.Deserialize<InstanceQuotaFileModel>(json, JsonOptions);
                _cacheWriteUtc = w;
                return _cache;
            }
            catch
            {
                _cache = null;
                _cacheWriteUtc = default;
                return null;
            }
        }
    }

    public void Write(InstanceQuotaFileModel model)
    {
        var dir = Path.Combine(_env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dir);
        var path = GetFilePath();
        var json = JsonSerializer.Serialize(model, JsonOptions);
        File.WriteAllText(path, json);
        lock (_lock)
        {
            _cache = null;
            _cacheWriteUtc = default;
        }
    }
}
