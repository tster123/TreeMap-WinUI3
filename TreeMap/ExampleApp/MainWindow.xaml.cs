using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Windows.Foundation;
using Microsoft.UI;
using Microsoft.UI.Xaml.Shapes;
using Serilog;
using TreeMapLib;
using TreeMapLib.Models;
using Color = Windows.UI.Color;
using Rect = TreeMapLib.Rect;
using TreeMapLib.Models.FileSystem;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ExampleApp;



/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window
{
    private IViewableModel model;
    public MainWindow()
    {
        model = new FileSystemModel("C:\\Users\\thboo\\OneDrive");
        InitializeComponent();
        _showContainers = showContainersCheckbox.IsChecked ?? false;
        canvas.SizeChanged += (_, _) => RenderCanvas();
    }


    private Dictionary<string, BrushBase> flavorToBrush = new();
    private IColorer colorer = new ExtensionColoring();
    private void RenderCanvas()
    {
        if (canvas == null || canvas.ActualWidth <= 0 || canvas.ActualHeight <= 0) return;
        Stopwatch sw = Stopwatch.StartNew();
        canvas.Children.Clear();
        Log.Information("Cleared children {Elapsed}", sw);
        flavorToBrush = new();
        nextColorIndex = 0;
        if (double.IsNaN(canvas.ActualHeight) || double.IsNaN(canvas.ActualWidth)) return;

        Stopwatch sw2 = Stopwatch.StartNew();
        TreeMapPlacer placer = new TreeMapPlacer();
        placer.RenderContainers = _showContainers;
        ITreeMapInput[] input = model.GetTreeMapInputs();
        TreeMapBox[] placements = placer.GetPlacements(input, canvas.ActualWidth, canvas.ActualHeight).ToArray();
        Log.Information("Buildings placements took {Elapsed}", sw2);
        colorer.Initialize(placements.Select(p => p.Item));
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

    private void RenderContainerPlacement(TreeMapBox placement)
    {
        Rect r = placement.Rectangle;
        var textBlock = new TextBlock
        {
            Text = placement.Label,
            Height = placement.ContainerHeaderHeightPixels,
            FontSize = placement.ContainerHeaderHeightPixels - 2,
            Foreground = new SolidColorBrush(Colors.Black),
        };
        var header = new Border
        {
            Background = new SolidColorBrush(Colors.LightGray),
            Child = textBlock
        };
        canvas.Children.Add(header);
        header.SetValue(Canvas.TopProperty, r.Y + placement.BorderThicknessPixels);
        header.SetValue(Canvas.LeftProperty, r.X + placement.BorderThicknessPixels);
        textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
        if (r.Width < 6 * placement.Label.Length)
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
        canvas.Children.Add(border);
        border.SetValue(Canvas.TopProperty, r.Y);
        border.SetValue(Canvas.LeftProperty, r.X);
    }

    private void RenderLeafPlacement(TreeMapBox placement)
    {
        Brush brush;
        string flavor = colorer.GetFlavor(placement.Item);

        if (!flavorToBrush.TryGetValue(flavor, out BrushBase? brushBase))
        {
            brushBase = GenerateNextBrush();
            flavorToBrush[flavor] = brushBase;
        }

        brush = brushBase.GetBrushByStrength(colorer.GetColorStrength(placement.Item));

        var rect = new Rectangle
        {
            Fill = brush,
            Height = placement.Rectangle.Height,
            Width = placement.Rectangle.Width,
        };
        canvas.Children.Add(rect);
        rect.SetValue(Canvas.TopProperty, placement.Rectangle.Y);
        rect.SetValue(Canvas.LeftProperty, placement.Rectangle.X);
        //ToolTip t = new ToolTip();
        //t.Content = placement.Item.FullName;
        //ToolTipService.SetToolTip(rect, t);
        rect.PointerEntered += (object sender, PointerRoutedEventArgs e) =>
        {
            fileText.Text = placement.Label + " - " + placement.Size.ToString("N0");
            rect.Fill = new SolidColorBrush(Colors.Azure);
        };
        rect.PointerExited += (object sender, PointerRoutedEventArgs e) =>
        {
            rect.Fill = brush;
        };
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

    private static Brush _defaultBrush = CreateBrush(Colors.Black);
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

    private void coloringChanged(object sender, SelectionChangedEventArgs e)
    {
        string? colorBy = e.AddedItems[0].ToString();
        if (colorBy == null) colorBy = model.RenderModes[0].Name;
        RenderMode? renderMode = model.RenderModes.SingleOrDefault(m => m.Name == colorBy);
        if (renderMode == null)
        {
            throw new ArgumentException("Cannot find colorer: [" + colorBy + "]");
        }
        colorer = renderMode.Colorer;
        RenderCanvas();
    }

    private bool _showContainers;
    private void ShowContainersChecked(object sender, RoutedEventArgs e)
    {
        _showContainers = true;
        DispatcherQueue.TryEnqueue(RenderCanvas);
        //RenderCanvas();
    }

    private void ShowContainersUnchecked(object sender, RoutedEventArgs e)
    {
        _showContainers = false;
        DispatcherQueue.TryEnqueue(RenderCanvas);
       // RenderCanvas();
    }
}
