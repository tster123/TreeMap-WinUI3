using Avalonia.Controls;
using System.Collections.Generic;
using System;
using Avalonia.Media;
using System.Diagnostics;
using Avalonia;
using Avalonia.Media.Immutable;
using Avalonia.Rendering.Composition;
using Serilog;
using Rect = TreeMapLib.Rect;
using Avalonia.Input;

namespace AvaloniaUI.Views;

public class ReproBox(string label, Rect rectangle)
{
    public Rect Rectangle = rectangle;
    public string Label = label;

    public override string ToString() => $"{nameof(Label)}: {Label}, {nameof(Rectangle)}: {Rectangle}";
}

public partial class MainView : UserControl
{
    public Window? Window;
    public MainView()
    {
        InitializeComponent();

        PointerMoved += OnPointerMoved;
        PointerPressed += OnPointerPressed;
        AttachCustomVisual(Canvas);
        Canvas.SizeChanged += Canvas_SizeChanged;
        RenderCanvas();
    }

    private void Canvas_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        RenderCanvas();
    }

    private readonly TreeMapVisualHandler treeMapVisualHandler = new();
    private CompositionCustomVisual? _customVisual;
    void AttachCustomVisual(Visual v)
    {
        void Update()
        {
            if (_customVisual == null)
                return;
            _customVisual.Size = new(v.Bounds.Width, v.Bounds.Height);
            _customVisual.Offset = new(0, 0, 0);
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

    private void RenderCanvas()
    {
        if (Canvas == null || Canvas.Bounds.Width <= 0 || Canvas.Bounds.Height <= 0) return;
        Stopwatch sw = Stopwatch.StartNew();
        treeMapVisualHandler.Clear();
        
        Log.Information("Cleared children {Elapsed}", sw);

        List<ReproBox> placements = GenerateReproPlacements(Canvas.Bounds.Width, Canvas.Bounds.Height);
        foreach (var placement in placements)
        {
            RenderLeafPlacement(placement);
        }

        Log.Information("RenderCanvas took {Elapsed}", sw);
    }

    private List<ReproBox> GenerateReproPlacements(double width, double height)
    {
        int ROWS = 300;
        int COLUMNS = 300;
        List<ReproBox> ret = new();
        for (int x = 0; x < COLUMNS; x++)
        {
            if (x < COLUMNS / 2 || x % 5 != 0)
            {
                for (int y = 0; y < ROWS; y++)
                {
                    ret.Add(new ReproBox($"({x}, {y})", new Rect(x * width / COLUMNS, y * height / ROWS, width / COLUMNS, height / COLUMNS)));
                }
            }
            else
            {
                int h = 5 + new Random().Next(20);
                for (int y = 0; y < ROWS; y += h)
                {
                    ret.Add(new ReproBox($"({x}, {y})", new Rect(x * width / COLUMNS, y * height / ROWS, width / COLUMNS, h * height / COLUMNS)));
                }
            }
        }
        return ret;
    }

    private readonly IImmutableBrush brush = CreateBrush(Colors.Green);

    private void RenderLeafPlacement(ReproBox placement)
    {
        var myRect = new MyRect(placement.Label, brush, placement.Rectangle);
        treeMapVisualHandler.AddRectangle(myRect);
    }

    private string hoverText;
    private MyRect? hoveredRectangle;
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
        MyRect found = null;
        foreach (MyRect r in treeMapVisualHandler.allRectangles)
        {
            if (r.Bounds.Left < p.X && r.Bounds.Right > p.X && r.Bounds.Top < p.Y && r.Bounds.Bottom > p.Y)
            {
                found = r;
                break;
            }
        }

        // either no rectangle was previously hovered and no rectangle is currently hovered (both are null), 
        // or the mouse is over the rectangle that is already known to be hovered (so already colored)
        if (found == hoveredRectangle) return;

        // mouse moved out of the bounds of all rectangles
        if (found == null)
        {
            if (hoveredRectangle == null) return;

            // set the old one back to normal
            hoveredRectangle.Brush = brush;
            treeMapVisualHandler.MarkRectangleDirty(hoveredRectangle);
            _customVisual?.SendHandlerMessage(TreeMapVisualHandler.CheckRefreshMessage);
            hoveredRectangle = null;
            return;
        }

        // at this point we know found is a rectangle that is newly being moused over.

        if (hoveredRectangle != null)
        {
            // set the old one back to normal
            hoveredRectangle.Brush = brush;
            treeMapVisualHandler.MarkRectangleDirty(hoveredRectangle);
        }

        found.Brush = new ImmutableSolidColorBrush(Colors.Azure);
        hoveredRectangle = found;
        string str = found.Name;
        hoverText = str;
        treeMapVisualHandler.MarkRectangleDirty(hoveredRectangle);

        if (clickedRectangle != null)
        {
            clickedColorIndex = (clickedColorIndex + 1) % clickedColors.Length;
            clickedRectangle.Brush = new ImmutableSolidColorBrush(clickedColors[clickedColorIndex]);
            treeMapVisualHandler.MarkRectangleDirty(clickedRectangle);
        }
        _customVisual?.SendHandlerMessage(TreeMapVisualHandler.CheckRefreshMessage);
    }

    private Color[] clickedColors = [Colors.Black, Colors.Pink, Colors.Blue, Colors.Orange, Colors.Yellow];
    private int clickedColorIndex = 0;

    private MyRect? clickedRectangle;
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (hoveredRectangle != null)
        {
            clickedRectangle = hoveredRectangle;
        }
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

    class MyRect (string name, IImmutableBrush brush, Rect r)
    {
        public readonly string Name = name;
        public readonly Avalonia.Rect Bounds = new(r.X, r.Y, r.Width, r.Height);
        public IImmutableBrush Brush = brush;
    }

    class TreeMapVisualHandler : CompositionCustomVisualHandler
    {
        public readonly List<MyRect> allRectangles = new();
        private readonly HashSet<MyRect> toRefresh = new();
        private readonly HashSet<MyRect> toRender = new();
        public static readonly object CheckRefreshMessage = new();
        private bool refreshWhole = true;


        public void AddRectangle(MyRect rect)
        {
            allRectangles.Add(rect);
        }

        public void Clear()
        {
            allRectangles.Clear();
            refreshWhole = true;
        }

        public void MarkRectangleDirty(MyRect rect)
        {
            lock(this) toRefresh.Add(rect);
        }

        private bool registered = false;
        public override void OnMessage(object message)
        {
            if (message == CheckRefreshMessage)
            {
                if (!registered && toRefresh.Count > 0)
                {
                    RegisterForNextAnimationFrameUpdate();
                    registered = true;
                }
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
                Log.Logger.Information($"Rendering Full!");
                foreach (MyRect r in allRectangles)
                {
                    drawingContext.DrawRectangle(r.Brush, null, r.Bounds);
                }
                refreshWhole = false;
            }
            else
            {
               
                lock (this)
                {
                    int i = 0;
                    foreach (MyRect r in toRender)
                    {
                        i++;
                        drawingContext.DrawRectangle(r.Brush, null, r.Bounds);
                        Log.Logger.Information($"Rendered {i} of {toRender.Count}: " + BoundsStr(r.Bounds));
                    }
                    toRender.Clear();
                }

            }
        }

        private string BoundsStr(Avalonia.Rect r)
        {
            return $"x [{r.X} - {r.Right}], y [{r.Y} - {r.Bottom}]";
        }
        
        public override void OnAnimationFrameUpdate()
        {
            registered = false;
            Log.Logger.Information("OnAnimationFrameUpdate");
            if (refreshWhole)
            {
                Log.Logger.Information($"Invalidating Full!");
                Invalidate();
                lock (this)
                {
                    toRefresh.Clear();
                    toRender.Clear();
                }
            }
            else
            {
                lock (this)
                {
                    int i = 0;
                    foreach (MyRect rect in toRefresh)
                    {
                        i++;
                        Invalidate(rect.Bounds);
                        Log.Logger.Information($"Invalidated {i} of {toRefresh.Count}: " + BoundsStr(rect.Bounds));
                        toRender.Add(rect);
                    }
                    toRefresh.Clear();
                }
            }
        }

    }
}

