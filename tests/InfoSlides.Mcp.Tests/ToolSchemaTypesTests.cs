// ── Copyright notice ──────────────────────────────────────────────────────────────────
// (c) 2026 Arni Bjorgvinsson. All rights reserved.
// ─────────────────────────────────────────────────────────────────────────────────

using System.Reflection;
using System.Text.Json;
using InfoSlides.Mcp.Tools;
using ModelContextProtocol.Server;
using Xunit;

namespace InfoSlides.Mcp.Tests;

/// <summary>The Claude directory scan rejects tool parameters whose JSON schema has no <c>type</c>.</summary>
public sealed class ToolSchemaTypesTests
{
    /// <summary>Every parameter of every tool declares a JSON schema type, so none is an untyped <c>{}</c>.</summary>
    [Fact]
    public void Every_tool_parameter_has_a_schema_type()
    {
        var options = new McpServerToolCreateOptions { SerializerOptions = InfoSlidesToolRegistration.CreateJsonOptions() };
        var untyped = new List<string>();

        foreach (var cls in typeof(TenantTools).Assembly.GetTypes()
                     .Where(t => t.GetCustomAttribute<McpServerToolTypeAttribute>() != null))
        {
            foreach (var method in cls.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                         .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null))
            {
                var tool = McpServerTool.Create(method, _ => null!, options);
                if (!tool.ProtocolTool.InputSchema.TryGetProperty("properties", out var props))
                {
                    continue;
                }

                foreach (var p in props.EnumerateObject())
                {
                    var v = p.Value;
                    if (!v.TryGetProperty("type", out _) && !v.TryGetProperty("anyOf", out _) && !v.TryGetProperty("enum", out _))
                    {
                        untyped.Add($"{tool.ProtocolTool.Name}.{p.Name}");
                    }
                }
            }
        }

        Assert.True(untyped.Count == 0, "Untyped parameters: " + string.Join(", ", untyped));
    }
}
