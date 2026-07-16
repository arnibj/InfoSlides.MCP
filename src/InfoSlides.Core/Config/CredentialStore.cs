using System.Text.Json;
using InfoSlides.Core.Serialization;

namespace InfoSlides.Core.Config;

public sealed record StoredCredentials(
    string? ApiKey = null,
    string? SessionToken = null,
    DateTimeOffset? ExpiresAt = null,
    string? TenantId = null,
    string? Email = null);

public sealed record StoredConfig(string? ApiUrl = null);

/// <summary>
/// File-based credential/config storage under ~/.infoslides. Files are written 0600 (owner
/// read/write only) on Unix; OS keychain integration is deliberately deferred to v2 because no
/// AOT-compatible cross-platform keychain library exists.
/// </summary>
public sealed class CredentialStore(string directory)
{
    public string Directory { get; } = directory;

    public string CredentialsPath => Path.Combine(Directory, "credentials.json");
    public string ConfigPath => Path.Combine(Directory, "config.json");

    public static string DefaultDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".infoslides");

    public StoredCredentials? LoadCredentials() =>
        Load(CredentialsPath, InfoSlidesJsonContext.Default.StoredCredentials);

    public StoredConfig? LoadConfig() =>
        Load(ConfigPath, InfoSlidesJsonContext.Default.StoredConfig);

    public void SaveCredentials(StoredCredentials credentials) =>
        Save(CredentialsPath, JsonSerializer.Serialize(credentials, InfoSlidesJsonContext.Default.StoredCredentials));

    public void SaveConfig(StoredConfig config) =>
        Save(ConfigPath, JsonSerializer.Serialize(config, InfoSlidesJsonContext.Default.StoredConfig));

    public void DeleteCredentials()
    {
        if (File.Exists(CredentialsPath))
        {
            File.Delete(CredentialsPath);
        }
    }

    private static T? Load<T>(string path, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
        where T : class
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(path), typeInfo);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void Save(string path, string json)
    {
        System.IO.Directory.CreateDirectory(Directory);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(Directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        File.WriteAllText(path, json);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
