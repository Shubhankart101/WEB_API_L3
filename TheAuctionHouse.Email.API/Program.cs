var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var apiKey = app.Configuration["ServiceAuth:ApiKey"] ?? "email-service-api-key-2025";

app.MapPost("/api/email/send", async (SendEmailRequest request, HttpContext ctx) =>
{
    if (!ctx.Request.Headers.TryGetValue("X-Service-ApiKey", out var key) || key != apiKey)
        return Results.Unauthorized();

    // Log the email (replace with real SMTP integration as needed)
    app.Logger.LogInformation("[EMAIL] To: {To} | Subject: {Subject}", request.To, request.Subject);

    return Results.Ok(new { message = "Email sent." });
});

app.Run();

public record SendEmailRequest(string To, string Subject, string Body, bool IsHtml = true);


record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
