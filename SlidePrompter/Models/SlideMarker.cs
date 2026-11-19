namespace SlidePrompter.Models;

/// <summary>
/// Locates a slide section inside the raw editor text using exact character indices for gutter alignment.
/// </summary>
public sealed record SlideMarker(
    int SlideNumber,
    int SectionStartCharIndex,
    int FirstContentCharIndex,
    int SectionEndCharIndex,
    bool IsEmptyDraft);

