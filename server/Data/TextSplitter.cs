namespace Huamishu.Api.Data;

/// <summary>
/// 文本拆分器：将用户一次性输入的多条记录拆分为独立的文本段。
/// 拆分规则：优先按换行，其次按中文分隔符（；、。），不按逗号。
/// </summary>
public static class TextSplitter
{
    private const int MinSegmentLength = 5;
    private const int MaxSegments = 10;

    /// <summary>
    /// 将用户输入拆分为多条独立文本。
    /// 如果不需要拆分（只有一条），返回只包含原文本的列表。
    /// </summary>
    public static List<string> Split(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return [];

        var trimmed = rawText.Trim();

        // 优先按换行拆分
        var lines = trimmed.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length > 1)
            return lines.Select(l => l.Trim())
                .Where(l => l.Length >= MinSegmentLength)
                .Take(MaxSegments).ToList();

        // 其次按中文分隔符拆分
        var parts = trimmed.Split(['；', '、', '。'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1)
            return parts.Select(p => p.Trim())
                .Where(p => p.Length >= MinSegmentLength)
                .Take(MaxSegments).ToList();

        // 不拆分
        return [trimmed];
    }

    /// <summary>
    /// 判断是否需要拆分（拆分后超过 1 条）
    /// </summary>
    public static bool NeedsSplit(string rawText) => Split(rawText).Count > 1;

    /// <summary>
    /// 获取拆分后的段数
    /// </summary>
    public static int SegmentCount(string rawText) => Split(rawText).Count;
}
