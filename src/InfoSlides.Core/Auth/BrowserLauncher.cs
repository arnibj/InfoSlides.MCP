using System.Diagnostics;

namespace InfoSlides.Core.Auth;

public static class BrowserLauncher
{
    /// <summary>Opens a URL in the default browser; returns false so callers can print the URL instead.</summary>
    public static bool TryOpen(string url)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", url);
            }
            else
            {
                Process.Start("xdg-open", url);
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
