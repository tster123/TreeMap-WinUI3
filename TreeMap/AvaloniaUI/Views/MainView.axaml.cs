using Avalonia.Controls;
using System.Collections.Generic;
using System;
using System.Linq;
using Avalonia.Interactivity;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using System.Diagnostics;
using System.Timers;
using Avalonia;
using Avalonia.Threading;
using Serilog;
using TreeMapLib;
using TreeMapLib.Models;
using TreeMapLib.Models.FileSystem;
using Rect = TreeMapLib.Rect;

namespace AvaloniaUI.Views;

public partial class MainView : UserControl
{
    private readonly IViewableModel model;

    public MainView()
    {
        // ReSharper disable once StringLiteralTypo
        model = new FileSystemModel("C:\\Users\\thboo\\OneDrive");
        InitializeComponent();
        ShowContainersCheckbox.IsCheckedChanged += ShowContainersCheckEvent;
        RenderDropDown.ItemsSource = model.RenderModes;
        RenderDropDown.SelectionChanged += ColoringChanged;
        RenderDropDown.SelectedIndex = 0;
        _showContainers = ShowContainersCheckbox.IsChecked ?? false;
        //Canvas.PointerMoved += OnPointerMoved;
        SizeChanged += MainView_SizeChanged;
        RenderCanvas();
    }

    private readonly Stopwatch _timeSinceLastTimeChange = new();
    private Timer? _sizeChangedTimer;

    private void MainView_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (double.IsNaN(e.NewSize.Width)) return;
        HoverText.Width = HoverText.MaxWidth = HoverText.MinWidth = e.NewSize.Width;
        HoverArea.Width = HoverArea.MaxWidth = HoverArea.MinWidth = e.NewSize.Width;

        _timeSinceLastTimeChange.Restart();
        lock (this)
        {
            if (_sizeChangedTimer == null)
            {
                _sizeChangedTimer = new Timer(TimeSpan.FromMilliseconds(50));
                _sizeChangedTimer.Elapsed += SizeChangedTimer_Elapsed;
                _sizeChangedTimer.AutoReset = true;
                _sizeChangedTimer.Enabled = true;
            }
        }
    }

    private void SizeChangedTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        lock (this)
        {
            if (_timeSinceLastTimeChange.Elapsed.TotalMilliseconds < 300)
            {
                // resize has happened in the last 100ms, so likely still dragging
                return;
            }

            Debug.Assert(_sizeChangedTimer != null);
            _sizeChangedTimer.Stop();
            _sizeChangedTimer.Dispose();
            _sizeChangedTimer = null;
        }
        Dispatcher.UIThread.Invoke(RenderCanvas);
    }

    private bool _showContainers;
    private void ShowContainersCheckEvent(object? sender, RoutedEventArgs e)
    {
        _showContainers = ShowContainersCheckbox.IsChecked ?? false;
        RenderCanvas();
    }

    private void ColoringChanged(object? sender, SelectionChangedEventArgs e)
    {
        string colorBy = RenderDropDown.SelectionBoxItem?.ToString() ?? model.RenderModes[0].Name;
        RenderMode? renderMode = model.RenderModes.SingleOrDefault(m => m.Name == colorBy);
        if (renderMode == null)
        {
            throw new ArgumentException("Cannot find colorer: [" + colorBy + "]");
        }
        colorer = renderMode.Colorer;
        RenderCanvas();
    }

    

    private Dictionary<string, BrushBase> flavorToBrush = new();
    private IColorer colorer = new ExtensionColoring();

    private void RenderCanvas()
    {
        if (Canvas == null || Canvas.Bounds.Width <= 0 || Canvas.Bounds.Height <= 0) return;
        Stopwatch sw = Stopwatch.StartNew();
        Canvas.Children.Clear();
        Log.Information("Cleared children {Elapsed}", sw);
        flavorToBrush = new();
        nextColorIndex = 0;

        Stopwatch sw2 = Stopwatch.StartNew();
        TreeMapPlacer placer = new TreeMapPlacer
        {
            RenderContainers = _showContainers
        };
        ITreeMapInput[] input = model.GetTreeMapInputs();
        TreeMapBox[] placements = placer.GetPlacements(input, Canvas.Bounds.Width, Canvas.Bounds.Height).ToArray();
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
        Canvas.Children.Add(header);
        header.SetValue(Canvas.TopProperty, r.Y + placement.BorderThicknessPixels);
        header.SetValue(Canvas.LeftProperty, r.X + placement.BorderThicknessPixels);
        textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
        if (r.Width < 6 * textBlock.Text.Length)
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

    private void RenderLeafPlacement(TreeMapBox placement)
    {
        string flavor = colorer.GetFlavor(placement.Item);

        if (!flavorToBrush.TryGetValue(flavor, out BrushBase? brushBase))
        {
            brushBase = GenerateNextBrush();
            flavorToBrush[flavor] = brushBase;
        }

        Brush brush = brushBase.GetBrushByStrength(colorer.GetColorStrength(placement.Item));
        
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
        
        rect.PointerEntered += (_, _) =>
        {
            string text = placement.Label + " - " + placement.Size.ToString("N0");
            //hoverText = text;
            //var model = (MainViewModel)Canvas.DataContext;
            //Task.Run(() => { model.HoveredItem = placement.Item.FullName + " - " + placement.Item.Size.ToString("N0"); });
            //HoverText.Text = 
            //HoverArea.Children.Clear();
            //HoverArea.Children.Add(new TextBlock{Text = text });
            HoverText.Text = text;
            rect.Fill = new SolidColorBrush(Colors.Azure);
            //hoveredRectangle = rect;
        };
        rect.PointerExited += (_, _) =>
        {
            //hoveredRectangle = null;
            rect.Fill = brush;
        };
    }
    /*
    private string hoverText;
    private Rectangle? hoveredRectangle;
    private IBrush? hoveredBrush;
    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (hoveredRectangle != null)
        {
            if (hoverText != HoverText.Text)
            {
                //HoverText.Text = hoverText;
            }
        }

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
    }
    */

    private readonly Color[] colors =
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

    private class BrushBase(Color baseColor)
    {
        private readonly Brush?[] fadingBrushes = new Brush?[101];

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
                fadingBrushes[strengthInt] = CreateBrush(FadeColor(baseColor, strength));
            }

            return fadingBrushes[strengthInt]!;
        }
    }
}