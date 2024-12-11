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
using Avalonia.Media.Immutable;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Serilog;
using TreeMapLib;
using TreeMapLib.Models;
using TreeMapLib.Models.FileSystem;
using Rect = TreeMapLib.Rect;
using Avalonia.Input;

namespace AvaloniaUI.Views;

/*
 *  TODO list:
 * 1) add list of flavors on top with total count & size
 * 2) add tree view of items on top
 * 3) allow clicking to select an item (both in tree map and tree view)
 * 4) implement input file as model
 * 5) add item size filter to UI
 * 6) add ability to selectively exclude nodes
 * 7) add ability to selectively exclude flavors
 * 8) add small item collapsing
 * 9) zoomable UI
 * 10) optimizations to handle 1m+ items
 */

public partial class MainView : UserControl
{
    private readonly IViewableModel model;
    public Window? Window;
    private bool CustomerRender = true;
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

        PointerMoved += OnPointerMoved;
        SizeChanged += MainView_SizeChanged;
        AttachCustomVisual(Canvas);
        RenderCanvas();
    }

    private readonly TreeMapVisualHandler treeMapVisualHandler = new();
    private CompositionCustomVisual? _customVisual;
    void AttachCustomVisual(Visual v)
    {
        if (!CustomerRender) return;
        void Update()
        {
            if (_customVisual == null)
                return;
            var h = (float)Math.Min(v.Bounds.Height, v.Bounds.Width / 3);
            _customVisual.Size = new(v.Bounds.Width, h);
            _customVisual.Offset = new(0, (v.Bounds.Height - h) / 2, 0);
        }
        v.AttachedToVisualTree += (sender, args) =>
        {
            var compositor = ElementComposition.GetElementVisual(v)?.Compositor;
            if (compositor == null || _customVisual?.Compositor == compositor)
                return;
            _customVisual = compositor.CreateCustomVisual(treeMapVisualHandler);
            ElementComposition.SetElementChildVisual(v, _customVisual);
            Update();
        };

        v.PropertyChanged += (_, a) =>
        {
            if (a.Property == BoundsProperty)
                Update();
        };
    }

    private readonly Stopwatch _timeSinceLastTimeChange = new();
    private Timer? _sizeChangedTimer;

    private void MainView_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (double.IsNaN(e.NewSize.Width)) return;
        HoverText.Width = HoverText.MaxWidth = HoverText.MinWidth = e.NewSize.Width;
        //HoverArea.Width = HoverArea.MaxWidth = HoverArea.MinWidth = e.NewSize.Width;

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

        if (!CustomerRender)
        {
            Canvas.Children.Clear();
        }
        else
        {
            treeMapVisualHandler.Clear();
        }
        
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

    private Rectangle? clicked;
    private void RenderLeafPlacement(TreeMapBox placement)
    {
        string flavor = colorer.GetFlavor(placement.Item);

        if (!flavorToBrush.TryGetValue(flavor, out BrushBase? brushBase))
        {
            brushBase = GenerateNextBrush();
            flavorToBrush[flavor] = brushBase;
        }

        IImmutableBrush brush = brushBase.GetBrushByStrength(colorer.GetColorStrength(placement.Item));
        
        var rect = new Rectangle
        {
            Fill = brush,
            Height = placement.Rectangle.Height,
            Width = placement.Rectangle.Width,
        };
        var myRect = new MyRect(rect, brush, placement.Rectangle);
        if (CustomerRender)
        {
            treeMapVisualHandler.AddRectangle(myRect);
        }
        else
        {
            Canvas.Children.Add(rect);
            rect.SetValue(Canvas.TopProperty, placement.Rectangle.Y);
            rect.SetValue(Canvas.LeftProperty, placement.Rectangle.X);
        }

        
        rect.DataContext = placement;
        //ToolTip t = new ToolTip();
        //t.Content = placement.Item.FullName;
        //ToolTipService.SetToolTip(rect, t);
        rect.PointerPressed += (_, _) =>
        {
            clicked = rect;
        };
        rect.PointerEntered += (_, _) =>
        {
            string text = placement.Label + " - " + placement.Size.ToString("N0");
            HoverText.Text = text;

            if (CustomerRender)
            {
                myRect.Brush = new ImmutableSolidColorBrush(Colors.Azure);
                treeMapVisualHandler.AddRectangle(myRect);
            }
            else
            {
                rect.Fill = new SolidColorBrush(Colors.Azure);
            }
            
            
            
            if (clicked != null)
            {
                Color[] c = [Colors.Red, Colors.Aqua, Colors.Blue, Colors.Green, Colors.SaddleBrown, Colors.Yellow, Colors.Violet, Colors.SteelBlue];
                clicked.Fill = new SolidColorBrush(c[new Random().Next(c.Length)]);
            }
        };
        rect.PointerExited += (_, _) =>
        {
            //hoveredRectangle = null;
            if (CustomerRender)
            {
                myRect.Brush = brush;
                treeMapVisualHandler.AddRectangle(myRect);
            }
            else
            {
                rect.Fill = brush;
            }
            
        };
    }

    private string hoverText;
    private MyRect? hoveredRectangle;
    private IImmutableBrush? hoveredBrush;
    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (hoveredRectangle != null)
        {
            if (hoverText != HoverText.Text)
            {
                HoverText.Text = hoverText;
            }
        }

        Point p = e.GetPosition(Canvas);
        MyRect found = null;
        foreach (MyRect r in treeMapVisualHandler.allRectangles)
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
            hoveredRectangle.Brush = hoveredBrush;
            treeMapVisualHandler.MarkRectangleDirty(hoveredRectangle);
            _customVisual?.SendHandlerMessage(TreeMapVisualHandler.CheckRefreshMessage);
            hoveredRectangle = null;
            hoveredBrush = null;
            return;
        }

        if (hoveredRectangle != null)
        {
            // set the old one back to normal
            hoveredRectangle.Brush = hoveredBrush;
            treeMapVisualHandler.MarkRectangleDirty(hoveredRectangle);
        }

        hoveredBrush = found.Brush;
        found.Brush = new ImmutableSolidColorBrush(Colors.Azure);
        hoveredRectangle = found;
        string str = found.Rect.DataContext?.ToString() ?? "<not found>";
        hoverText = str;
        treeMapVisualHandler.MarkRectangleDirty(hoveredRectangle);
        _customVisual?.SendHandlerMessage(TreeMapVisualHandler.CheckRefreshMessage);
        Console.WriteLine(str);
        HoverText.Text = str;
    }


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

    private static IImmutableBrush CreateBrush(Color color)
    {
        IEnumerable<ImmutableGradientStop> gradientStops =
        [
            new ImmutableGradientStop(0, FadeColor(color, 0.4)),
            new ImmutableGradientStop(1, color)
        ];
        return new ImmutableRadialGradientBrush(
            gradientStops: new List<ImmutableGradientStop>(gradientStops),
            center: RelativePoint.BottomRight,
            radiusX: RelativeScalar.End,
            radiusY: RelativeScalar.End);
    }

    private class BrushBase(Color baseColor)
    {
        private readonly IImmutableBrush?[] fadingBrushes = new IImmutableBrush?[101];

        public IImmutableBrush GetBrushByStrength(double strength)
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

    class MyRect (Rectangle rect, IImmutableBrush brush, Rect r)
    {
        public readonly Rectangle Rect = rect;
        public readonly Avalonia.Rect Bounds = new(r.X, r.Y, r.Width, r.Height);
        public IImmutableBrush Brush = brush;
    }

    class TreeMapVisualHandler : CompositionCustomVisualHandler
    {
        public readonly List<MyRect> allRectangles = new();
        private readonly HashSet<MyRect> toRefresh = new();
        public static readonly object CheckRefreshMessage = new();
        private bool refreshWhole = true;

        public TreeMapVisualHandler()
        {
        }

        public void AddRectangle(MyRect rect)
        {
            allRectangles.Add(rect);
            lock (toRefresh) toRefresh.Add(rect);
        }

        public void Clear()
        {
            allRectangles.Clear();
            refreshWhole = true;
        }

        public void MarkRectangleDirty(MyRect rect)
        {
            lock(toRefresh) toRefresh.Add(rect);
        }

        public override void OnMessage(object message)
        {
            if (message == CheckRefreshMessage)
            {
                RegisterForNextAnimationFrameUpdate();
            }
        }

        public void MarkCanvasDirty()
        {
            refreshWhole = true;
        }

        public override void OnRender(ImmediateDrawingContext drawingContext)
        {
            if (refreshWhole)
            {
                foreach (MyRect r in allRectangles)
                {
                    drawingContext.DrawRectangle(r.Brush, null, r.Bounds);
                }
                lock (toRefresh) toRefresh.Clear();
                refreshWhole = false;
            }
            else
            {
                lock (toRefresh)
                {
                    foreach (MyRect r in toRefresh)
                    {
                        drawingContext.DrawRectangle(r.Brush, null, r.Bounds);
                        Log.Logger.Information("Rendered: " + BoundsStr(r.Bounds));
                    }

                    toRefresh.Clear();
                }
            }
        }

        private string BoundsStr(Avalonia.Rect r)
        {
            return $"x [{r.X} - {r.X + r.Width}], y [{r.Y} - {r.Y + r.Height}]";
        }
        
        public override void OnAnimationFrameUpdate()
        {
            if (refreshWhole)
            {
                Invalidate();
            }
            else
            {
                lock (toRefresh)
                {
                    foreach (MyRect rect in toRefresh)
                    {
                        Invalidate(rect.Bounds);
                        Log.Logger.Information("Invalidated: " + BoundsStr(rect.Bounds));
                    }
                }
            }
            //RegisterForNextAnimationFrameUpdate();
        }

    }
}

