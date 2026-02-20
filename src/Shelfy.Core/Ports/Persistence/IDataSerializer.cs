using Shelfy.Core.UseCases.DataTransfer;

namespace Shelfy.Core.Ports.Persistence;

/// <summary>
/// Export/Import データのシリアライズ・デシリアライズインターフェース
/// </summary>
public interface IDataSerializer
{
    /// <summary>
    /// ExportData を JSON 文字列にシリアライズする
    /// </summary>
    string Serialize(ExportData data);

    /// <summary>
    /// JSON 文字列を ExportData にデシリアライズする
    /// </summary>
    ExportData? Deserialize(string json);
}
