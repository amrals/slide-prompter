using SlidePrompter.Services;
using Xunit;

namespace SlidePrompter.Tests;

public sealed class ScriptParserServiceTests
{
    private readonly ScriptParserService _parser = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \r\n\t  ")]
    public void Parse_EmptyOrWhitespaceInput_ReturnsEmptyList(string? input)
    {
        var slides = _parser.Parse(input);

        Assert.Empty(slides);
    }

    [Fact]
    public void Parse_SingleSlideWithoutDelimiter_ReturnsSingleSlide()
    {
        var slides = _parser.Parse("Olá pessoal, vamos começar.");

        Assert.Single(slides);
        Assert.Equal(0, slides[0].Index);
        Assert.Equal(1, slides[0].Number);
        Assert.Equal("Olá pessoal, vamos começar.", slides[0].SingleLineText);
        Assert.Equal("Olá pessoal, vamos começar.", slides[0].RawText);
    }

    [Fact]
    public void Parse_MultipleSlidesSeparatedByDashes_ParsesAllSlidesInOrder()
    {
        string script = "Slide 1 texto\r\n---\r\nSlide 2 texto\n---\nSlide 3 texto";

        var slides = _parser.Parse(script);

        Assert.Equal(3, slides.Count);
        Assert.Equal("Slide 1 texto", slides[0].SingleLineText);
        Assert.Equal("Slide 2 texto", slides[1].SingleLineText);
        Assert.Equal("Slide 3 texto", slides[2].SingleLineText);
        Assert.Equal(1, slides[0].Number);
        Assert.Equal(2, slides[1].Number);
        Assert.Equal(3, slides[2].Number);
    }

    [Fact]
    public void Parse_MultiLineSlide_ConvertsLineBreaksToSingleSpacesInSingleLineText()
    {
        string script = "Primeira linha.\r\nSegunda linha.\n\n   Terceira\tlinha.";

        var slides = _parser.Parse(script);

        Assert.Single(slides);
        Assert.Equal("Primeira linha. Segunda linha. Terceira linha.", slides[0].SingleLineText);
        Assert.Contains("\n", slides[0].RawText);
        Assert.Equal(6, slides[0].WordCount);
    }

    [Fact]
    public void Parse_DelimiterWithSurroundingSpaces_SplitsProperlyAndIgnoresInlineDashes()
    {
        string script = """
                        Texto com traços inline --- no meio da frase.
                          ---  
                        Segundo slide com ---- quatro traços na linha.
                        """;

        var slides = _parser.Parse(script);

        Assert.Equal(2, slides.Count);
        Assert.Equal("Texto com traços inline --- no meio da frase.", slides[0].SingleLineText);
        Assert.Equal("Segundo slide com ---- quatro traços na linha.", slides[1].SingleLineText);
    }

    [Fact]
    public void Parse_LeadingTrailingAndConsecutiveDelimiters_IgnoresEmptySections()
    {
        string script = """
                        ---
                        Slide A
                        ---
                        
                        ---
                        Slide B
                        ---
                        """;

        var slides = _parser.Parse(script);

        Assert.Equal(2, slides.Count);
        Assert.Equal("Slide A", slides[0].SingleLineText);
        Assert.Equal("Slide B", slides[1].SingleLineText);
        Assert.Equal(0, slides[0].Index);
        Assert.Equal(1, slides[1].Index);
    }

    [Fact]
    public void GetSampleScript_ProducesValidMultiSlideScript()
    {
        string sample = _parser.GetSampleScript();

        var slides = _parser.Parse(sample);

        Assert.True(slides.Count >= 4);
        Assert.All(slides, slide =>
        {
            Assert.False(string.IsNullOrWhiteSpace(slide.SingleLineText));
            Assert.DoesNotContain('\n', slide.SingleLineText);
            Assert.DoesNotContain('\r', slide.SingleLineText);
        });
    }

    [Fact]
    public void LocateSlideMarkers_CrlfMultiSlideScript_PreservesExactCharacterIndices()
    {
        string script = "Primeiro slide\r\n---\r\nSegundo slide\r\n---\r\nTerceiro slide";

        var markers = _parser.LocateSlideMarkers(script);

        Assert.Equal(3, markers.Count);
        Assert.Equal(1, markers[0].SlideNumber);
        Assert.Equal("Primeiro slide", script.Substring(markers[0].FirstContentCharIndex, "Primeiro slide".Length));
        Assert.False(markers[0].IsEmptyDraft);

        Assert.Equal(2, markers[1].SlideNumber);
        Assert.Equal("Segundo slide", script.Substring(markers[1].FirstContentCharIndex, "Segundo slide".Length));
        Assert.False(markers[1].IsEmptyDraft);

        Assert.Equal(3, markers[2].SlideNumber);
        Assert.Equal("Terceiro slide", script.Substring(markers[2].FirstContentCharIndex, "Terceiro slide".Length));
        Assert.False(markers[2].IsEmptyDraft);
    }

    [Fact]
    public void LocateSlideMarkers_TrailingDelimiterWithNewLine_EmitsEmptyDraftForNextSlide()
    {
        string script = "Slide 1\r\n---\r\nSlide 2\r\n---\r\n";

        var markers = _parser.LocateSlideMarkers(script);

        Assert.Equal(3, markers.Count);
        Assert.Equal(1, markers[0].SlideNumber);
        Assert.False(markers[0].IsEmptyDraft);
        Assert.Equal(2, markers[1].SlideNumber);
        Assert.False(markers[1].IsEmptyDraft);
        Assert.Equal(3, markers[2].SlideNumber);
        Assert.True(markers[2].IsEmptyDraft);
        Assert.Equal(script.Length, markers[2].FirstContentCharIndex);
    }

    [Fact]
    public void LocateSlideMarkers_BlankLinesAfterDelimiter_AlignsBadgeToFirstContentLine()
    {
        string script = "Slide 1\r\n---\r\n\r\nConteúdo do slide 2";

        var markers = _parser.LocateSlideMarkers(script);

        Assert.Equal(2, markers.Count);
        Assert.Equal(2, markers[1].SlideNumber);
        Assert.True(markers[1].FirstContentCharIndex > markers[1].SectionStartCharIndex);
        Assert.StartsWith("Conteúdo do slide 2", script.Substring(markers[1].FirstContentCharIndex));
    }

    [Fact]
    public void LocateSlideMarkers_EmptyScript_ReturnsSingleDraftSlide1()
    {
        var markers = _parser.LocateSlideMarkers(string.Empty);

        Assert.Single(markers);
        Assert.Equal(1, markers[0].SlideNumber);
        Assert.True(markers[0].IsEmptyDraft);
    }
}

