using System.Text;
using System.Text.Json;

namespace Gleanvolt.Mcp;

/// <summary>
/// The one thing that talks to the installation. Responses are handed on as the JSON the API produced,
/// not deserialised into local types and re-serialised: the model is the consumer, it reads JSON, and
/// every DTO defined here would be a second place for the contract to be wrong.
/// </summary>
internal sealed class GleanvoltClient(HttpClient http)
{
    /// <summary>
    /// Property names stay exactly as written on the request records — the API's document is
    /// camelCase and so are they, so there is no policy to get out of step with.
    /// </summary>
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal Task<string> GetAsync(string path, CancellationToken ct) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Get, path), ct);

    internal Task<string> PostAsync(string path, object body, CancellationToken ct) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Post, path) { Content = Body(body) }, ct);

    internal Task<string> PutAsync(string path, object body, CancellationToken ct) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Put, path) { Content = Body(body) }, ct);

    /// <summary>
    /// Serialised up front rather than through JsonContent.Create, which cannot report its own length
    /// and so goes out chunked. Kestrel reads that happily, but these bodies are a few hundred bytes
    /// and a declared Content-Length is what every proxy and every packet capture between here and the
    /// Pi expects to see.
    /// </summary>
    private static StringContent Body(object body) =>
        new(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");

    /// <summary>
    /// Failures come back as JSON the model can read rather than as thrown exceptions. A tool that
    /// throws tells the model only that something went wrong; the API's own 400 says <em>which field
    /// was wrong and why</em>, and that is the difference between the model correcting itself and the
    /// model retrying the same bad call.
    /// </summary>
    private async Task<string> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            using var response = await http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                return string.IsNullOrWhiteSpace(body) ? "{}" : body;
            }

            return JsonSerializer.Serialize(new
            {
                error = new
                {
                    status = (int)response.StatusCode,
                    reason = response.ReasonPhrase,
                    request = $"{request.Method} {request.RequestUri}",
                    detail = TryParse(body),
                },
            }, Json);
        }
        catch (HttpRequestException exception)
        {
            return JsonSerializer.Serialize(new
            {
                error = new
                {
                    status = 0,
                    reason = "The installation could not be reached.",
                    request = $"{request.Method} {request.RequestUri}",
                    detail = exception.Message,
                },
            }, Json);
        }
    }

    /// <summary>Keeps a ProblemDetails body structured, and a plain-text one readable.</summary>
    private static object? TryParse(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(body);
        }
        catch (JsonException)
        {
            return body;
        }
    }
}
