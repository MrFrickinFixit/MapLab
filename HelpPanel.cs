using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TimingTableCalculator;

public sealed class HelpPanel : Grid
{
    private static readonly SolidColorBrush Accent = new(Color.FromRgb(0, 103, 192));
    private static readonly SolidColorBrush Text = new(Color.FromRgb(32, 32, 32));
    private static readonly SolidColorBrush Muted = new(Color.FromRgb(94, 94, 94));
    private static readonly SolidColorBrush LineBrush = new(Color.FromRgb(209, 209, 209));
    private static readonly SolidColorBrush PanelBrush = new(Color.FromRgb(247, 249, 252));
    private static readonly SolidColorBrush SearchHighlight = new(Color.FromRgb(255, 235, 140));

    private readonly List<HelpTopic> topics;
    private readonly TextBox searchBox = new();
    private readonly TreeView contentsTree = new();
    private readonly ListBox indexList = new();
    private readonly ListBox searchResults = new();
    private readonly TabControl navigationTabs = new();
    private readonly FlowDocumentScrollViewer viewer = new();
    private readonly TextBlock statusText = new();
    private string currentQuery = string.Empty;
    private HelpTopic? currentTopic;

    public HelpPanel()
    {
        var help = LoadHelp();
        topics = help.Topics
            .Where(topic => !string.IsNullOrWhiteSpace(topic.Id) && !string.IsNullOrWhiteSpace(topic.Title))
            .ToList();

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition());
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Children.Add(BuildHeading(help));
        var searchBar = BuildSearchBar();
        Grid.SetRow(searchBar, 1);
        Children.Add(searchBar);

        var workspace = new Grid();
        workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
        workspace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        workspace.ColumnDefinitions.Add(new ColumnDefinition());

        ConfigureNavigation();
        var navigationFrame = Frame(navigationTabs);
        Grid.SetColumn(navigationFrame, 0);
        workspace.Children.Add(navigationFrame);

        viewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        viewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        viewer.IsToolBarVisible = false;
        viewer.Background = Brushes.White;
        var viewerFrame = Frame(viewer);
        Grid.SetColumn(viewerFrame, 2);
        workspace.Children.Add(viewerFrame);
        Grid.SetRow(workspace, 2);
        Children.Add(workspace);

        statusText.Foreground = Muted;
        statusText.FontSize = 11;
        statusText.Margin = new Thickness(4, 7, 0, 0);
        Grid.SetRow(statusText, 3);
        Children.Add(statusText);

