// ── Copyright notice ──────────────────────────────────────────────────────────────────
// (c) 2026 Arni Bjorgvinsson. All rights reserved.
// ─────────────────────────────────────────────────────────────────────────────────

using System.Reflection;

namespace InfoSlides.Mcp.Tools;

/// <summary>
/// The server instructions MCP clients show the model. Hosts that cannot load the skill (the Gemini app) get all
/// their guidance from here, so it carries the whole outcome and the working loop. There is one text per
/// <see cref="ToolProfile"/>. The texts are written next to the skill in the website repository
/// (<c>skill-source/instructions.*.txt</c>), checked there against the hosted tool list and forbidden terms, and
/// copied here by <c>scripts/sync-skills.py</c> as embedded resources, so the skill and the instructions have one
/// author and cannot drift apart. Tests here keep each text to tools that profile actually offers.
/// </summary>
public static class InfoSlidesServerInstructions
{
    /// <summary>The full-profile text (stdio server, API-key callers, Gemini).</summary>
    public static readonly string Full = Load("full");

    /// <summary>
    /// The hosted-profile text: the same working loop, only tools the hosted profile offers, and nothing about prices,
    /// trials, credit cards or upgrades (directory policies forbid selling from the assistant).
    /// </summary>
    public static readonly string Hosted = Load("hosted");

    /// <summary>Returns the instructions for a profile.</summary>
    /// <param name="profile">The caller's profile.</param>
    /// <returns>The instruction text.</returns>
    public static string For(ToolProfile profile) => profile == ToolProfile.Hosted ? Hosted : Full;

    private static string Load(string kind)
    {
        using var stream = typeof(InfoSlidesServerInstructions).Assembly.GetManifestResourceStream($"Instructions.{kind}.txt")
            ?? throw new InvalidOperationException($"The embedded instructions '{kind}' are missing; run scripts/sync-skills.py.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Trim();
    }
}
