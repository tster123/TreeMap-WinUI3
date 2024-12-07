using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Foundation;
using Microsoft.UI;
using Microsoft.UI.Xaml.Shapes;
using TreeMap;
using Color = Windows.UI.Color;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ExampleApp;

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

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Files = new DirectoryInfo("C:\\Users\\thboo\\OneDrive").GetFiles("*", SearchOption.AllDirectories).Where(i => i.Length > 100000).ToList();
        canvas.SizeChanged += (_, _) => RenderCanvas();
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
        canvas.Children.Clear();
        FlavorToBrush = new();
        nextColorIndex = 0;
        if (double.IsNaN(canvas.ActualHeight) || double.IsNaN(canvas.ActualWidth)) return;

        TreeMapPlacer placer = new TreeMapPlacer();
        ITreeMapInput<FileSystemNode>[] input = GetTreeMapInputs();
        TreeMapBox<FileSystemNode>[] placements = placer.GetPlacements(input, canvas.ActualWidth, canvas.ActualHeight).ToArray();
        colorer.Initialize(placements.Select(p => p.Item.FileInfo).Where(f => f != null).Select(f => f!));
        foreach (var placement in placements)
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
            canvas.Children.Add(rect);
            rect.SetValue(Canvas.TopProperty, placement.Rectangle.Y);
            rect.SetValue(Canvas.LeftProperty, placement.Rectangle.X);
            ToolTip t = new ToolTip();
            t.Content = placement.Item.FullName;
            ToolTipService.SetToolTip(rect, t);
            rect.PointerEntered += (object sender, PointerRoutedEventArgs e) =>
            {
                fileText.Text = placement.Item.FullName + " - " + placement.Item.Size.ToString("N0");
                rect.Fill = new SolidColorBrush(Colors.Azure);
            };
            rect.PointerExited += (object sender, PointerRoutedEventArgs e) =>
            {
                rect.Fill = brush;
            };
        }
    }

    private Color[] colors =
    [
        Colors.DarkBlue, Colors.DarkRed, Colors.DarkGreen, Colors.DarkSeaGreen, Colors.Purple, Colors.DeepPink,
        Colors.Magenta, Colors.Brown, Colors.Coral, Colors.SlateBlue, Colors.Salmon,
        Colors.Orange, Colors.Gold, Colors.Green, Colors.Pink, Colors.DarkRed, Colors.Beige
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

    private static Color FadeColor(Color baseColor, double strength)
    {
        double fadeBy = 1 - strength;        
        return new Color
        {
            A = baseColor.A,
            B = (byte)(baseColor.B + (byte)(fadeBy * (256 - baseColor.B))),
            G = (byte)(baseColor.G + (byte)(fadeBy * (256 - baseColor.G))),
            R = (byte)(baseColor.R + (byte)(fadeBy * (256 - baseColor.R)))
        };
    }

    private static Brush DefaultBrush = CreateBrush(Colors.Black);
    private static Brush CreateBrush(Color color)
    {
        var lightColor = FadeColor(color, 0.4);
        return new RadialGradientBrush
        {
            MappingMode = BrushMappingMode.RelativeToBoundingBox,
            Center = new Point(1, 1),
            RadiusX = 1,
            RadiusY = 1,
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

    public List<FileInfo> Files { get; set; }

    private void coloringChanged(object sender, SelectionChangedEventArgs e)
    {
        string? colorBy = e.AddedItems.FirstOrDefault() as string;
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