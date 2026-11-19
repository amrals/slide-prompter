using SlidePrompter.Models;

namespace SlidePrompter.Services.Interfaces;

/// <summary>
/// Parses raw presentation scripts separated by '---' lines into structured slides.
/// </summary>
public interface IScriptParserService
{
    IReadOnlyList<SlideItem> Parse(string? rawScript);

    IReadOnlyList<SlideMarker> LocateSlideMarkers(string? rawScript);

    string GetSampleScript();
}

