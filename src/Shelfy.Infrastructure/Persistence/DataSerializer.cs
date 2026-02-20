using System.Text.Json;
using System.Text.Json.Serialization;
using Shelfy.Core.Ports.Persistence;
using Shelfy.Core.UseCases.DataTransfer;

namespace Shelfy.Infrastructure.Persistence;

/// <summary>
/// Export/Import データの JSON シリアライザ
/// </summary>
public class DataSerializer : IDataSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// ExportData を JSON 文字列にシリアライズする
    /// </summary>
    public string Serialize(ExportData data)
    {
        return JsonSerializer.Serialize(data, Options);
    }

    /// <summary>
    /// JSON 文字列を ExportData にデシリアライズする
    /// </summary>
    public ExportData? Deserialize(string json)
    {
        return JsonSerializer.Deserialize<ExportData>(json, Options);
    }
}
