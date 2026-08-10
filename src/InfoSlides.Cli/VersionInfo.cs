using System.Reflection;

namespace InfoSlides.Cli;

internal static class VersionInfo
{
    // Read from the assembly's own version metadata — set at publish time by the release
    // workflow's `-p:Version=${GITHUB_REF_NAME#v}` (falling back to the csproj's <Version> for
    // a local dev build) — rather than a second, hand-maintained constant. Keeping two copies in
    // sync is exactly what shipped a 1.3.1 binary whose own --version said 1.3.0.
    public static readonly string Version = FormatVersion();

    private static string FormatVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        // GetName().Version is always 4-part (Major.Minor.Build.Revision); this project's
        // releases are 3-part semver, and the unused Revision defaults to 0 rather than -1, so
        // it must be dropped explicitly — comparing "1.3.1" against "1.3.1.0" via Version.Parse
        // (as UpdateChecker.IsNewer does) would otherwise treat them as different versions.
        return v is null ? "0.0.0" : $"{v.Major}.{v.Minor}.{v.Build}";
    }
}
