namespace InfoSlides.Core.Config;

/// <summary>
/// Resolved runtime settings. Precedence (highest wins): CLI flags, environment variables
/// (INFOSLIDES_API_KEY / INFOSLIDES_API_URL), stored credentials/config in ~/.infoslides,
/// then the default API URL.
/// </summary>
public sealed record AppSettings(Uri ApiUrl, string? Credential, string ConfigDirectory)
{
    public const string DefaultApiUrl = "https://api.infoslides.app";
    public const string ApiKeyEnvVar = "INFOSLIDES_API_KEY";
    public const string ApiUrlEnvVar = "INFOSLIDES_API_URL";

    public static AppSettings Resolve(
        string? flagApiKey = null,
        string? flagApiUrl = null,
        Func<string, string?>? getEnv = null,
        string? configDirectory = null)
    {
        getEnv ??= Environment.GetEnvironmentVariable;
        configDirectory ??= CredentialStore.DefaultDirectory();

        var store = new CredentialStore(configDirectory);
        var credentials = store.LoadCredentials();
        var config = store.LoadConfig();

        var apiUrl = FirstNonEmpty(flagApiUrl, getEnv(ApiUrlEnvVar), config?.ApiUrl) ?? DefaultApiUrl;
        var credential = FirstNonEmpty(
            flagApiKey,
            getEnv(ApiKeyEnvVar),
            credentials?.ApiKey,
            SessionTokenIfValid(credentials));

        return new AppSettings(new Uri(apiUrl, UriKind.Absolute), credential, configDirectory);
    }

    private static string? SessionTokenIfValid(StoredCredentials? credentials) =>
        credentials?.SessionToken is { Length: > 0 } token &&
        (credentials.ExpiresAt is null || credentials.ExpiresAt > DateTimeOffset.UtcNow)
            ? token
            : null;

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
