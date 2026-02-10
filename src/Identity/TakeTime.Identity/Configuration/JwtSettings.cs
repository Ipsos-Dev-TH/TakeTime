namespace TakeTime.Identity.Configuration;

public class JwtSettings
{
    public const string SectionName = "JWT";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "TakeTime.Platform";
    public string Audience { get; set; } = "TakeTime.Clients";
    public int AccessTokenExpirationMinutes { get; set; } = 60;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
