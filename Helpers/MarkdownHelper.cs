using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace MortysDLP.Helpers
{
    internal static partial class MarkdownHelper
    {
        // ── Inline patterns ──────────────────────────────────────────────────────
        // Order matters: bold+italic (***) before bold (**) before italic (*) before code (`)
        [GeneratedRegex(@"\*\*\*(.+?)\*\*\*|\*\*(.+?)\*\*|\*([^*\n]+?)\*|`([^`\n]+?)`", RegexOptions.Singleline)]
        private static partial Regex InlineRegex();

        [GeneratedRegex(@"^(#{1,6})\s+(.+)")]
        private static partial Regex HeadingRegex();

        [GeneratedRegex(@"^[ \t]*[-*+]\s+(.*)")]
        private static partial Regex BulletRegex();

        [GeneratedRegex(@"^[ \t]*\d+\.\s+(.*)")]
        private static partial Regex NumberedRegex();

        [GeneratedRegex(@"^[-*_]{3,}\s*$")]
        private static partial Regex HrRegex();

        // ── HTML-Normalisierung ──────────────────────────────────────────────────
        // GitHub-Release-Notizen enthalten oft HTML (von Hand eingefügt, aus einem
        // WYSIWYG-Editor, oder GitHubs auto-generierte "What's Changed"-Blöcke). Der
        // zeilenbasierte Parser unten versteht kein HTML und zeigte die Tags wörtlich an.
        // Diese Vorstufe übersetzt die gängigen Block- und Inline-Tags nach Markdown und
        // entfernt den Rest.
        [GeneratedRegex(@"<\s*/?\s*(ul|ol|div|section|article|details|summary|blockquote|table|thead|tbody|tr|figure)\b[^>]*>", RegexOptions.IgnoreCase)]
        private static partial Regex HtmlStripBlockRegex();

        [GeneratedRegex(@"<\s*h([1-6])\b[^>]*>(.*?)<\s*/\s*h\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
        private static partial Regex HtmlHeadingRegex();

        [GeneratedRegex(@"<\s*li\b[^>]*>(.*?)<\s*/\s*li\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
        private static partial Regex HtmlListItemRegex();

        [GeneratedRegex(@"<\s*p\b[^>]*>(.*?)<\s*/\s*p\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
        private static partial Regex HtmlParagraphRegex();

        [GeneratedRegex(@"<\s*a\b[^>]*>(.*?)<\s*/\s*a\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
        private static partial Regex HtmlLinkRegex();

        [GeneratedRegex(@"<\s*br\s*/?\s*>", RegexOptions.IgnoreCase)]
        private static partial Regex HtmlBreakRegex();

        [GeneratedRegex(@"<\s*hr\s*/?\s*>", RegexOptions.IgnoreCase)]
        private static partial Regex HtmlRuleRegex();

        [GeneratedRegex(@"<\s*/?\s*(strong|b)\s*>", RegexOptions.IgnoreCase)]
        private static partial Regex HtmlBoldRegex();

        [GeneratedRegex(@"<\s*/?\s*(em|i)\s*>", RegexOptions.IgnoreCase)]
        private static partial Regex HtmlItalicRegex();

        [GeneratedRegex(@"<\s*/?\s*(code|kbd|samp|tt)\s*>", RegexOptions.IgnoreCase)]
        private static partial Regex HtmlCodeRegex();

        [GeneratedRegex(@"<[^>]+>")]
        private static partial Regex HtmlAnyTagRegex();

        [GeneratedRegex(@"\n{3,}")]
        private static partial Regex ExcessBlankLinesRegex();

        /// <summary>Übersetzt HTML-Auszeichnung in Markdown, damit der zeilenbasierte Parser sie
        /// versteht. Ist kein <c>&lt;</c> im Text, wird er unverändert zurückgegeben.</summary>
        internal static string NormalizeHtml(string text)
        {
            if (string.IsNullOrEmpty(text) || !text.Contains('<'))
                return text;

            text = HtmlHeadingRegex().Replace(text, m =>
                "\n" + new string('#', m.Groups[1].Value[0] - '0') + " " + m.Groups[2].Value.Trim() + "\n");
            text = HtmlListItemRegex().Replace(text, m => "\n- " + m.Groups[1].Value.Trim() + "\n");
            text = HtmlParagraphRegex().Replace(text, m => "\n" + m.Groups[1].Value.Trim() + "\n\n");
            text = HtmlLinkRegex().Replace(text, m => m.Groups[1].Value.Trim());
            text = HtmlBreakRegex().Replace(text, "\n");
            text = HtmlRuleRegex().Replace(text, "\n---\n");
            text = HtmlBoldRegex().Replace(text, "**");
            text = HtmlItalicRegex().Replace(text, "*");
            text = HtmlCodeRegex().Replace(text, "`");
            text = HtmlStripBlockRegex().Replace(text, "\n");
            text = HtmlAnyTagRegex().Replace(text, string.Empty);   // alles Übrige raus

            // Entities erst jetzt auflösen: so wird ein bewusst geschriebenes "&lt;code&gt;"
            // nicht doch noch als Tag verarbeitet.
            text = System.Net.WebUtility.HtmlDecode(text);

            return ExcessBlankLinesRegex().Replace(text, "\n\n").Trim();
        }

        // ── Public entry point ───────────────────────────────────────────────────

        public static FlowDocument ToFlowDocument(string markdown)
        {
            var doc = new FlowDocument
            {
                FontFamily  = new FontFamily("Segoe UI"),
                FontSize    = 13,
                PagePadding = new Thickness(0),
                LineHeight  = double.NaN
            };

            if (string.IsNullOrWhiteSpace(markdown))
                return doc;

            markdown = NormalizeHtml(markdown);

            var lines = markdown.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            int i = 0;

            while (i < lines.Length)
            {
                string line = lines[i];

                // ── Horizontal rule ──────────────────────────────────────────────
                if (HrRegex().IsMatch(line))
                {
                    doc.Blocks.Add(BuildSeparator());
                    i++;
                    continue;
                }

                // ── Heading ──────────────────────────────────────────────────────
                var hm = HeadingRegex().Match(line);
                if (hm.Success)
                {
                    doc.Blocks.Add(BuildHeading(hm.Groups[2].Value, hm.Groups[1].Length));
                    i++;
                    continue;
                }

                // ── Bullet list ──────────────────────────────────────────────────
                if (BulletRegex().IsMatch(line))
                {
                    var list = new List
                    {
                        MarkerStyle = TextMarkerStyle.Disc,
                        Margin      = new Thickness(18, 2, 0, 2),
                        Padding     = new Thickness(4, 0, 0, 0)
                    };
                    while (i < lines.Length)
                    {
                        var bm = BulletRegex().Match(lines[i]);
                        if (!bm.Success) break;
                        list.ListItems.Add(BuildListItem(bm.Groups[1].Value));
                        i++;
                    }
                    doc.Blocks.Add(list);
                    continue;
                }

                // ── Numbered list ────────────────────────────────────────────────
                if (NumberedRegex().IsMatch(line))
                {
                    var list = new List
                    {
                        MarkerStyle = TextMarkerStyle.Decimal,
                        Margin      = new Thickness(18, 2, 0, 2),
                        Padding     = new Thickness(4, 0, 0, 0)
                    };
                    while (i < lines.Length)
                    {
                        var nm = NumberedRegex().Match(lines[i]);
                        if (!nm.Success) break;
                        list.ListItems.Add(BuildListItem(nm.Groups[1].Value));
                        i++;
                    }
                    doc.Blocks.Add(list);
                    continue;
                }

                // ── Empty line ───────────────────────────────────────────────────
                if (string.IsNullOrWhiteSpace(line))
                {
                    i++;
                    continue;
                }

                // ── Normal paragraph ─────────────────────────────────────────────
                doc.Blocks.Add(new Paragraph(BuildInlineSpan(line))
                {
                    Margin = new Thickness(0, 1, 0, 3)
                });
                i++;
            }

            return doc;
        }

        // ── Block builders ────────────────────────────────────────────────────────

        private static Paragraph BuildHeading(string text, int level)
        {
            double[] sizes = [22, 18, 15, 14, 13, 13];
            double size = level >= 1 && level <= 6 ? sizes[level - 1] : 13;

            return new Paragraph(BuildInlineSpan(text))
            {
                FontSize   = size,
                FontWeight = FontWeights.SemiBold,
                Margin     = new Thickness(0, level == 1 ? 6 : 4, 0, 2)
            };
        }

        private static ListItem BuildListItem(string text)
        {
            return new ListItem(new Paragraph(BuildInlineSpan(text))
            {
                Margin = new Thickness(0, 1, 0, 1)
            });
        }

        private static BlockUIContainer BuildSeparator()
        {
            return new BlockUIContainer(new Separator
            {
                Margin  = new Thickness(0, 4, 0, 4),
                Opacity = 0.3
            });
        }

        // ── Inline parser ─────────────────────────────────────────────────────────

        private static Span BuildInlineSpan(string text)
        {
            var span     = new Span();
            int lastIndex = 0;

            foreach (Match m in InlineRegex().Matches(text))
            {
                // plain text before this match
                if (m.Index > lastIndex)
                    span.Inlines.Add(new Run(text[lastIndex..m.Index]));

                if (m.Groups[1].Success)        // ***bold-italic***
                {
                    span.Inlines.Add(new Bold(new Italic(new Run(m.Groups[1].Value))));
                }
                else if (m.Groups[2].Success)   // **bold**
                {
                    span.Inlines.Add(new Bold(new Run(m.Groups[2].Value)));
                }
                else if (m.Groups[3].Success)   // *italic*
                {
                    span.Inlines.Add(new Italic(new Run(m.Groups[3].Value)));
                }
                else if (m.Groups[4].Success)   // `code`
                {
                    span.Inlines.Add(new Run(m.Groups[4].Value)
                    {
                        FontFamily = new FontFamily("Cascadia Mono, Consolas, Courier New"),
                        Background = new SolidColorBrush(Color.FromArgb(40, 128, 128, 128))
                    });
                }

                lastIndex = m.Index + m.Length;
            }

            // remaining plain text
            if (lastIndex < text.Length)
                span.Inlines.Add(new Run(text[lastIndex..]));

            return span;
        }
    }
}
