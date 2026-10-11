using System.IO;
using System.Text;
using System.Windows.Documents;
using System.Xml;
using Guard.WPF.Core;

namespace Guard.Test.Core
{
    public class NotesDocumentCodecTests
    {
        private const string PresentationNamespace =
            "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

        [Fact]
        public void LoadsTextAndSupportedFormatting()
        {
            FlowDocument document = Load(
                $"""
                <Section xmlns="{PresentationNamespace}">
                  <Paragraph><Bold>recovery</Bold><LineBreak/><Run Foreground="#FF102030">code</Run></Paragraph>
                </Section>
                """
            );

            Assert.Contains("recovery", GetText(document));
            Assert.Contains("code", GetText(document));
            Assert.IsType<Bold>(
                ((Paragraph)((Section)document.Blocks.FirstBlock).Blocks.FirstBlock)
                    .Inlines.FirstInline
            );
        }

        [Fact]
        public void LoadsLegacyPlainTextAsLiteralText()
        {
            FlowDocument document = Load("backup note <not markup");

            Assert.Contains("backup note <not markup", GetText(document));
        }

        [Theory]
        [InlineData("<ObjectDataProvider ObjectType=\"{x:Type Process}\" MethodName=\"Start\" />")]
        [InlineData("<Paragraph x:Class=\"Exploit\">text</Paragraph>")]
        [InlineData("<Paragraph><Button Click=\"Run\">text</Button></Paragraph>")]
        public void RejectsExecutableXaml(string payload)
        {
            string xaml =
                $"<Section xmlns=\"{PresentationNamespace}\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">{payload}</Section>";

            Assert.Throws<InvalidDataException>(() => Load(xaml));
        }

        [Fact]
        public void RejectsDocumentTypeDefinitions()
        {
            string xaml =
                $"<!DOCTYPE Section [<!ENTITY value SYSTEM \"file:///windows/win.ini\">]><Section xmlns=\"{PresentationNamespace}\"><Paragraph>&value;</Paragraph></Section>";

            Assert.Throws<XmlException>(() => Load(xaml));
        }

        [Fact]
        public void RendersHyperlinksAsInertText()
        {
            FlowDocument document = Load(
                $"<Section xmlns=\"{PresentationNamespace}\"><Paragraph><Hyperlink NavigateUri=\"file:///windows/system32/calc.exe\">label</Hyperlink></Paragraph></Section>"
            );
            Paragraph paragraph = (Paragraph)((Section)document.Blocks.FirstBlock).Blocks.FirstBlock;

            Assert.IsType<Span>(paragraph.Inlines.FirstInline);
            Assert.IsNotType<Hyperlink>(paragraph.Inlines.FirstInline);
            Assert.Contains("label", GetText(document));
        }

        [Fact]
        public void RejectsExcessiveNesting()
        {
            string xaml =
                $"<Section xmlns=\"{PresentationNamespace}\"><Paragraph>"
                + string.Concat(Enumerable.Repeat("<Span>", 40))
                + "text"
                + string.Concat(Enumerable.Repeat("</Span>", 40))
                + "</Paragraph></Section>";

            Assert.Throws<InvalidDataException>(() => Load(xaml));
        }

        [Fact]
        public void AcceptsUtf8ByteOrderMarkFromLegacyStorage()
        {
            byte[] xaml = Encoding.UTF8.GetPreamble()
                .Concat(
                    Encoding.UTF8.GetBytes(
                        $"<Section xmlns=\"{PresentationNamespace}\"><Paragraph>note</Paragraph></Section>"
                    )
                )
                .ToArray();

            Assert.Contains("note", GetText(NotesDocumentCodec.Load(xaml)));
        }

        private static FlowDocument Load(string value) =>
            NotesDocumentCodec.Load(Encoding.UTF8.GetBytes(value));

        private static string GetText(FlowDocument document) =>
            new TextRange(document.ContentStart, document.ContentEnd).Text;
    }
}