        PopulateContents();
        PopulateIndex();
        UpdateStatus();
        if (topics.Count > 0) ShowTopic(topics[0]);
    }

    public void FocusSearch()
    {
        searchBox.Focus();
        searchBox.SelectAll();
    }

    private static HelpFile LoadHelp()
    {
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Help", "MapLabHelp.json");
        try
        {
            var json = File.ReadAllText(path);
            var help = JsonSerializer.Deserialize<HelpFile>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (help is null || help.Topics.Count == 0) throw new InvalidDataException("The help file contains no topics.");
            var duplicate = help.Topics.GroupBy(topic => topic.Id, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
            if (duplicate is not null) throw new InvalidDataException($"Duplicate help topic ID: {duplicate.Key}");
            return help;
        }
        catch (Exception ex)
        {
            return new HelpFile
            {
                Title = "Map Lab Help",
                Version = "Unavailable",
                Topics =
                [
                    new HelpTopic
                    {
                        Id = "help-file-error",
                        Category = "Help",
                        Title = "Help File Could Not Be Loaded",
                        Summary = "Map Lab could not open its bundled help content.",
                        Keywords = ["error", "help file"],
                        Body = [$"Expected file: {path}", $"Details: {ex.Message}", "Repair or reinstall Map Lab to restore the help file."]
                    }
                ]
            };
        }
    }

    private static UIElement BuildHeading(HelpFile help)
    {
        var heading = new Grid { Margin = new Thickness(4, 0, 0, 12) };
        heading.ColumnDefinitions.Add(new ColumnDefinition());
        heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var title = new StackPanel();
        title.Children.Add(new TextBlock { Text = "MAP LAB", Foreground = Accent, FontSize = 12, FontWeight = FontWeights.Bold });
        title.Children.Add(new TextBlock { Text = help.Title, Foreground = Text, FontSize = 26, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 3, 0, 0) });
        title.Children.Add(new TextBlock { Text = "Browse the contents, use the alphabetical index, or search every topic.", Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 4, 0, 0) });
        heading.Children.Add(title);
        var version = new Border { Background = PanelBrush, BorderBrush = LineBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Padding = new Thickness(10, 5, 10, 5), VerticalAlignment = VerticalAlignment.Top, Child = new TextBlock { Text = help.Version, Foreground = Muted, FontSize = 11, FontWeight = FontWeights.SemiBold } };
        Grid.SetColumn(version, 1);
        heading.Children.Add(version);
        return heading;
    }

    private UIElement BuildSearchBar()
    {
        var bar = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.ColumnDefinitions.Add(new ColumnDefinition());
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.Children.Add(new TextBlock { Text = "SEARCH HELP", Foreground = Muted, FontSize = 11, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 10, 0) });
        searchBox.Height = 32;
        searchBox.Padding = new Thickness(8, 4, 8, 4);
        searchBox.ToolTip = "Search topic titles, summaries, keywords, and instructions";
        searchBox.TextChanged += (_, _) => Search(searchBox.Text);
        searchBox.KeyDown += SearchBox_KeyDown;
        Grid.SetColumn(searchBox, 1);
        bar.Children.Add(searchBox);
        var clear = new Button { Content = "Clear", Width = 72, Height = 32, Margin = new Thickness(7, 0, 0, 0), Padding = new Thickness(8, 3, 8, 3) };
        clear.Click += (_, _) => { searchBox.Clear(); searchBox.Focus(); };
        Grid.SetColumn(clear, 2);
        bar.Children.Add(clear);
        return bar;
    }

    private void ConfigureNavigation()
    {
        navigationTabs.Background = Brushes.White;
        navigationTabs.Items.Add(new TabItem { Header = "CONTENTS", Content = contentsTree });
        navigationTabs.Items.Add(new TabItem { Header = "INDEX", Content = indexList });
        navigationTabs.Items.Add(new TabItem { Header = "SEARCH", Content = searchResults });

        contentsTree.BorderThickness = new Thickness(0);
        contentsTree.Background = Brushes.White;
        contentsTree.SelectedItemChanged += (_, _) =>
        {
            if (contentsTree.SelectedItem is TreeViewItem { Tag: HelpTopic topic }) ShowTopic(topic);
        };

        ConfigureList(indexList);
        ConfigureList(searchResults);
        indexList.SelectionChanged += (_, _) =>
        {
            if (indexList.SelectedItem is HelpNavigationItem item) ShowTopic(item.Topic);
        };
        searchResults.SelectionChanged += (_, _) =>
        {
            if (searchResults.SelectedItem is HelpNavigationItem item) ShowTopic(item.Topic);
        };
    }

    private static void ConfigureList(ListBox list)
    {
        list.BorderThickness = new Thickness(0);
        list.Background = Brushes.White;
        list.DisplayMemberPath = nameof(HelpNavigationItem.Display);
        list.Padding = new Thickness(3);
    }

    private void PopulateContents()
    {
        foreach (var category in topics.GroupBy(topic => topic.Category))
        {
            var categoryItem = new TreeViewItem { Header = category.Key, IsExpanded = true, FontWeight = FontWeights.SemiBold, Foreground = Accent };
            foreach (var topic in category)
            {
                categoryItem.Items.Add(new TreeViewItem { Header = topic.Title, Tag = topic, FontWeight = FontWeights.Normal, Foreground = Text, Padding = new Thickness(2) });
            }
            contentsTree.Items.Add(categoryItem);
        }
    }

    private void PopulateIndex()
    {
        var entries = topics
            .SelectMany(topic => topic.Keywords.Append(topic.Title).Select(keyword => new HelpNavigationItem(topic, $"{keyword}  —  {topic.Title}")))
            .DistinctBy(item => $"{item.Topic.Id}\0{item.Display}", StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item.Display, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        indexList.ItemsSource = entries;
    }

    private void Search(string query)
    {
        currentQuery = query.Trim();
        if (currentQuery.Length == 0)
        {
            searchResults.ItemsSource = Array.Empty<HelpNavigationItem>();
            UpdateStatus();
            if (currentTopic is not null) ShowTopic(currentTopic);
            return;
        }

        var terms = currentQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var matches = topics
            .Select(topic => new { Topic = topic, Score = Score(topic, terms) })
            .Where(match => match.Score >= 0)
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Topic.Title, StringComparer.CurrentCultureIgnoreCase)
            .Select(match => new HelpNavigationItem(match.Topic, $"{match.Topic.Title}\n{match.Topic.Summary}"))
            .ToList();
        searchResults.ItemsSource = matches;
        navigationTabs.SelectedIndex = 2;
        statusText.Text = matches.Count == 1 ? "1 matching help topic" : $"{matches.Count} matching help topics";
        if (matches.Count > 0)
        {
            searchResults.SelectedIndex = 0;
            ShowTopic(matches[0].Topic);
        }
        else ShowNoResults();
    }

    private static int Score(HelpTopic topic, IReadOnlyCollection<string> terms)
    {
        var searchable = string.Join('\n', topic.Title, topic.Summary, topic.Category, string.Join(' ', topic.Keywords), string.Join(' ', topic.Body));
        if (terms.Any(term => searchable.IndexOf(term, StringComparison.OrdinalIgnoreCase) < 0)) return -1;
        var score = 0;
        foreach (var term in terms)
        {
            if (topic.Title.Equals(term, StringComparison.OrdinalIgnoreCase)) score += 200;
            else if (topic.Title.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 100;
            if (topic.Keywords.Any(keyword => keyword.Equals(term, StringComparison.OrdinalIgnoreCase))) score += 80;
            else if (topic.Keywords.Any(keyword => keyword.Contains(term, StringComparison.OrdinalIgnoreCase))) score += 45;
            if (topic.Summary.Contains(term, StringComparison.OrdinalIgnoreCase)) score += 25;
            if (topic.Body.Any(line => line.Contains(term, StringComparison.OrdinalIgnoreCase))) score += 10;
        }
        return score;
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            searchBox.Clear();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && searchResults.Items.Count > 0)
        {
            searchResults.SelectedIndex = 0;
            searchResults.Focus();
            e.Handled = true;
        }
    }

    private void ShowTopic(HelpTopic topic)
    {
        currentTopic = topic;
        var document = NewDocument();
        document.Blocks.Add(new Paragraph(new Run(topic.Category.ToUpperInvariant())) { Foreground = Muted, FontSize = 10, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
        var heading = new Paragraph { FontSize = 24, FontWeight = FontWeights.SemiBold, Foreground = Accent, Margin = new Thickness(0, 0, 0, 7) };
        AppendHighlighted(heading, topic.Title, currentQuery);
        document.Blocks.Add(heading);
        var summary = new Paragraph { FontSize = 14, Foreground = Muted, Margin = new Thickness(0, 0, 0, 18) };
        AppendHighlighted(summary, topic.Summary, currentQuery);
        document.Blocks.Add(summary);

        var list = new System.Windows.Documents.List { MarkerStyle = TextMarkerStyle.Disc, Margin = new Thickness(24, 0, 4, 12) };
        foreach (var line in topic.Body)
        {
            var paragraph = new Paragraph { Margin = new Thickness(0, 2, 0, 7), LineHeight = 20 };
            AppendHighlighted(paragraph, line, currentQuery);
            list.ListItems.Add(new ListItem(paragraph));
        }
        document.Blocks.Add(list);

        if (TopicIllustration(topic.Id) is { } illustration) document.Blocks.Add(illustration);

        var keywords = new Paragraph { Margin = new Thickness(0, 20, 0, 0), FontSize = 11, Foreground = Muted };
        keywords.Inlines.Add(new Run("INDEX TERMS  ") { FontWeight = FontWeights.Bold, Foreground = Accent });
        AppendHighlighted(keywords, string.Join("  •  ", topic.Keywords), currentQuery);
        document.Blocks.Add(keywords);
        if (topic.Id == "calibration-safety") document.Blocks.Add(SafetyNotice());
        viewer.Document = document;
    }

    private void ShowNoResults()
    {
        var document = NewDocument();
        document.Blocks.Add(new Paragraph(new Run("No Help Topics Found")) { FontSize = 24, FontWeight = FontWeights.SemiBold, Foreground = Accent, Margin = new Thickness(0, 0, 0, 8) });
        document.Blocks.Add(new Paragraph(new Run($"No indexed topic contains every term in “{currentQuery}”. Try fewer words or use the Index tab.")) { Foreground = Muted });
        viewer.Document = document;
    }

    private static FlowDocument NewDocument() => new()
    {
        FontFamily = new FontFamily("Segoe UI"),
        FontSize = 13,
        Foreground = Text,
        Background = Brushes.White,
        PagePadding = new Thickness(26),
        ColumnWidth = double.PositiveInfinity
    };

    private static void AppendHighlighted(Paragraph paragraph, string value, string query)
    {
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(term => term.Length)
            .ToArray();
        if (terms.Length == 0)
        {
            paragraph.Inlines.Add(new Run(value));
            return;
        }

        var position = 0;
        while (position < value.Length)
        {
            var nextIndex = -1;
            var nextLength = 0;
            foreach (var term in terms)
            {
                var index = value.IndexOf(term, position, StringComparison.OrdinalIgnoreCase);
                if (index >= 0 && (nextIndex < 0 || index < nextIndex || index == nextIndex && term.Length > nextLength))
                {
                    nextIndex = index;
                    nextLength = term.Length;
                }
            }
            if (nextIndex < 0)
            {
                paragraph.Inlines.Add(new Run(value[position..]));
                break;
            }
            if (nextIndex > position) paragraph.Inlines.Add(new Run(value[position..nextIndex]));
            paragraph.Inlines.Add(new Run(value.Substring(nextIndex, nextLength)) { Background = SearchHighlight, FontWeight = FontWeights.SemiBold });
            position = nextIndex + nextLength;
        }
    }

    private void UpdateStatus()
    {
        var termCount = topics.SelectMany(topic => topic.Keywords).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        statusText.Text = $"{topics.Count} topics  •  {termCount} indexed terms  •  Help file: Help\\MapLabHelp.json";
    }

    private static Border Frame(UIElement child) => new()
    {
        Background = Brushes.White,
        BorderBrush = LineBrush,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(7),
        ClipToBounds = true,
        Child = child
    };

    private static BlockUIContainer SafetyNotice() => new(new Border
    {
        Background = new SolidColorBrush(Color.FromRgb(255, 248, 225)),
        BorderBrush = new SolidColorBrush(Color.FromRgb(226, 190, 92)),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(5),
        Padding = new Thickness(14),
        Margin = new Thickness(0, 18, 0, 4),
        Child = new TextBlock { Text = "Calibration safety: verify exported values independently and use appropriate engine safeguards before applying a map to a vehicle.", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(82, 62, 14)), FontWeight = FontWeights.SemiBold }
    });

    private static BlockUIContainer? TopicIllustration(string id) => id switch
    {
        "cell-selection" => Illustration("Drag selects one area. Hold Ctrl while clicking or dragging to add separated areas.", SelectionDiagram()),
        "axes" => Illustration("Y-axis values run vertically; X-axis values run left to right along the bottom.", AxisDiagram()),
        "basic-smoothing" => Illustration("Row and column smoothing use the outer selected values as anchors.", SmoothingDiagram()),
        "timing-regions" => Illustration("The intersecting boundary cell divides the table into four operating regions.", BoundaryDiagram()),
        "three-d-view" => Illustration("Right-drag to orbit, use the wheel to zoom, and left-drag to select surface cells.", ThreeDDiagram()),
        _ => null
    };

    private static BlockUIContainer Illustration(string caption, UIElement visual)
    {
        var stack = new StackPanel();
        stack.Children.Add(new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, HorizontalAlignment = HorizontalAlignment.Left, Child = visual });
        stack.Children.Add(new TextBlock { Text = caption, Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 9, 2, 0) });
        return new BlockUIContainer(new Border { Background = PanelBrush, BorderBrush = new SolidColorBrush(Color.FromRgb(205, 214, 225)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(14), Margin = new Thickness(0, 6, 0, 16), Child = stack });
    }

    private static Canvas SelectionDiagram()
    {
        var canvas = DiagramCanvas(720, 170);
        DrawGrid(canvas, 28, 25, 9, 5, 48, 23, (row, col) => row is >= 1 and <= 3 && col is >= 1 and <= 4 ? Color.FromRgb(0, 103, 192) : Color.FromRgb(225, 231, 239));
        DrawGrid(canvas, 410, 25, 6, 5, 42, 23, (row, col) => row is >= 1 and <= 2 && col is >= 1 and <= 2 || row == 3 && col is >= 4 and <= 5 ? Color.FromRgb(54, 199, 173) : Color.FromRgb(225, 231, 239));
        AddLabel(canvas, "DRAG SELECTION", 28, 145, Accent, true); AddLabel(canvas, "+ CTRL: ADD AREAS", 410, 145, Accent, true);
        AddArrow(canvas, 72, 60, 220, 112, Colors.White); AddLabel(canvas, "drag", 137, 76, Brushes.White, true);
        return canvas;
    }

    private static Canvas AxisDiagram()
    {
        var canvas = DiagramCanvas(720, 170); DrawGrid(canvas, 120, 20, 8, 5, 55, 21, (_, _) => Color.FromRgb(225, 231, 239));
        AddArrow(canvas, 103, 126, 103, 26, Accent.Color); AddLabel(canvas, "Y AXIS", 30, 64, Accent, true);
        AddArrow(canvas, 120, 143, 555, 143, Color.FromRgb(54, 199, 173)); AddLabel(canvas, "X AXIS", 315, 147, new SolidColorBrush(Color.FromRgb(30, 130, 111)), true);
        AddLabel(canvas, "HIGH", 62, 17, Muted, true); AddLabel(canvas, "LOW", 68, 115, Muted, true); AddLabel(canvas, "LOW", 114, 147, Muted, true); AddLabel(canvas, "HIGH", 547, 147, Muted, true);
        return canvas;
    }

    private static Canvas SmoothingDiagram()
    {
        var canvas = DiagramCanvas(720, 190);
        DrawGrid(canvas, 28, 25, 8, 5, 36, 22, (_, col) => col is 0 or 7 ? Color.FromRgb(0, 103, 192) : Color.FromRgb((byte)(100 + col * 14), 205, 190));
        AddLabel(canvas, "SMOOTH ROWS", 28, 146, Accent, true); AddLabel(canvas, "anchors", 28, 163, Muted, false); AddLabel(canvas, "anchors", 252, 163, Muted, false);
        DrawGrid(canvas, 405, 25, 7, 5, 36, 22, (row, _) => row is 0 or 4 ? Color.FromRgb(0, 103, 192) : Color.FromRgb(110, (byte)(190 - row * 12), 220));
        AddLabel(canvas, "SMOOTH COLUMNS", 405, 146, Accent, true); AddLabel(canvas, "top anchor", 405, 163, Muted, false); AddLabel(canvas, "bottom anchor", 565, 163, Muted, false);
        return canvas;
    }

    private static Canvas BoundaryDiagram()
    {
        var canvas = DiagramCanvas(720, 220); var left = 95d; var top = 18d; var width = 520d; var height = 160d; var splitX = left + 190; var splitY = top + 72;
        AddRect(canvas, left, top, splitX - left, splitY - top, Color.FromRgb(255, 224, 146)); AddRect(canvas, splitX, top, left + width - splitX, splitY - top, Color.FromRgb(240, 158, 186));
        AddRect(canvas, left, splitY, splitX - left, top + height - splitY, Color.FromRgb(174, 220, 183)); AddRect(canvas, splitX, splitY, left + width - splitX, top + height - splitY, Color.FromRgb(139, 205, 232));
        canvas.Children.Add(new Line { X1 = splitX, X2 = splitX, Y1 = top, Y2 = top + height, Stroke = Brushes.Black, StrokeThickness = 4 }); canvas.Children.Add(new Line { X1 = left, X2 = left + width, Y1 = splitY, Y2 = splitY, Stroke = Brushes.Black, StrokeThickness = 4 });
        AddLabel(canvas, "IDLE HIGH MAP", 126, 43, Text, true); AddLabel(canvas, "PART THROTTLE → WOT", 367, 43, Text, true); AddLabel(canvas, "IDLE LOW MAP", 130, 126, Text, true); AddLabel(canvas, "CRUISE → PART THROTTLE", 360, 126, Text, true);
        return canvas;
    }

    private static Canvas ThreeDDiagram()
    {
        var canvas = DiagramCanvas(720, 225);
        var surface = new Polygon { Points = new PointCollection { new(175, 42), new(520, 28), new(620, 139), new(105, 171) }, Fill = new LinearGradientBrush(Color.FromRgb(255, 75, 45), Color.FromRgb(113, 40, 220), 0), Stroke = Brushes.Black, StrokeThickness = 1.5 };
        canvas.Children.Add(surface);
        for (var i = 1; i < 8; i++)
        {
            var x1 = 105 + (175 - 105) * i / 8d; var y1 = 171 + (42 - 171) * i / 8d; var x2 = 620 + (520 - 620) * i / 8d; var y2 = 139 + (28 - 139) * i / 8d;
            canvas.Children.Add(new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), StrokeThickness = 1 });
        }
        AddArrow(canvas, 90, 67, 137, 31, Accent.Color); AddArrow(canvas, 137, 31, 190, 58, Accent.Color); AddLabel(canvas, "RIGHT-DRAG TO ORBIT", 25, 19, Accent, true);
        AddLabel(canvas, "WHEEL TO ZOOM", 528, 17, Muted, true); AddLabel(canvas, "SELECT SURFACE CELLS", 252, 190, new SolidColorBrush(Color.FromRgb(30, 130, 111)), true);
        return canvas;
    }

    private static Canvas DiagramCanvas(double width, double height) => new() { Width = width, Height = height, Background = Brushes.Transparent };
    private static void DrawGrid(Canvas canvas, double left, double top, int columns, int rows, double cellWidth, double cellHeight, Func<int, int, Color> color)
    {
        for (var row = 0; row < rows; row++) for (var col = 0; col < columns; col++) AddRect(canvas, left + col * cellWidth, top + row * cellHeight, cellWidth - 2, cellHeight - 2, color(row, col));
    }
    private static void AddRect(Canvas canvas, double left, double top, double width, double height, Color color) { var rectangle = new Rectangle { Width = width, Height = height, Fill = new SolidColorBrush(color), Stroke = new SolidColorBrush(Color.FromRgb(115, 130, 148)), StrokeThickness = 1 }; Canvas.SetLeft(rectangle, left); Canvas.SetTop(rectangle, top); canvas.Children.Add(rectangle); }
    private static void AddLabel(Canvas canvas, string value, double left, double top, Brush color, bool bold) { var label = new TextBlock { Text = value, Foreground = color, FontSize = 11, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal }; Canvas.SetLeft(label, left); Canvas.SetTop(label, top); canvas.Children.Add(label); }
    private static void AddArrow(Canvas canvas, double x1, double y1, double x2, double y2, Color color)
    {
        var brush = new SolidColorBrush(color); canvas.Children.Add(new Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2, Stroke = brush, StrokeThickness = 3 }); var angle = Math.Atan2(y2 - y1, x2 - x1); var size = 9d;
        canvas.Children.Add(new Polygon { Fill = brush, Points = new PointCollection { new(x2, y2), new(x2 - size * Math.Cos(angle - .55), y2 - size * Math.Sin(angle - .55)), new(x2 - size * Math.Cos(angle + .55), y2 - size * Math.Sin(angle + .55)) } });
    }

    private sealed class HelpFile
    {
        public string Title { get; set; } = "Map Lab Help";
        public string Version { get; set; } = string.Empty;
        public List<HelpTopic> Topics { get; set; } = [];
    }

    private sealed class HelpTopic
    {
        public string Id { get; set; } = string.Empty;
        public string Category { get; set; } = "Help";
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public List<string> Keywords { get; set; } = [];
        public List<string> Body { get; set; } = [];
    }

    private sealed record HelpNavigationItem(HelpTopic Topic, string Display);
}
