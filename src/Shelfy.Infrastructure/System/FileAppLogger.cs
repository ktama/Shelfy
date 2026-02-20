using System.Collections.Concurrent;
using Shelfy.Core.Ports.System;

namespace Shelfy.Infrastructure.System;

/// <summary>
/// ファイルベースのアプリケーションロガー
/// </summary>
public class FileAppLogger : IAppLogger, IDisposable
{
    private readonly string _logFilePath;
    private readonly BlockingCollection<string> _logQueue = new(1000);
    private readonly Task _writerTask;
    private readonly CancellationTokenSource _cts = new();

    public FileAppLogger(string? logDirectory = null)
    {
        var logDir = logDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Shelfy");

        Directory.CreateDirectory(logDir);
        _logFilePath = Path.Combine(logDir, "Shelfy.log");

        // バックグラウンドでログを書き込む
        _writerTask = Task.Run(ProcessLogQueue);
    }

    public void Info(string message) => Enqueue("INFO", message);
    public void Warn(string message) => Enqueue("WARN", message);

    public void Error(string message, Exception? exception = null)
    {
        var fullMessage = exception is not null
            ? $"{message} | {exception}"
            : message;
        Enqueue("ERROR", fullMessage);
    }

    private void Enqueue(string level, string message)
    {
        var entry = $"{DateTime.UtcNow:O} [{level}] {message}";
        _logQueue.TryAdd(entry);
    }

    private async Task ProcessLogQueue()
    {
        try
        {
            foreach (var entry in _logQueue.GetConsumingEnumerable(_cts.Token))
            {
                try
                {
                    await File.AppendAllTextAsync(_logFilePath, entry + Environment.NewLine);
                }
                catch
                {
                    // ログ書き込みに失敗してもアプリを止めない
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常終了
        }
    }

    public void Dispose()
    {
        _logQueue.CompleteAdding();

        try
        {
            // まずキューの残りを書き終わるのを待つ
            _writerTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // タイムアウトは無視
        }

        // 書き込みタスクが GetConsumingEnumerable で待機中の場合にキャンセル
        _cts.Cancel();

        _cts.Dispose();
        _logQueue.Dispose();
        GC.SuppressFinalize(this);
    }
}
