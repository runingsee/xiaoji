namespace Huamishu.Api.Common;

/// <summary>时间统一约定：全站使用北京时间（Asia/Shanghai）墙钟时间，不涉时区换算。</summary>
public static class TimeFormat
{
    /// <summary>yyyy-MM-dd</summary>
    public static string Date(DateTime dt) => dt.ToString("yyyy-MM-dd");

    /// <summary>整日则只给日期，否则 yyyy-MM-dd HH:mm。</summary>
    public static string DateTimeFlex(DateTime dt) =>
        dt is { Hour: 0, Minute: 0, Second: 0 } ? dt.ToString("yyyy-MM-dd") : dt.ToString("yyyy-MM-dd HH:mm");

    /// <summary>解析 AI 返回的日期串（yyyy-MM-dd 或 yyyy-MM-dd HH:mm），失败返回 null。</summary>
    public static DateTime? TryParseOccurredAt(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return DateTime.TryParse(s.Trim(), out var dt) ? dt : null;
    }
}