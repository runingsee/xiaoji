using Huamishu.Api.Data;
using Xunit;

namespace Huamishu.Api.Tests;

public class TextSplitterTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Split_NullOrWhitespace_ReturnsEmpty(string? raw)
    {
        var result = TextSplitter.Split(raw!);
        Assert.Empty(result);
    }

    [Fact]
    public void Split_SingleLineWithoutSeparators_ReturnsOriginal()
    {
        const string raw = "今天花了三十五块买咖啡";
        var result = TextSplitter.Split(raw);
        Assert.Single(result);
        Assert.Equal(raw, result[0]);
    }

    [Fact]
    public void Split_MultipleLines_SplitsByNewline()
    {
        const string raw = "早上吃了包子\n中午吃了拉面\n晚上吃了火锅";
        var result = TextSplitter.Split(raw);
        Assert.Equal(3, result.Count);
        Assert.Equal("早上吃了包子", result[0]);
        Assert.Equal("中午吃了拉面", result[1]);
        Assert.Equal("晚上吃了火锅", result[2]);
    }

    [Fact]
    public void Split_ChineseSeparators_Splits()
    {
        var result = TextSplitter.Split("买书花了五十块；打车花了二十块。看电影花了四十块");
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Split_ShortSegments_AreFiltered()
    {
        var result = TextSplitter.Split("买书\n好\n看电影花的钱");
        Assert.All(result, s => Assert.True(s.Length >= 5));
        Assert.DoesNotContain(result, s => s == "好");
    }

    [Fact]
    public void Split_MoreThanMaxSegments_IsTruncated()
    {
        var many = string.Join('\n', Enumerable.Range(0, 15).Select(i => $"第{i}条记录内容足够长"));
        var result = TextSplitter.Split(many);
        Assert.Equal(10, result.Count);
    }

    [Fact]
    public void NeedsSplit_ReflectsSegmentCount()
    {
        Assert.True(TextSplitter.NeedsSplit("第一条记录内容够了\n第二条记录内容够了"));
        Assert.False(TextSplitter.NeedsSplit("单独一条记录内容"));
    }

    [Fact]
    public void SegmentCount_CountsSegments()
    {
        Assert.Equal(2, TextSplitter.SegmentCount("第一条记录内容\n第二条记录内容"));
    }
}