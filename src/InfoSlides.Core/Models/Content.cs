using System.Text.Json;

namespace InfoSlides.Core.Models;

public sealed record SlideCondition(string Type, string Value);

public sealed record Slide(
    string Id,
    string? MediaUrl = null,
    string? TemplateId = null,
    double? DurationSeconds = null,
    int? Position = null,
    IReadOnlyList<SlideCondition>? Conditions = null);

public sealed record Slideshow(
    string Id,
    string Title,
    Resolution Resolution,
    IReadOnlyList<Slide>? Slides = null);

public sealed record CreateSlideshowRequest(
    string Title,
    Resolution? Resolution = null,
    IReadOnlyList<NewSlide>? Slides = null);

public sealed record NewSlide(
    string? MediaUrl = null,
    string? TemplateId = null,
    double? DurationSeconds = null);

public sealed record UpdateSlideshowRequest(
    string? Title = null,
    Resolution? Resolution = null,
    IReadOnlyList<string>? SlideOrder = null);

public sealed record AddMediaSlideRequest(
    string MediaUrl,
    double? DurationSeconds = null,
    int? Position = null);

public sealed record SetConditionsRequest(IReadOnlyList<SlideCondition> Conditions);

public sealed record Template(
    string Id,
    string Title,
    JsonElement? SampleJson = null,
    string? Html = null,
    string? Css = null);

public sealed record CreateTemplateRequest(
    string Title,
    string? Prompt = null,
    JsonElement? SampleJson = null,
    string? Html = null,
    string? Css = null);

public sealed record GalleryItem(
    string Id,
    string Title,
    string? Description,
    string? PreviewUrl,
    Resolution? Resolution);
