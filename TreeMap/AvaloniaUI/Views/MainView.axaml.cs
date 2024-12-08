using Avalonia.Controls;
using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;
using Avalonia.Interactivity;
using TreeMap;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using System.Diagnostics;
using Avalonia;
using Avalonia.Input;
using AvaloniaUI.ViewModels;
using Serilog;
using Rect = TreeMap.Rect;

namespace AvaloniaUI.Views;

public partial class MainView : UserControl
{
    public List<FileInfo> Files { get; set; }
    public MainView()
    {
        InitializeComponent();
        Files = new DirectoryInfo("C:\\Users\\thboo\\OneDrive").GetFiles("*", SearchOption.AllDirectories).Where(i => i.Length > 100000).ToList();
        ShowContainersCheckbox.IsCheckedChanged += ShowContainersCheckEvent;
        RenderDropDown.SelectionChanged += ColoringChanged;
        Canvas.PointerMoved += OnPointerMoved;
        
        RenderCanvas();
    }

    private bool _showContainers;
    private void ShowContainersCheckEvent(object? sender, RoutedEventArgs e)
    {
        _showContainers = ShowContainersCheckbox.IsChecked ?? false;
        RenderCanvas();
    }

    private void ColoringChanged(object sender, SelectionChangedEventArgs e)
    {
        string colorBy = RenderDropDown.SelectionBoxItem.ToString();
        if (colorBy == null) colorBy = "Extension";
        if (colorBy == "Extension")
        {
            colorer = new ExtensionColoring();
        }
        else if (colorBy == "File Age")
        {
            colorer = new AgeColoring();
        }
        else if (colorBy == "Extension & Age")
        {
            colorer = new ExtensionAndAgeColoring();
        }
        else
        {
            throw new ArgumentException("Cannot find colorer: [" + colorBy + "]");
        }
        RenderCanvas();
    }

    private ITreeMapInput<FileSystemNode>[] GetTreeMapInputs()
    {
        Folder root = new Folder("", "<root>");
        foreach (var file in Files)
        {
            string[] parts = file.FullName.Split(System.IO.Path.DirectorySeparatorChar);
            Folder current = root;
            string name = "";
            for (int i = 0; i < parts.Length - 1; i++)
            {
                string dirName = parts[i];
                if (!current.Children.TryGetValue(dirName, out Folder? next))
                {
                    next = new Folder(current.FullName, parts[i]);
                    current.Children[dirName] = next;
                }
                current = next;
            }

            current.Files.Add(file);
        }

        Folder baseDir = root;
        while (baseDir.Files.Count == 0 && baseDir.Children.Count == 1)
        {
            baseDir = baseDir.Children.First().Value;
        }

        return baseDir.GetTreeMapInput().Children;

    }

    private Dictionary<string, BrushBase> FlavorToBrush = new();
    private IColorer<FileInfo> colorer = new ExtensionColoring();

    private void RenderCanvas()
    {
        if (Canvas == null || Canvas.Bounds.Width <= 0 || Canvas.Bounds.Height <= 0) return;
        Stopwatch sw = Stopwatch.StartNew();
        Canvas.Children.Clear();
        Log.Information("Cleared children {Elapsed}", sw);
        FlavorToBrush = new();
        nextColorIndex = 0;

        Stopwatch sw2 = Stopwatch.StartNew();
        TreeMapPlacer placer = new TreeMapPlacer();
        placer.RenderContainers = _showContainers;
        ITreeMapInput<FileSystemNode>[] input = GetTreeMapInputs();
        TreeMapBox<FileSystemNode>[] placements = placer.GetPlacements(input, Canvas.Bounds.Width, Canvas.Bounds.Height).ToArray();
        Log.Information("Buildings placements took {Elapsed}", sw2);
        colorer.Initialize(placements.Select(p => p.Item.FileInfo).Where(f => f != null).Select(f => f!));
        foreach (var placement in placements)
        {
            if (placement.IsContainer)
            {
                RenderContainerPlacement(placement);
            }
            else
            {
                RenderLeafPlacement(placement);
            }
        }

        Log.Information("RenderCanvas took {Elapsed}", sw);
    }

    private void RenderContainerPlacement(TreeMapBox<FileSystemNode> placement)
    {
        Rect r = placement.Rectangle;
        var textBlock = new TextBlock
        {
            Text = placement.Item.FullName,
            Height = placement.ContainerHeaderHeightPixels,
            FontSize = placement.ContainerHeaderHeightPixels - 2,
            Foreground = new SolidColorBrush(Colors.Black),
        };
        var header = new Border
        {
            Background = new SolidColorBrush(Colors.LightGray),
            Child = textBlock
        };
        Canvas.Children.Add(header);
        header.SetValue(Canvas.TopProperty, r.Y + placement.BorderThicknessPixels);
        header.SetValue(Canvas.LeftProperty, r.X + placement.BorderThicknessPixels);
        textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
        if (r.Width < 6 * placement.Item.FullName.Length)
        {
            textBlock.Text = "..." + textBlock.Text.Substring((int)(textBlock.Text.Length - r.Width / 6));

        }

        var border = new Polyline
        {
            Points =
            [
                new(0, 0),
                new(0, r.Height),
                new(r.Width, r.Height),
                new(r.Width, 0),
                new(0, 0)
            ],
            Stroke = new SolidColorBrush(Colors.Black)
        };
        Canvas.Children.Add(border);
        border.SetValue(Canvas.TopProperty, r.Y);
        border.SetValue(Canvas.LeftProperty, r.X);
    }

