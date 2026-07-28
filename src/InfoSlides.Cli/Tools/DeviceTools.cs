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
    [Description("Register the physical screen the content will play on — the TV in reception, the " +
                 "menu board above the counter, the noticeboard in the corridor, the display in the " +
                 "waiting room, the monitor in the shop window (Icelandic: upplýsingaskjár, skjár). " +
                 "Do this once per screen. Free accounts allow exactly 1 active screen; a " +
                 "DeviceLimitReached error comes with an upgrade link. Resolution defaults to " +
                 "1920x1080 for a normal wall-mounted TV — use 1080x1920 for a screen turned on its " +
                 "end, which is common for menu boards and window displays.")]
    public Task<CallToolResult> CreateDevice(
        [Description("What this screen is and where it is, e.g. 'Lobby screen' or 'Menu board — counter'.")] string name,
        [Description("Screen width in pixels (default 1920 — landscape).")] int width = 1920,
        [Description("Screen height in pixels (default 1080 — landscape).")] int height = 1080,
        CancellationToken ct = default) =>
        ToolResults.Execute(
            () => api.CreateDeviceAsync(new CreateDeviceRequest(name, new Resolution(width, height)), ct),
            InfoSlidesJsonContext.Default.Device);

    [McpServerTool(Name = "list_devices", ReadOnly = true)]
    [Description("See every screen registered to this workspace and the shape each one is set up for. " +
                 "Use it to find the id of a screen before assigning content to it, or to check how " +
                 "many of the account's allowed screens are already used.")]
    public Task<CallToolResult> ListDevices(CancellationToken ct = default) =>
        ToolResults.Execute(() => api.ListDevicesAsync(ct), InfoSlidesJsonContext.Default.ListDevice);

    [McpServerTool(Name = "get_device_status", ReadOnly = true)]
    [Description("Find out whether a screen is actually on and what it is showing right now — is the " +
                 "TV live, when did it last check in, which content is playing. This is the tool to " +
                 "reach for when someone says the screen is blank, frozen, or showing the wrong thing.")]
    public Task<CallToolResult> GetDeviceStatus(
        [Description("Id of the device.")] string deviceId,
        CancellationToken ct = default) =>
        ToolResults.Execute(() => api.GetDeviceStatusAsync(deviceId, ct), InfoSlidesJsonContext.Default.DeviceStatus);

    [McpServerTool(Name = "assign_schedule")]
    [Description("Tell a screen what to play. Connects slideshows to a registered display, in the " +
                 "order given, as a continuous loop. The call succeeds even when the content's shape " +
                 "does not match the screen's, but returns an AspectMismatch warning — act on it, " +
                 "because it means the content will be stretched or cropped on a display people can " +
                 "see. Follow with get_stream_link to get the URL to open on the TV.")]
    public Task<CallToolResult> AssignSchedule(
        [Description("Id of the device.")] string deviceId,
        [Description("Ids of the slideshows to play, in order.")] List<string> slideshowIds,
        CancellationToken ct = default) =>
        ToolResults.Execute(
            () => api.AssignScheduleAsync(deviceId, new AssignScheduleRequest(slideshowIds), ct),
            InfoSlidesJsonContext.Default.Schedule);

    [McpServerTool(Name = "get_stream_link", ReadOnly = true)]
    [Description("Get the link that makes the content appear on the TV — the last step of any setup. " +
                 "Hand this URL to the user: opened in the smart TV's browser, the InfoSlides TV app, " +
                 "or any HLS-capable player, it starts playing their content. The link is stable, so " +
                 "the screen keeps working as content changes. A StreamNotReady warning means nothing " +
                 "is playable yet — usually because the content is still rendering.")]
    public Task<CallToolResult> GetStreamLink(
        [Description("Id of the device.")] string deviceId,
        CancellationToken ct = default) =>
        ToolResults.Execute(() => api.GetStreamLinkAsync(deviceId, ct), InfoSlidesJsonContext.Default.StreamLink);
}
