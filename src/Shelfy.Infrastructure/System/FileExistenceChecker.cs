using Shelfy.Core.Ports.System;

namespace Shelfy.Infrastructure.System;

/// <summary>
/// ファイル/フォルダの存在確認実装
/// </summary>
public class FileExistenceChecker : IExistenceChecker
{
    public bool Exists(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
            return false;

        // URLの場合は常にtrueを返す（実際の存在確認は行わない）
        if (Uri.TryCreate(target, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return true;
        }

        // ファイルまたはフォルダの存在確認
        return File.Exists(target) || Directory.Exists(target);
    }
}
