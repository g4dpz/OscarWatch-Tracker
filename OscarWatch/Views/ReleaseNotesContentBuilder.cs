using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using OscarWatch.Core.Display;

namespace OscarWatch.Views;

internal static class ReleaseNotesContentBuilder
{
    private static readonly FontFamily MonospaceFont = new("Cascadia Mono, Consolas, Courier New, monospace");

    public static void Populate(StackPanel panel, ReleaseNotesDocument document)
    {
        panel.Children.Clear();
        foreach (var block in document.Blocks)
            panel.Children.Add(CreateBlockControl(block));
    }

    private static Control CreateBlockControl(ReleaseNoteBlock block) =>
        block.Kind switch
        {
            ReleaseNoteBlockKind.Heading2 => CreateHeading(block, 15, new Thickness(0, 12, 0, 4)),
            ReleaseNoteBlockKind.Heading3 => CreateHeading(block, 14, new Thickness(0, 10, 0, 4)),
            ReleaseNoteBlockKind.CodeBlock => CreateCodeBlock(block),
            ReleaseNoteBlockKind.ListItem => CreateListItem(block, "•"),
            ReleaseNoteBlockKind.OrderedListItem => CreateListItem(block, "–"),
            _ => CreateParagraph(block)
        };

    private static Control CreateHeading(ReleaseNoteBlock block, double fontSize, Thickness margin) =>
        new TextBlock
        {
            Text = GetPlainText(block),
            FontSize = fontSize,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap,
            Margin = margin
        };

    private static Control CreateParagraph(ReleaseNoteBlock block)
    {
        var text = CreateInlineTextBlock(block.Spans);
        text.Margin = new Thickness(0, 0, 0, 8);
        return text;
    }

    private static Control CreateListItem(ReleaseNoteBlock block, string marker)
    {
        var grid = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)],
            Margin = new Thickness(0, 0, 0, 6)
        };

        grid.Children.Add(new TextBlock
        {
            Text = marker,
            FontSize = 13,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Top
        });

        var content = CreateInlineTextBlock(block.Spans);
        Grid.SetColumn(content, 1);
        grid.Children.Add(content);
        return grid;
    }

    private static Control CreateCodeBlock(ReleaseNoteBlock block)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1AFFFFFF")),
            BorderBrush = new SolidColorBrush(Color.Parse("#33FFFFFF")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 8),
            Margin = new Thickness(0, 0, 0, 8)
        };

        border.Child = new TextBlock
        {
            Text = GetPlainText(block),
            FontFamily = MonospaceFont,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 18
        };
        return border;
    }

    private static TextBlock CreateInlineTextBlock(IReadOnlyList<ReleaseNoteSpan> spans)
    {
        var text = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            LineHeight = 20
        };

        foreach (var span in spans)
        {
            text.Inlines!.Add(CreateInline(span));
        }

        return text;
    }

    private static Inline CreateInline(ReleaseNoteSpan span) =>
        span.Kind switch
        {
            ReleaseNoteSpanKind.Bold => new Run
            {
                Text = span.Text,
                FontWeight = FontWeight.SemiBold
            },
            ReleaseNoteSpanKind.Italic => new Run
            {
                Text = span.Text,
                FontStyle = FontStyle.Italic
            },
            ReleaseNoteSpanKind.Code => new Run
            {
                Text = span.Text,
                FontFamily = MonospaceFont,
                Background = new SolidColorBrush(Color.Parse("#22FFFFFF"))
            },
            ReleaseNoteSpanKind.Link => CreateLinkInline(span),
            _ => new Run { Text = span.Text }
        };

    private static Inline CreateLinkInline(ReleaseNoteSpan span) =>
        new Run
        {
            Text = span.Text,
            TextDecorations = TextDecorations.Underline,
            Foreground = new SolidColorBrush(Color.Parse("#FF6CB6FF"))
        };

    private static string GetPlainText(ReleaseNoteBlock block) =>
        string.Concat(block.Spans.Select(s => s.Text));
}
