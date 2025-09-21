namespace KsefGateway.Config;

public class KsefOptions
{
    public string BaseUrl { get; set; } = "";
    public AuthOptions Auth { get; set; } = new();

    public class AuthOptions
    {
        public string BearerToken { get; set; } = "";
    }
}
