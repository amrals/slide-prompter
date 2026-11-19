using System.Text.RegularExpressions;
using SlidePrompter.Models;
using SlidePrompter.Services.Interfaces;

namespace SlidePrompter.Services;

/// <summary>
/// Splits presentation scripts on standalone '---' lines and produces both raw and single-line text representations.
/// </summary>
public sealed partial class ScriptParserService : IScriptParserService
{
    private const string SlideDelimiter = "---";

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex WhitespaceRunRegex();

    public IReadOnlyList<SlideItem> Parse(string? rawScript)
    {
        if (string.IsNullOrWhiteSpace(rawScript))
        {
            return Array.Empty<SlideItem>();
        }

        string normalized = rawScript.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] lines = normalized.Split('\n');

        var slides = new List<SlideItem>();
        var currentLines = new List<string>();

        foreach (string line in lines)
        {
            if (string.Equals(line.Trim(), SlideDelimiter, StringComparison.Ordinal))
            {
                FlushCurrentSlide(currentLines, slides);
            }
            else
            {
                currentLines.Add(line);
            }
        }

        FlushCurrentSlide(currentLines, slides);
        return slides;
    }

    public IReadOnlyList<SlideMarker> LocateSlideMarkers(string? rawScript)
    {
        if (string.IsNullOrEmpty(rawScript))
        {
            return new[] { new SlideMarker(1, 0, 0, 0, IsEmptyDraft: true) };
        }

        var lines = ScanRawLines(rawScript);
        var sections = new List<(int Start, int End, int? FirstContentStart, bool AfterDelimiter)>();

        int currentStart = 0;
        int? firstContent = null;
        bool hasLinesInSection = false;
        bool currentAfterDelimiter = false;
        int lastSectionEnd = 0;

        foreach (RawLineSpan line in lines)
        {
            ReadOnlySpan<char> slice = rawScript.AsSpan(line.Start, line.End - line.Start);
            ReadOnlySpan<char> trimmed = slice.Trim();

            if (trimmed.SequenceEqual(SlideDelimiter.AsSpan()))
            {
                if (hasLinesInSection)
                {
                    sections.Add((currentStart, lastSectionEnd, firstContent, currentAfterDelimiter));
                }

                firstContent = null;
                hasLinesInSection = false;
                currentAfterDelimiter = true;
                currentStart = line.NextStart;
                lastSectionEnd = line.NextStart;
            }
            else
            {
                if (!hasLinesInSection)
                {
                    currentStart = line.Start;
                    hasLinesInSection = true;
                }

                if (!firstContent.HasValue && !trimmed.IsEmpty)
                {
                    firstContent = line.Start;
                }

                lastSectionEnd = line.End;
            }
        }

        if (hasLinesInSection)
        {
            sections.Add((currentStart, lastSectionEnd, firstContent, currentAfterDelimiter));
        }

        var markers = new List<SlideMarker>(sections.Count);
        int slideNumber = 0;

        for (int i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            bool isLastSection = i == sections.Count - 1;

            if (section.FirstContentStart.HasValue)
            {
                slideNumber++;
                markers.Add(new SlideMarker(
                    slideNumber,
                    section.Start,
                    section.FirstContentStart.Value,
                    section.End,
                    IsEmptyDraft: false));
            }
            else if (isLastSection && (section.AfterDelimiter || markers.Count == 0))
            {
                markers.Add(new SlideMarker(
                    slideNumber + 1,
                    section.Start,
                    section.Start,
                    section.End,
                    IsEmptyDraft: true));
            }
        }

        if (markers.Count == 0)
        {
            markers.Add(new SlideMarker(1, 0, 0, rawScript.Length, IsEmptyDraft: true));
        }

        return markers;
    }

    private static List<RawLineSpan> ScanRawLines(string text)
    {
        var lines = new List<RawLineSpan>();
        int pos = 0;

        while (pos <= text.Length)
        {
            int lineStart = pos;
            while (pos < text.Length && text[pos] != '\r' && text[pos] != '\n')
            {
                pos++;
            }

            int lineEnd = pos;
            int nextStart = pos;
            bool hadNewLine = false;

            if (pos < text.Length)
            {
                hadNewLine = true;
                nextStart = (text[pos] == '\r' && pos + 1 < text.Length && text[pos + 1] == '\n')
                    ? pos + 2
                    : pos + 1;
            }

            lines.Add(new RawLineSpan(lineStart, lineEnd, nextStart));

            if (!hadNewLine)
            {
                break;
            }

            pos = nextStart;
        }

        return lines;
    }

    private readonly record struct RawLineSpan(int Start, int End, int NextStart);

    public string GetSampleScript()
    {
        return """
               Bem-vindos à demonstração do SlidePrompter! Este teleprompter fica centralizado logo abaixo da sua webcam para manter o contato visual natural durante toda a gravação.
               ---
               Enquanto você apresenta no PowerPoint ou Google Slides, basta usar as setas Direita e Esquerda — ou PageDown e PageUp — normalmente. O SlidePrompter acompanha cada troca de slide sem roubar o foco da sua apresentação.
               ---
               Perceba que cada novo slide já começa visível na zona de leitura à esquerda, com uma breve pausa inicial antes de deslizar suavemente. Assim você nunca espera o texto cruzar uma barra vazia.
               ---
               Precisa ajustar o ritmo ao vivo? Use Ctrl+Alt+Espaço para pausar ou retomar, Ctrl+Alt+Cima ou Baixo para mudar a velocidade em tempo real, e Ctrl+Alt+R para reiniciar o slide atual do começo.
               ---
               Por fim, com a opção Ocultar da Gravação ativada, esta barra permanece totalmente invisível no OBS Studio, Teams, Zoom e capturas de tela do Windows. Boa apresentação!
               """;
    }

    private static void FlushCurrentSlide(List<string> currentLines, List<SlideItem> slides)
    {
        if (currentLines.Count == 0)
        {
            return;
        }

        string rawBlock = string.Join('\n', currentLines).Trim();
        currentLines.Clear();

        if (string.IsNullOrWhiteSpace(rawBlock))
        {
            return;
        }

        string singleLine = WhitespaceRunRegex().Replace(rawBlock, " ").Trim();
        if (string.IsNullOrEmpty(singleLine))
        {
            return;
        }

        slides.Add(new SlideItem(slides.Count, rawBlock, singleLine));
    }
}

