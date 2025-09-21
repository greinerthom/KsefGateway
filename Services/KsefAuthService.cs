using Microsoft.Extensions.Options;
using KsefGateway.Options;
using KsefGateway.Models;
using System.Net.Http.Json;

namespace KsefGateway.Services;

public class KsefAuthService
{
    private readonly IHttpClientFactory _factory;
    private readonly KsefTokenStore _store;
    private readonly KsefOptions _options;

    public KsefAuthService(IHttpClientFactory factory, KsefTokenStore store, IOptions<KsefOptions> options)
    {
        _factory = factory;
        _store = store;
        _options = options.Value;
    }

    public async Task<string?> EnsureTokenAsync()
    {
        if (_store.IsValid())
            return _store.Get();

        var http = _factory.CreateClient("raw");

        // 🔹 krok 1: pobranie challenge
        var res = await http.PostAsync("auth/challenge",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        if (!res.IsSuccessStatusCode)
            throw new Exception("❌ Nie udało się pobrać challenge");

        var challenge = await res.Content.ReadFromJsonAsync<ChallengeResponse>();

        // 🔹 krok 2: token request
        var tokenReq = new TokenRequest(challenge!.challenge, _options.TechnicalToken!);
        var res2 = await http.PostAsJsonAsync("auth/token", tokenReq);

        if (!res2.IsSuccessStatusCode)
            throw new Exception("❌ Nie udało się pobrać tokena");

        var tokenResp = await res2.Content.ReadFromJsonAsync<TokenResponse>();

        // 💾 zapis w pamięci
        var expires = DateTime.TryParse(tokenResp!.expiresAt, out var exp)
            ? exp
            : DateTime.UtcNow.AddMinutes(30);

        _store.Save(tokenResp!.accessToken, expires);

        return tokenResp.accessToken;
    }
}
