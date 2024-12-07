using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Foundation;
using ABI.Windows.UI;
using Microsoft.UI;
using Microsoft.UI.Xaml.Shapes;
using TreeMap;
using Color = Windows.UI.Color;
using Path = Microsoft.UI.Xaml.Shapes.Path;

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
                fileChildren.Add(new TreeMapInput<FileSystemNode>(file.Length, new FileSystemNode(FullName, file.Name, file.Length), []));
            }

            children.Add(new TreeMapInput<FileSystemNode>(filesSize, new FileSystemNode(FullName, "<files>", filesSize), fileChildren.ToArray()));
        }

        foreach (var child in Children)
        {
            children.Add(child.Value.GetTreeMapInput());
        }

        return new(Size, new FileSystemNode(ParentName, Name, Size), children.ToArray());
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

    private Dictionary<string, Brush> FlavorToBrush = new();

    private void RenderCanvas()
    {
        canvas.Children.Clear();
        if (double.IsNaN(canvas.ActualHeight) || double.IsNaN(canvas.ActualWidth)) return;

        TreeMapPlacer placer = new TreeMapPlacer();
        ITreeMapInput<FileSystemNode>[] input = GetTreeMapInputs();
        var placements = placer.GetPlacements(input, canvas.ActualWidth, canvas.ActualHeight);

        foreach (var placement in placements)
        {
            string extension = "";
            if (placement.Item.FullName.LastIndexOf('.') >= 0)
            {
                extension = placement.Item.FullName.Substring(placement.Item.FullName.LastIndexOf('.'));
            }

            extension = extension.ToLower();
            if (!FlavorToBrush.TryGetValue(extension, out Brush? brush))
            {
                brush = GenerateNextBrush();
                FlavorToBrush[extension] = brush;
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
        Colors.Blue, Colors.Red, Colors.Green, Colors.Yellow, Colors.Beige, Colors.Pink, Colors.Purple,
        Colors.Magenta, Colors.Aquamarine, Colors.Brown, Colors.Coral, Colors.SlateBlue, Colors.Salmon,
        Colors.Orange, Colors.Gold
    ];

    private int nextColorIndex = 0;
    private Brush GenerateNextBrush()
    {
        Color color = Colors.Gray;
        if (nextColorIndex < colors.Length)
        {
            color = colors[nextColorIndex];
            nextColorIndex++;
        }
        var lightColor = new Color
        {
            A = color.A,
            B = (byte)(color.B + (byte)(0.4 * (256 - color.B))),
            G = (byte)(color.G + (byte)(0.4 * (256 - color.G))),
            R = (byte)(color.R + (byte)(0.4 * (256 - color.R)))
        };
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
}

public class FileSystemNode(string folderName, string name, long size)
{
    public string FolderName { get; } = folderName;
    public string Name { get; } = name;
    public readonly string FullName = System.IO.Path.Combine(folderName, name);
    public long Size { get; } = size;

    public override string ToString() => System.IO.Path.Combine(FolderName, Name);
}