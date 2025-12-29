using Shelfy.Core.Domain.Entities;

namespace Shelfy.Core.UseCases.Launch;

/// <summary>
/// LaunchItem ユースケースの実行結果
/// </summary>
public abstract record LaunchItemResult
{
    public sealed record Success(PostLaunchAction PostAction) : LaunchItemResult;
    public sealed record ItemNotFound(ItemId ItemId) : LaunchItemResult;
    public sealed record LaunchFailed(string ErrorMessage) : LaunchItemResult;
}
