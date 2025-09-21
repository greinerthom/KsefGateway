using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using Ksef.Client;            // NSwag wygenerowany klient
using KsefGateway.Options;    // KsefOptions
using KsefGateway.Http;       // KsefAuthHandler
using KsefGateway.Models;     // ChallengeResponse, TokenRequest, TokenResponse
using System.Net.Http.Json;   // PostAsJsonAsync, ReadFromJsonAsync
using KsefGateway.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 🔹 Serilog
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

// 🔹 Options
builder.Services.Configure<KsefOptions>(builder.Configuration.GetSection("Ksef"));

// 🔹 Services
builder.Services.AddSingleton<KsefTokenStore>();
builder.Services.AddScoped<KsefAuthService>();

// 🔹 Auth handler
builder.Services.AddTransient<KsefAuthHandler>();

// 🔹 HttpClient z NSwag (IKsefApiClient)
builder.Services.AddHttpClient<IKsefApiClient, KsefApiClient>((sp, http) =>
{
    var opts = sp.GetRequiredService<IOptions<KsefOptions>>().Value;
    http.BaseAddress = new Uri($"{opts.BaseUrl.TrimEnd('/')}/api/v2/");
})
.AddHttpMessageHandler<KsefAuthHandler>()
.SetHandlerLifetime(TimeSpan.FromMinutes(5))
.AddPolicyHandler(HttpPolicyExtensions
    .HandleTransientHttpError()
    .OrResult(r => (int)r.StatusCode == 429)
    .WaitAndRetryAsync(3, i => TimeSpan.FromSeconds(Math.Pow(2, i))));

// 🔹 Surowy HttpClient do debugów
builder.Services.AddHttpClient("raw", (sp, http) =>
{
    var opts = sp.GetRequiredService<IOptions<KsefOptions>>().Value;
    http.BaseAddress = new Uri($"{opts.BaseUrl.TrimEnd('/')}/api/v2/");
})
.AddHttpMessageHandler<KsefAuthHandler>()
.SetHandlerLifetime(TimeSpan.FromMinutes(5))
.AddPolicyHandler(HttpPolicyExtensions
    .HandleTransientHttpError()
    .OrResult(r => (int)r.StatusCode == 429)
    .WaitAndRetryAsync(3, i => TimeSpan.FromSeconds(Math.Pow(2, i))));

// 🔹 Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ---------------------------
// Endpointy
// ---------------------------

// Zdrowie
app.MapGet("/", () => Results.Ok(new { ok = true, service = "KSeF Gateway" }));

// LOGIN: challenge + technical token → accessToken
app.MapPost("/ksef/login", async (IHttpClientFactory f, IOptions<KsefOptions> opt) =>
{
    try
    {
        var http = f.CreateClient("raw");

        // 1️⃣ Pobieramy challenge
        var res = await http.PostAsync("auth/challenge",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync();
            return Results.Problem($"❌ Nie udało się pobrać challenge. {err}",
                                   statusCode: (int)res.StatusCode);
        }

        var challenge = await res.Content.ReadFromJsonAsync<ChallengeResponse>();

        // 2️⃣ Wysyłamy token request z TechnicalToken
        var tokenReq = new TokenRequest(challenge!.challenge, opt.Value.TechnicalToken!);
        var reqJson = System.Text.Json.JsonSerializer.Serialize(tokenReq);

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "auth/token");
        requestMessage.Content = new StringContent(reqJson, Encoding.UTF8, "application/json");

        // jawnie ustawiamy nagłówki zgodne z wymaganiami ASCII
        requestMessage.Headers.Accept.Clear();
        requestMessage.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        var res2 = await http.SendAsync(requestMessage);

        var body = await res2.Content.ReadAsStringAsync();
        return Results.Json(new { status = (int)res2.StatusCode, body });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});

// RAW: limits (wymaga accessToken!)
app.MapGet("/ksef/raw-limits", async (IHttpClientFactory f) =>
{
    var http = f.CreateClient("raw");
    var res = await http.GetAsync("certificates/limits");
    var body = await res.Content.ReadAsStringAsync();
    return Results.Json(new { status = (int)res.StatusCode, body });
});

// NSwag client: publiczne certyfikaty (działa BEZ tokena)
app.MapGet("/ksef/public-keys", async (IKsefApiClient api) =>
{
    try
    {
        var list = await api.PublicKeyCertificatesAsync();
        return Results.Ok(list);
    }
    catch (ApiException ex)
    {
        return Results.Json(new { ex.StatusCode, ex.Message, ex.Response }, statusCode: (int)ex.StatusCode);
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});
// TEST: wysyłka faktury do KSeF
app.MapPost("/ksef/send-test-invoice", async (
    IHttpClientFactory f,
    IOptions<KsefOptions> opt,
    KsefAuthService auth
) =>
{
    try
    {
        var http = f.CreateClient("raw");

        // 🔑 Upewniamy się, że mamy token
        var token = await auth.EnsureTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            return Results.Problem("Brak ważnego accessToken – zaloguj się najpierw przez /ksef/login");
        }

        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // 📄 Prosta faktura XML (przykład minimalny)
        var sampleInvoice = @"<?xml version=""1.0"" encoding=""UTF-8""?>
            <Faktura>
              <Naglowek>
                <NumerFaktury>FV/1/2025</NumerFaktury>
                <DataWystawienia>2025-09-21</DataWystawienia>
                <SprzedawcaNIP>1234567890</SprzedawcaNIP>
                <NabywcaNIP>9876543210</NabywcaNIP>
                <Kwota>100.00</Kwota>
              </Naglowek>
            </Faktura>";

        var content = new StringContent(sampleInvoice, System.Text.Encoding.UTF8, "application/xml");

        // 🚀 Wysyłamy fakturę do KSeF
        var res = await http.PostAsync("invoices/send", content);

        var body = await res.Content.ReadAsStringAsync();
        return Results.Json(new { status = (int)res.StatusCode, body });
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});
app.Run();