    private void RenderLeafPlacement(TreeMapBox<FileSystemNode> placement)
    {
        Brush brush;
        if (placement.Item.FileInfo == null)
        {
            brush = DefaultBrush;
        }
        else
        {
            string flavor = colorer.GetFlavor(placement.Item.FileInfo!);

            if (!FlavorToBrush.TryGetValue(flavor, out BrushBase? brushBase))
            {
                brushBase = GenerateNextBrush();
                FlavorToBrush[flavor] = brushBase;
            }

            brush = brushBase.GetBrushByStrength(colorer.GetColorStrength(placement.Item.FileInfo));
        }

        var rect = new Rectangle
        {
            Fill = brush,
            Height = placement.Rectangle.Height,
            Width = placement.Rectangle.Width,
        };
        Canvas.Children.Add(rect);
        rect.SetValue(Canvas.TopProperty, placement.Rectangle.Y);
        rect.SetValue(Canvas.LeftProperty, placement.Rectangle.X);
        rect.DataContext = placement;
        //ToolTip t = new ToolTip();
        //t.Content = placement.Item.FullName;
        //ToolTipService.SetToolTip(rect, t);
        
        rect.PointerEntered += (object sender, PointerEventArgs e) =>
        {
            string text = placement.Item.FullName + " - " + placement.Item.Size.ToString("N0");
            //var model = (MainViewModel)Canvas.DataContext;
            //Task.Run(() => { model.HoveredItem = placement.Item.FullName + " - " + placement.Item.Size.ToString("N0"); });
            //HoverText.Text = 
            HoverArea.Children.Clear();
            HoverArea.Children.Add(new TextBlock{Text = text });
            rect.Fill = new SolidColorBrush(Colors.Azure);
        };
        rect.PointerExited += (object sender, PointerEventArgs e) =>
        {
            rect.Fill = brush;
        };
    }

    private Rectangle? hoveredRectangle;
    private IBrush? hoveredBrush;
    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        /*
        Point p = e.GetPosition(Canvas);
        Rectangle found = null;
        foreach (Rectangle r in Canvas.Children.OfType<Rectangle>())
        {
            if (r.Bounds.Left < p.X && r.Bounds.Right > p.X && r.Bounds.Top < p.Y && r.Bounds.Bottom > p.Y)
            {
                found = r;
                break;
            }
        }

        if (found == hoveredRectangle) return;

        if (found == null)
        {
            if (hoveredRectangle == null) return;

            // set the old one back to normal
            hoveredRectangle.Fill = hoveredBrush;
            hoveredRectangle = null;
            hoveredBrush = null;
            return;
        }

        if (hoveredRectangle != null)
        {
            // set the old one back to normal
            hoveredRectangle.Fill = hoveredBrush;
        }

        hoveredBrush = found.Fill;
        found.Fill = new SolidColorBrush(Colors.Azure);
        hoveredRectangle = found;
        string str = ((TreeMapBox<FileSystemNode>)found.DataContext).Item.FullName;
        Console.WriteLine(str);
        //HoverText.Text = 
        */
    }

    private Color[] colors =
    [
        Colors.DarkBlue, Colors.DarkRed, Colors.DarkGreen,Colors.Purple, Colors.DeepPink,
        Colors.Magenta, Colors.Brown, Colors.Coral, Colors.SlateBlue, Colors.Salmon,
        Colors.Orange, Colors.Green, Colors.SteelBlue, Colors.Red, Colors.SaddleBrown
    ];
    private int nextColorIndex = 0;
    private BrushBase GenerateNextBrush()
    {
        Color color = Colors.Gray;
        if (nextColorIndex < colors.Length)
        {
            color = colors[nextColorIndex];
            nextColorIndex++;
        }
        return new BrushBase(color);
    }

    private static Color FadeColor(Color baseColor, double strength)
    {
        double fadeBy = 1 - strength;
        return new Color(
            a: baseColor.A,
            b: (byte)(baseColor.B + (byte)(fadeBy * (256 - baseColor.B))),
            g: (byte)(baseColor.G + (byte)(fadeBy * (256 - baseColor.G))),
            r: (byte)(baseColor.R + (byte)(fadeBy * (256 - baseColor.R))));

    }

    private static Brush DefaultBrush = CreateBrush(Colors.Black);
    private static Brush CreateBrush(Color color)
    {
        var lightColor = FadeColor(color, 0.4);
        return new RadialGradientBrush
        {
            Center =RelativePoint.BottomRight,
            RadiusX = RelativeScalar.End,
            RadiusY = RelativeScalar.End,
            GradientStops =
            {
                new GradientStop
                {
                    Color = lightColor,
                    Offset = 0
                },
                new GradientStop
                {
                    Color = color,
                    Offset = 1
                }
            }
        };
    }

    private class BrushBase
    {
        private readonly Color BaseColor;
        private Brush?[] fadingBrushes;

        public BrushBase(Color baseColor)
        {
            BaseColor = baseColor;
            fadingBrushes = new Brush?[101];
        }

        public Brush GetBrushByStrength(double strength)
        {
            int strengthInt = (int)(strength * 100);
            if (strength < 0)
            {
                strengthInt = 0;
                strength = 1;
            }
            if (fadingBrushes[strengthInt] == null)
            {
                fadingBrushes[strengthInt] = CreateBrush(FadeColor(BaseColor, strength));
            }

            return fadingBrushes[strengthInt]!;
        }
    }
}



