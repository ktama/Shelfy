namespace Shelfy.Core.Ports.System;

/// <summary>
/// ログ出力インターフェース
/// </summary>
public interface IAppLogger
{
    void Info(string message);
    void Warn(string message);
    void Error(string message, Exception? exception = null);
}
