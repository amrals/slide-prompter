namespace SlidePrompter.Models;

/// <summary>
/// Represents a single parsed slide entry from the presentation script.
/// </summary>
public sealed record SlideItem(int Index, string RawText, string SingleLineText)
{
    /// <summary>
    /// One-based slide number for display purposes.
    /// </summary>
    public int Number => Index + 1;

    /// <summary>
    /// Approximate word count for the slide.
    /// </summary>
    public int WordCount => string.IsNullOrWhiteSpace(SingleLineText)
        ? 0
        : SingleLineText.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>
    /// Short preview snippet for slide navigation lists.
    /// </summary>
    public string PreviewText => SingleLineText.Length <= 56
        ? SingleLineText
        : string.Concat(SingleLineText.AsSpan(0, 53), "...");
}