public class Folder(string parentName, string name)
{
    public readonly string ParentName = parentName;
    public readonly string Name = name;
    public readonly string FullName = System.IO.Path.Combine(parentName, name);
    public readonly Dictionary<string, Folder> Children = new();
    public readonly List<FileInfo> Files = new();
    private long? _size;

    public long Size
    {
        get
        {
            if (_size == null)
            {
                long s = Files.Sum(f => f.Length);
                s += Children.Values.Sum(d => d.Size);
                _size = s;
            }

            return _size.Value;
        }
    }

    public TreeMapInput<FileSystemNode> GetTreeMapInput()
    {
        List<ITreeMapInput<FileSystemNode>> children = new();
        if (Files.Count > 0)
        {
            long filesSize = Files.Sum(f => f.Length);
            List<ITreeMapInput<FileSystemNode>> fileChildren = new();
            foreach (FileInfo file in Files)
            {
                fileChildren.Add(new TreeMapInput<FileSystemNode>(file.Length, new FileSystemNode(FullName, file.Name, file.Length, file), []));
            }

            children.Add(new TreeMapInput<FileSystemNode>(filesSize, new FileSystemNode(FullName, "<files>", filesSize, null), fileChildren.ToArray()));
        }

        foreach (var child in Children)
        {
            children.Add(child.Value.GetTreeMapInput());
        }

        return new(Size, new FileSystemNode(ParentName, Name, Size, null), children.ToArray());
    }
}

public class FileSystemNode(string folderName, string name, long size, FileInfo? fileInfo)
{
    public string FolderName { get; } = folderName;
    public string Name { get; } = name;
    public readonly string FullName = System.IO.Path.Combine(folderName, name);
    public long Size { get; } = size;
    public FileInfo? FileInfo { get; } = fileInfo;

    public override string ToString() => FileInfo?.FullName ?? System.IO.Path.Combine(FolderName, Name);
}

public interface IColorer<T>
{
    string GetFlavor(T item);
    double GetColorStrength(T item);

    void Initialize(IEnumerable<T> allItems)
    {
    }
}

public class ExtensionColoring : IColorer<FileInfo>
{
    public string GetFlavor(FileInfo file)
    {
        return file.Extension.ToLower();
    }

    public double GetColorStrength(FileInfo file)
    {
        return 1;
    }
}

public class AgeColoring : IColorer<FileInfo>
{
    public virtual string GetFlavor(FileInfo file)
    {
        return "";
    }

    public double GetColorStrength(FileInfo file)
    {
        TimeSpan fromStart = GetDate(file).Subtract(minAge);
        return fromStart.Ticks / tickSpan;
    }

    private DateTime minAge = DateTime.MaxValue, maxAge = DateTime.MinValue;
    private double tickSpan;
    public void Initialize(IEnumerable<FileInfo> allItems)
    {
        foreach (FileInfo item in allItems)
        {
            DateTime date = GetDate(item);
            if (minAge > date) minAge = date;
            if (maxAge < date) maxAge = date;
        }
        tickSpan = (maxAge - minAge).Ticks;
    }

    private static DateTime GetDate(FileInfo file)
    {
        if (file.CreationTime.Year < 1990) return file.LastWriteTime;
        if (file.LastWriteTime.Year < 1990) return file.CreationTime;
        if (file.LastWriteTime < file.CreationTime) return file.LastWriteTime;
        return file.CreationTime;
    }
}

public class ExtensionAndAgeColoring : AgeColoring
{
    public override string GetFlavor(FileInfo file)
    {
        return file.Extension.ToLower();
    }
}