using System.ComponentModel;
using InfoSlides.Core.Api;
using InfoSlides.Core.Models;
using InfoSlides.Core.Serialization;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace InfoSlides.Cli.Tools;

[McpServerToolType]
public sealed class DeviceTools(InfoSlidesApiClient api)
{
    [McpServerTool(Name = "create_device")]
    [Description("Register a display device (screen). Free tier allows exactly 1 active device — a " +
                 "DeviceLimitReached error includes an upgrade link. Resolution defaults to 1920x1080; " +
                 "use 1080x1920 for portrait screens.")]
    public Task<CallToolResult> CreateDevice(
        [Description("Human-readable device name, e.g. 'Lobby screen'.")] string name,
        [Description("Horizontal resolution in pixels (default 1920).")] int width = 1920,
        [Description("Vertical resolution in pixels (default 1080).")] int height = 1080,
        CancellationToken ct = default) =>
        ToolResults.Execute(
            () => api.CreateDeviceAsync(new CreateDeviceRequest(name, new Resolution(width, height)), ct),
            InfoSlidesJsonContext.Default.Device);

    [McpServerTool(Name = "list_devices", ReadOnly = true)]
    [Description("List registered devices with their resolutions.")]
    public Task<CallToolResult> ListDevices(CancellationToken ct = default) =>
        ToolResults.Execute(() => api.ListDevicesAsync(ct), InfoSlidesJsonContext.Default.ListDevice);

    [McpServerTool(Name = "get_device_status", ReadOnly = true)]
    [Description("Check whether a device is online, when it was last seen, and what it is currently " +
                 "playing — use this to verify a screen is actually showing the scheduled content.")]
    public Task<CallToolResult> GetDeviceStatus(
        [Description("Id of the device.")] string deviceId,
        CancellationToken ct = default) =>
        ToolResults.Execute(() => api.GetDeviceStatusAsync(deviceId, ct), InfoSlidesJsonContext.Default.DeviceStatus);

    [McpServerTool(Name = "assign_schedule")]
    [Description("Assign slideshow(s) to a device's playback loop. Succeeds even when aspect ratios " +
                 "differ, but returns an AspectMismatch warning — if you see one, fix the slideshow or " +
                 "device resolution so content isn't stretched or cropped.")]
    public Task<CallToolResult> AssignSchedule(
        [Description("Id of the device.")] string deviceId,
        [Description("Ids of the slideshows to play, in order.")] List<string> slideshowIds,
        CancellationToken ct = default) =>
        ToolResults.Execute(
            () => api.AssignScheduleAsync(deviceId, new AssignScheduleRequest(slideshowIds), ct),
            InfoSlidesJsonContext.Default.Schedule);

    [McpServerTool(Name = "get_stream_link", ReadOnly = true)]
    [Description("Get the live HLS stream URL for a device — open it in any HLS-capable player or " +
                 "point the physical screen at it.")]
    public Task<CallToolResult> GetStreamLink(
        [Description("Id of the device.")] string deviceId,
        CancellationToken ct = default) =>
        ToolResults.Execute(() => api.GetStreamLinkAsync(deviceId, ct), InfoSlidesJsonContext.Default.StreamLink);
}
