namespace KsefGateway.Services;

public class KsefTokenStore
{
    private string? _token;
    private DateTime? _expiresAt;

    public void Save(string token, DateTime? expiresAt = null)
    {
        _token = token;
        _expiresAt = expiresAt;
    }

    public string? Get() => _token;

    public bool IsValid()
    {
        if (_token == null) return false;
        if (_expiresAt == null) return true;
        return DateTime.UtcNow < _expiresAt.Value.AddMinutes(-2); 
        // zostawiamy bufor 2 minut
    }
}
