using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using KsefGateway.Options;

namespace KsefGateway.Http;

public class KsefAuthHandler : DelegatingHandler
{
    private readonly IOptions<KsefOptions> _opts;

    public KsefAuthHandler(IOptions<KsefOptions> opts) => _opts = opts;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        // jeśli kiedyś wpiszesz token w konfiguracji – poleci jako Bearer
        var token = _opts.Value.TechnicalToken;
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return base.SendAsync(request, ct);
    }
}
