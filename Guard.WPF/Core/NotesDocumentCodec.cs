using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Xml;
using System.Xml.Linq;

namespace Guard.WPF.Core
{
    /// <summary>
    /// Reads the text-only subset of WPF XAML produced for token notes without
    /// allowing the XAML loader to instantiate types from backup-controlled data.
    /// </summary>
    internal static class NotesDocumentCodec
    {
        private const int MaxCharacters = 1_000_000;
        private const int MaxBytes = 4_000_000;
        private const int MaxDepth = 32;
        private const int MaxElements = 10_000;
        private static readonly XNamespace PresentationNamespace =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        internal static FlowDocument Load(byte[] notes)
        {
            ArgumentNullException.ThrowIfNull(notes);
            if (notes.Length > MaxBytes)
            {
                throw new InvalidDataException("Notes exceed the supported size.");
            }

            string text = Encoding.UTF8.GetString(notes).TrimStart('\uFEFF');
            var document = new FlowDocument();
            if (!text.AsSpan().TrimStart().StartsWith("<", StringComparison.Ordinal))
            {
                document.Blocks.Add(new Paragraph(new Run(text)));
                return document;
            }

            XDocument xaml = ParseXml(text);
            XElement root = xaml.Root ?? throw new InvalidDataException("Notes are empty.");
            ValidateElement(root, 0, new ElementCounter());

            if (root.Name == PresentationNamespace + "FlowDocument")
            {
                AddBlocks(root, document.Blocks, 1);
            }
            else if (root.Name == PresentationNamespace + "Section")
            {
                document.Blocks.Add(CreateSection(root, 1));
            }
            else
            {
                throw new InvalidDataException("Notes do not contain a supported document.");
            }

            return document;
        }

