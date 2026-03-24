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

        private static Block BuildSeparator()
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
