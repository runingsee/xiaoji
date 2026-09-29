using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Huamishu.Api.AI;

/// <summary>
/// DeepSeek API 封装（OpenAI 兼容接口）。
/// 依赖注入：AddHttpClient&lt;DeepSeekClient&gt;(BaseAddress = https://api.deepseek.com)
/// </summary>
public sealed class DeepSeekClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<DeepSeekClient> _log;

    public DeepSeekClient(HttpClient http, IConfiguration config, ILogger<DeepSeekClient> log)
    {
        _http = http;
        _config = config;
        _log = log;
    }

    /// <summary>把口述文本解析为结构化结果；失败返回 null（调用方决定重试/降级）。</summary>
    public async Task<ParseResult?> ParseAsync(string rawText, CancellationToken ct)
    {
        var messages = new[]
        {
            new ChatMessage("system", Prompts.ParseSystem),
            new ChatMessage("user", rawText),
        };

        var content = await ChatJsonAsync(messages, temperature: 0.2, ct);
        if (string.IsNullOrWhiteSpace(content))
        {
            _log.LogWarning("DeepSeek 解析未返回内容: {Raw}", Truncate(rawText));
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ParseResult>(content, JsonOpts);
        }
        catch (JsonException ex)
        {
            _log.LogWarning(ex, "DeepSeek 返回非法 JSON，降级处理: {Content}", Truncate(content));
            return null;
        }
    }

    /// <summary>把自然语言查询解析为结构化搜索意图；失败返回 null。</summary>
    public async Task<SearchIntent?> ParseSearchIntentAsync(string query, CancellationToken ct)
    {
        var messages = new[]
        {
            new ChatMessage("system", Prompts.SearchIntentPrompt),
            new ChatMessage("user", query),
        };

        var content = await ChatJsonAsync(messages, temperature: 0.1, ct);
        if (string.IsNullOrWhiteSpace(content))
        {
            _log.LogWarning("DeepSeek 搜索意图解析未返回内容: {Query}", Truncate(query));
            return null;
        }

        try
        {
            var intent = JsonSerializer.Deserialize<SearchIntent>(content, JsonOpts);
            return intent?.Confidence is >= 0.5 ? intent : null;
        }
        catch (JsonException ex)
        {
            _log.LogWarning(ex, "DeepSeek 返回非法搜索意图 JSON: {Content}", Truncate(content));
            return null;
        }
    }

    /// <summary>对话咨询（非 JSON 输出），返回自然语言回答。</summary>
    public async Task<string?> AskAsync(IReadOnlyList<ChatFact> facts, string question, CancellationToken ct)
    {
        var messages = new[]
        {
            new ChatMessage("system", Prompts.AskSystem(FactsToJson(facts))),
            new ChatMessage("user", question),
        };
        return await ChatTextAsync(messages, temperature: 0.3, ct);
    }

    /// <summary>JSON 模式调用：要求模型只输出 JSON。</summary>
    private async Task<string?> ChatJsonAsync(IEnumerable<ChatMessage> messages, double temperature, CancellationToken ct)
    {
        var payload = new
        {
            model = _config["DeepSeek:Model"] ?? "deepseek-chat",
            messages,
            temperature,
            stream = false,
            response_format = new { type = "json_object" },
        };
        return await SendAsync(payload, ct);
    }

    /// <summary>普通文本模式调用。</summary>
    private async Task<string?> ChatTextAsync(IEnumerable<ChatMessage> messages, double temperature, CancellationToken ct)
    {
        var payload = new
        {
            model = _config["DeepSeek:Model"] ?? "deepseek-chat",
            messages,
            temperature,
            stream = false,
        };
        return await SendAsync(payload, ct);
    }

    private async Task<string?> SendAsync(object payload, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = JsonContent.Create(payload, options: JsonOpts),
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config["DeepSeek:ApiKey"]);

            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("DeepSeek 调用失败 {Status}: {Body}", (int)resp.StatusCode, Truncate(body));
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _log.LogWarning(ex, "DeepSeek 调用异常");
            return null;
        }
    }

    private static string FactsToJson(IReadOnlyList<ChatFact> facts) =>
        JsonSerializer.Serialize(facts, JsonOpts);

    private static string Truncate(string s) => s.Length <= 500 ? s : s[..500] + "...";
}