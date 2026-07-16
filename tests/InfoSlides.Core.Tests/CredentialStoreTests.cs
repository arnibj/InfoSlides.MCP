using InfoSlides.Core.Config;
using Xunit;

namespace InfoSlides.Core.Tests;

public sealed class CredentialStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "infoslides-tests-" + Guid.NewGuid().ToString("N"));
    private readonly CredentialStore _store;

    public CredentialStoreTests() => _store = new CredentialStore(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Credentials_RoundTrip()
    {
        var credentials = new StoredCredentials("isk_admin_x", "tok",
            new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero), "t1", "o@a.test");

        _store.SaveCredentials(credentials);

        Assert.Equal(credentials, _store.LoadCredentials());
    }

    [Fact]
    public void MissingFiles_ReturnNull()
    {
        Assert.Null(_store.LoadCredentials());
        Assert.Null(_store.LoadConfig());
    }

    [Fact]
    public void CorruptedFile_ReturnsNull()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(_store.CredentialsPath, "{ not json");

        Assert.Null(_store.LoadCredentials());
    }

    [Fact]
    public void CredentialsFile_IsOwnerOnly_OnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        _store.SaveCredentials(new StoredCredentials(ApiKey: "isk_admin_x"));

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(_store.CredentialsPath));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            File.GetUnixFileMode(_dir));
    }

    [Fact]
    public void DeleteCredentials_RemovesFile()
    {
        _store.SaveCredentials(new StoredCredentials(ApiKey: "isk_admin_x"));

        _store.DeleteCredentials();

        Assert.False(File.Exists(_store.CredentialsPath));
        _store.DeleteCredentials();
    }
}
