using System.Net.Http.Json;

/// <summary>Calls the Email microservice to deliver emails.</summary>
public class HttpEmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public HttpEmailService(HttpClient httpClient, string apiKey)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
    }

    public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/email/send");
        request.Headers.Add("X-Service-ApiKey", _apiKey);
        request.Content = JsonContent.Create(new { to, subject, body, isHtml });
        await _httpClient.SendAsync(request);
    }
}
