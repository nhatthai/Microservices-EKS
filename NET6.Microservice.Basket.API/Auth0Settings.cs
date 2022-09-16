namespace NET6.Microservice.Basket.API;

public class Auth0Settings
{
    public string? Authority { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? Audience { get; set; }
}