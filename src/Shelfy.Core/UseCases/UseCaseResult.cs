namespace Shelfy.Core.UseCases;

/// <summary>
/// UseCase の実行結果
/// </summary>
public abstract record UseCaseResult
{
    public bool IsSuccess => this is Success;
    public bool IsFailure => this is Failure;

    public sealed record Success : UseCaseResult;
    public sealed record Failure(string ErrorMessage) : UseCaseResult;
}

/// <summary>
/// 値を返す UseCase の実行結果
/// </summary>
public abstract record UseCaseResult<T>
{
    public bool IsSuccess => this is Success;
    public bool IsFailure => this is Failure;

    public sealed record Success(T Value) : UseCaseResult<T>;
    public sealed record Failure(string ErrorMessage) : UseCaseResult<T>;
}
