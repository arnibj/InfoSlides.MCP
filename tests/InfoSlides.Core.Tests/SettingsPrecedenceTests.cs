using InfoSlides.Core.Config;
using Xunit;

namespace InfoSlides.Core.Tests;

public sealed class SettingsPrecedenceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "infoslides-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static Func<string, string?> Env(params (string Key, string Value)[] pairs) =>
        key => pairs.Where(p => p.Key == key).Select(p => (string?)p.Value).FirstOrDefault();

    [Fact]
    public void Defaults_WhenNothingConfigured()
    {
        var settings = AppSettings.Resolve(getEnv: Env(), configDirectory: _dir);

        Assert.Equal(new Uri(AppSettings.DefaultApiUrl), settings.ApiUrl);
        Assert.Null(settings.Credential);
    }

    [Fact]
    public void StoredFiles_BeatDefaults()
    {
        var store = new CredentialStore(_dir);
        store.SaveConfig(new StoredConfig("https://api.staging.local"));
        store.SaveCredentials(new StoredCredentials(ApiKey: "isk_admin_file"));

        var settings = AppSettings.Resolve(getEnv: Env(), configDirectory: _dir);

        Assert.Equal(new Uri("https://api.staging.local"), settings.ApiUrl);
        Assert.Equal("isk_admin_file", settings.Credential);
    }

    [Fact]
    public void EnvVars_BeatStoredFiles()
    {
        var store = new CredentialStore(_dir);
        store.SaveConfig(new StoredConfig("https://api.staging.local"));
        store.SaveCredentials(new StoredCredentials(ApiKey: "isk_admin_file"));

        var settings = AppSettings.Resolve(
            getEnv: Env((AppSettings.ApiKeyEnvVar, "isk_admin_env"), (AppSettings.ApiUrlEnvVar, "https://api.env.local")),
            configDirectory: _dir);

        Assert.Equal(new Uri("https://api.env.local"), settings.ApiUrl);
        Assert.Equal("isk_admin_env", settings.Credential);
    }

    [Fact]
    public void Flags_BeatEverything()
    {
        var store = new CredentialStore(_dir);
        store.SaveCredentials(new StoredCredentials(ApiKey: "isk_admin_file"));

        var settings = AppSettings.Resolve(
            flagApiKey: "isk_admin_flag",
            flagApiUrl: "https://api.flag.local",
            getEnv: Env((AppSettings.ApiKeyEnvVar, "isk_admin_env")),
            configDirectory: _dir);

        Assert.Equal(new Uri("https://api.flag.local"), settings.ApiUrl);
        Assert.Equal("isk_admin_flag", settings.Credential);
    }

    [Fact]
    public void ExpiredSessionToken_IsIgnored()
    {
        var store = new CredentialStore(_dir);
        store.SaveCredentials(new StoredCredentials(
            SessionToken: "expired-token",
            ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(-5)));

        var settings = AppSettings.Resolve(getEnv: Env(), configDirectory: _dir);

        Assert.Null(settings.Credential);
    }

    [Fact]
    public void ApiKey_PreferredOverSessionToken()
    {
        var store = new CredentialStore(_dir);
        store.SaveCredentials(new StoredCredentials(
            ApiKey: "isk_admin_file",
            SessionToken: "session-token",
            ExpiresAt: DateTimeOffset.UtcNow.AddHours(1)));

        var settings = AppSettings.Resolve(getEnv: Env(), configDirectory: _dir);

        Assert.Equal("isk_admin_file", settings.Credential);
    }
}
