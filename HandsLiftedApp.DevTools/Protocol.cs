using System.Collections.Specialized;
using System.Text;
using System.Text.Json;

namespace HandsLiftedApp.DevTools;

/// <summary>Transport-independent request, so the API can be driven by the HTTP listener or called directly from tests.</summary>
public sealed class DevRequest
{
    public string Method { get; init; } = "GET";
    public string Path { get; init; } = "/";
    public NameValueCollection Query { get; init; } = new();
    public string Body { get; init; } = "";

    public string? Get(string key) => Query[key] is { Length: > 0 } v ? v : null;
    public int GetInt(string key, int fallback) => int.TryParse(Query[key], out var v) ? v : fallback;
    public double GetDouble(string key, double fallback) =>
        double.TryParse(Query[key], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : fallback;
    public bool Flag(string key) => Query[key] is "1" or "true" or "yes" or "on";
}

public sealed class DevResponse
{
    public int Status { get; init; } = 200;
    public string ContentType { get; init; } = "application/json; charset=utf-8";
    public byte[] Body { get; init; } = [];

    public string BodyText => Encoding.UTF8.GetString(Body);

    private static readonly JsonSerializerOptions Compact = new() { WriteIndented = false, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never };
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    public static DevResponse Json(object? value, bool pretty = false, int status = 200) => new()
    {
        Status = status,
        Body = JsonSerializer.SerializeToUtf8Bytes(value, pretty ? Pretty : Compact),
    };

    public static DevResponse Text(string text, int status = 200) => new()
    {
        Status = status,
        ContentType = "text/plain; charset=utf-8",
        Body = Encoding.UTF8.GetBytes(text),
    };

    public static DevResponse Png(byte[] bytes) => new() { ContentType = "image/png", Body = bytes };

    public static DevResponse Error(int status, string message, object? hint = null) =>
        Json(new { error = message, hint }, status: status);
}

/// <summary>Thrown by handlers for caller mistakes (bad node id, no match...): becomes a 4xx JSON error.</summary>
public sealed class DevToolsException(string message, int status = 400, object? hint = null) : Exception(message)
{
    public int Status { get; } = status;
    public object? Hint { get; } = hint;
}
