namespace Hexara.Application.Common.Interfaces;

/// <summary>
/// انتزاع زمان — بازی پر از تایمر و مهلت نوبت است و برای تست‌پذیری نباید
/// مستقیم از <c>DateTimeOffset.UtcNow</c> استفاده شود.
/// </summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
