namespace KsefGateway.Models;

public record ChallengeResponse(string challenge, string timestamp);
public record TokenRequest(string challenge, string token);
public record TokenResponse(string accessToken, string issuedAt, string expiresAt);

