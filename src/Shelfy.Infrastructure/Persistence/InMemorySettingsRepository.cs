using Shelfy.Core.Ports.Persistence;

namespace Shelfy.Infrastructure.Persistence;

/// <summary>
/// Settings のインメモリリポジトリ（開発・テスト用）
/// </summary>
public class InMemorySettingsRepository : ISettingsRepository
{
    private readonly Dictionary<string, string> _settings = new();

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        _settings.TryGetValue(key, out var value);
        return Task.FromResult(value);
    }

    public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        _settings[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _settings.Remove(key);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyDictionary<string, string>>(
            new Dictionary<string, string>(_settings));
    }
}