        private static XDocument ParseXml(string text)
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaxCharacters,
                MaxCharactersFromEntities = 0,
            };
            using var stringReader = new StringReader(text);
            using XmlReader reader = XmlReader.Create(stringReader, settings);
            return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        }

        private static void ValidateElement(XElement element, int depth, ElementCounter counter)
        {
            if (depth > MaxDepth)
            {
                throw new InvalidDataException("Notes exceed the supported nesting depth.");
            }
            if (++counter.Count > MaxElements)
            {
                throw new InvalidDataException("Notes contain too many elements.");
            }
            if (
                element.Name.Namespace != PresentationNamespace
                || !IsAllowedElement(element.Name.LocalName)
            )
            {
                throw new InvalidDataException($"Unsupported notes element: {element.Name}.");
            }
            if (
                element.Attributes().Any(attribute =>
                    !attribute.IsNamespaceDeclaration
                    && attribute.Name.Namespace != XNamespace.None
                    && attribute.Name.Namespace != XNamespace.Xml
                )
            )
            {
                throw new InvalidDataException("Notes contain an unsupported namespaced attribute.");
            }

            foreach (XElement child in element.Elements())
            {
                ValidateElement(child, depth + 1, counter);
            }
        }

        private static bool IsAllowedElement(string name) =>
            name is "FlowDocument"
                or "Section"
                or "Paragraph"
                or "List"
                or "ListItem"
                or "Run"
                or "Span"
                or "Bold"
                or "Italic"
                or "Underline"
                or "Hyperlink"
                or "LineBreak";

        private static Section CreateSection(XElement element, int depth)
        {
            var section = new Section();
            ApplyFormatting(element, section);
            AddBlocks(element, section.Blocks, depth);
            return section;
        }

        private static void AddBlocks(XElement parent, BlockCollection blocks, int depth)
        {
            foreach (XElement element in parent.Elements())
            {
                switch (element.Name.LocalName)
                {
                    case "Section":
                        blocks.Add(CreateSection(element, depth + 1));
                        break;
                    case "Paragraph":
                        blocks.Add(CreateParagraph(element, depth + 1));
                        break;
                    case "List":
                        blocks.Add(CreateList(element, depth + 1));
                        break;
                    default:
                        throw new InvalidDataException(
                            $"Element {element.Name.LocalName} is not valid in a notes block."
                        );
                }
            }
        }

        private static Paragraph CreateParagraph(XElement element, int depth)
        {
            var paragraph = new Paragraph();
            ApplyFormatting(element, paragraph);
            if (
                Enum.TryParse(
                    element.Attribute("TextAlignment")?.Value,
                    true,
                    out TextAlignment alignment
                )
                && Enum.IsDefined(alignment)
            )
            {
                paragraph.TextAlignment = alignment;
            }
            AddInlines(element, paragraph.Inlines, depth);
            return paragraph;
        }

        private static List CreateList(XElement element, int depth)
        {
            var list = new List();
            ApplyFormatting(element, list);
            list.MarkerStyle = element.Attribute("MarkerStyle")?.Value switch
            {
                "Decimal" => TextMarkerStyle.Decimal,
                "LowerLatin" => TextMarkerStyle.LowerLatin,
                "UpperLatin" => TextMarkerStyle.UpperLatin,
                "LowerRoman" => TextMarkerStyle.LowerRoman,
                "UpperRoman" => TextMarkerStyle.UpperRoman,
                "None" => TextMarkerStyle.None,
                _ => TextMarkerStyle.Disc,
            };

            foreach (XElement child in element.Elements())
            {
                if (child.Name.LocalName != "ListItem")
                {
                    throw new InvalidDataException("Only list items are valid inside a notes list.");
                }
                var item = new ListItem();
                ApplyFormatting(child, item);
                AddBlocks(child, item.Blocks, depth + 1);
                list.ListItems.Add(item);
            }
            return list;
        }

        private static void AddInlines(XElement parent, InlineCollection inlines, int depth)
        {
            foreach (XNode node in parent.Nodes())
            {
                if (node is XText text)
                {
                    if (text.Value.Length > 0)
                    {
                        inlines.Add(new Run(text.Value));
                    }
                    continue;
                }
                if (node is not XElement element)
                {
                    continue;
                }

                Inline inline = element.Name.LocalName switch
                {
                    "Run" => new Run(element.Value),
                    "LineBreak" => new LineBreak(),
                    "Span" => CreateSpan(element, new Span(), depth + 1),
                    "Bold" => CreateSpan(element, new Bold(), depth + 1),
                    "Italic" => CreateSpan(element, new Italic(), depth + 1),
                    "Underline" => CreateSpan(element, new Underline(), depth + 1),
                    // Imported links are deliberately rendered as inert formatted text.
                    "Hyperlink" => CreateSpan(element, new Span(), depth + 1),
                    _ => throw new InvalidDataException(
                        $"Element {element.Name.LocalName} is not valid in notes text."
                    ),
                };
                ApplyFormatting(element, inline);
                inlines.Add(inline);
            }
        }

        private static Span CreateSpan(XElement element, Span span, int depth)
        {
            AddInlines(element, span.Inlines, depth);
            return span;
        }

        private static void ApplyFormatting(XElement element, TextElement target)
        {
            if (
                double.TryParse(
                    element.Attribute("FontSize")?.Value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double fontSize
                )
                && fontSize is >= 1 and <= 200
            )
            {
                target.FontSize = fontSize;
            }

            target.FontWeight = element.Attribute("FontWeight")?.Value switch
            {
                "Thin" => FontWeights.Thin,
                "ExtraLight" => FontWeights.ExtraLight,
                "Light" => FontWeights.Light,
                "Medium" => FontWeights.Medium,
                "SemiBold" => FontWeights.SemiBold,
                "Bold" => FontWeights.Bold,
                "ExtraBold" => FontWeights.ExtraBold,
                "Black" => FontWeights.Black,
                _ => target.FontWeight,
            };
            target.FontStyle = element.Attribute("FontStyle")?.Value switch
            {
                "Italic" => FontStyles.Italic,
                "Oblique" => FontStyles.Oblique,
                _ => target.FontStyle,
            };

            if (TryParseColor(element.Attribute("Foreground")?.Value, out Color foreground))
            {
                target.Foreground = new SolidColorBrush(foreground);
            }
            if (TryParseColor(element.Attribute("Background")?.Value, out Color background))
            {
                target.Background = new SolidColorBrush(background);
            }
            if (target is Inline inline)
            {
                inline.TextDecorations = element.Attribute("TextDecorations")?.Value switch
                {
                    "Underline" => TextDecorations.Underline,
                    "Strikethrough" => TextDecorations.Strikethrough,
                    _ => inline.TextDecorations,
                };
            }
        }

        private static bool TryParseColor(string? value, out Color color)
        {
            color = default;
            if (value == null || value.Length is not (7 or 9) || value[0] != '#')
            {
                return false;
            }
            string hex = value[1..];
            if (
                !uint.TryParse(
                    hex,
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out uint parsed
                )
            )
            {
                return false;
            }
            color = hex.Length == 6
                ? Color.FromRgb((byte)(parsed >> 16), (byte)(parsed >> 8), (byte)parsed)
                : Color.FromArgb(
                    (byte)(parsed >> 24),
                    (byte)(parsed >> 16),
                    (byte)(parsed >> 8),
                    (byte)parsed
                );
            return true;
        }

        private sealed class ElementCounter
        {
            internal int Count { get; set; }
        }
    }
}
