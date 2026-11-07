using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using LevelVault.Models;

namespace LevelVault.Views;

/// <summary>A lightweight native WPF graph viewer. Every node retains its source and rationale in its tooltip.</summary>
public sealed class EvidenceGraphWindow : Window
{
    private static readonly Brush Panel = Brush("#171E28");
    private static readonly Brush Text = Brush("#E8EDF7");
    private static readonly Brush Muted = Brush("#9AA8BC");
    private static readonly Brush Accent = Brush("#8B7CFF");

    public EvidenceGraphWindow(Investigation investigation)
    {
        Title = "levelvault · evidence graph";
        Width = 1120;
        Height = 760;
        MinWidth = 760;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brush("#10151D");

        var root = new DockPanel { Margin = new Thickness(20) };
        var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        heading.Children.Add(new TextBlock
        {
            Text = "evidence graph",
            Foreground = Text,
            FontSize = 24,
            FontWeight = FontWeights.SemiBold
        });
        heading.Children.Add(new TextBlock
        {
            Text = $"{investigation.Query} · {investigation.EvidenceNodes.Count} nodes · {investigation.EvidenceEdges.Count} relationships",
            Foreground = Muted,
            Margin = new Thickness(0, 4, 0, 0)
        });
        DockPanel.SetDock(heading, Dock.Top);
        root.Children.Add(heading);

        var note = new TextBlock
        {
            Text = "lines show extracted relationships, not proof. hover over a node or line for its source and rationale.",
            Foreground = Muted,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 12, 0, 0)
        };
        DockPanel.SetDock(note, Dock.Bottom);
        root.Children.Add(note);

        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = Panel
        };
        var canvas = new Canvas { Background = Panel };
        scroll.Content = canvas;
        root.Children.Add(scroll);
        Content = root;

        RenderGraph(canvas, investigation);
    }

    private static void RenderGraph(Canvas canvas, Investigation investigation)
    {
        var nodes = investigation.EvidenceNodes.ToDictionary(x => x.Id);
        var left = investigation.EvidenceNodes.Where(x => x.Type == "clue").OrderBy(x => x.Label).ToArray();
        var right = investigation.EvidenceNodes.Where(x => x.Type != "clue").OrderBy(x => x.Type).ThenBy(x => x.Label).ToArray();
        const double cardWidth = 330;
        const double cardHeight = 62;
        const double gap = 22;
        const double leftX = 36;
        const double rightX = 720;
        var height = Math.Max(520, Math.Max(left.Length, right.Length) * (cardHeight + gap) + 70);
        canvas.Width = 1100;
        canvas.Height = height;

        var positions = new Dictionary<Guid, Point>();
        for (var i = 0; i < left.Length; i++) positions[left[i].Id] = new Point(leftX, 34 + i * (cardHeight + gap));
        for (var i = 0; i < right.Length; i++) positions[right[i].Id] = new Point(rightX, 34 + i * (cardHeight + gap));

        foreach (var edge in investigation.EvidenceEdges)
        {
            if (!positions.TryGetValue(edge.FromNodeId, out var a) || !positions.TryGetValue(edge.ToNodeId, out var b)) continue;
            var line = new Line
            {
                X1 = a.X + cardWidth,
                Y1 = a.Y + cardHeight / 2,
                X2 = b.X,
                Y2 = b.Y + cardHeight / 2,
                Stroke = edge.Confidence == "corroborated" ? Brush("#55C99A") : edge.Confidence == "strong match" ? Accent : Brush("#64748B"),
                StrokeThickness = edge.Confidence == "unverified" ? 1.2 : 2.4,
                Opacity = 0.75,
                ToolTip = $"{edge.Relationship}\n{edge.Confidence}\n{edge.Rationale}"
            };
            canvas.Children.Add(line);
            var label = new TextBlock
            {
                Text = edge.Relationship,
                Foreground = Muted,
                FontSize = 10,
                Background = Panel,
                ToolTip = edge.Rationale
            };
            Canvas.SetLeft(label, (line.X1 + line.X2) / 2 - 45);
            Canvas.SetTop(label, (line.Y1 + line.Y2) / 2 - 14);
            canvas.Children.Add(label);
        }

        foreach (var node in investigation.EvidenceNodes)
        {
            if (!positions.TryGetValue(node.Id, out var point)) continue;
            var card = new Border
            {
                Width = cardWidth,
                Height = cardHeight,
                CornerRadius = new CornerRadius(9),
                BorderThickness = new Thickness(1),
                BorderBrush = node.Type == "clue" ? Brush("#45546A") : Accent,
                Background = Brush("#202938"),
                Padding = new Thickness(10),
                ToolTip = $"{node.Type}\n{node.Notes}\n{node.SourceUrl}"
            };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = node.Type,
                Foreground = Accent,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold
            });
            stack.Children.Add(new TextBlock
            {
                Text = node.Label,
                Foreground = Text,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            card.Child = stack;
            Canvas.SetLeft(card, point.X);
            Canvas.SetTop(card, point.Y);
            canvas.Children.Add(card);
        }
    }

    private static SolidColorBrush Brush(string color) => new((Color)ColorConverter.ConvertFromString(color));
}
