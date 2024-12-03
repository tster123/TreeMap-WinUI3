using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using ABI.Windows.UI;
using Microsoft.UI;
using Microsoft.UI.Xaml.Shapes;
using TreeMap;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ExampleApp
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Files = new DirectoryInfo("C:\\Users\\thboo\\Downloads").GetFiles().ToList();
            canvas.SizeChanged += (_, _) => RenderCanvas();
        }

        private void RenderCanvas()
        {
            if (double.IsNaN(canvas.Width) || double.IsNaN(canvas.Height)) return;
            var radialBrush = new RadialGradientBrush
            {
                MappingMode = BrushMappingMode.RelativeToBoundingBox,
                Center = new Point(1, 1),
                RadiusX = 1,
                RadiusY = 1,
                //GradientOrigin = new Point(0, 0),
                GradientStops =
                {
                    new GradientStop
                    {
                        Color = Colors.LightBlue,
                        Offset = 0
                    },
                    new GradientStop()
                    {
                        Color = Colors.Blue,
                        Offset = 1
                    }
                }
            };

            TreeMapPlacer placer = new TreeMapPlacer();
            var placements = placer.GetPlacements(Files.Select(f => new TreeMapInput<FileInfo>(f.Length, f)), canvas.Width, canvas.Height);

            foreach (var placement in placements)
            {
                var rect = new Rectangle
                {
                    Fill = radialBrush,
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
                    fileText.Text = placement.Item.FullName;
                    rect.Fill = new SolidColorBrush(Colors.Azure);
                };
                rect.PointerExited += (object sender, PointerRoutedEventArgs e) =>
                {
                    rect.Fill = radialBrush;
                };
            }
        }

        public List<FileInfo> Files { get; set; }
    }
}
