using Shelfy.Core.Ports.Persistence;

namespace Shelfy.Core.UseCases.DataTransfer;

/// <summary>
/// データ転送関連 UseCase のファサード
/// </summary>
public class DataTransferUseCases(
    ExportDataUseCase export,
    ImportDataUseCase import,
    IDataSerializer serializer)
{
    public ExportDataUseCase Export { get; } = export;
    public ImportDataUseCase Import { get; } = import;
    public IDataSerializer Serializer { get; } = serializer;
}
