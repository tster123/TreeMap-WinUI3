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
            double totalSize = Files.Sum(f => f.Length);
            int x = 0;
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
            foreach (var file in Files)
            {
                double percentageOfTotal = file.Length / totalSize;

                var rect = new Rectangle
                {
                    Fill = radialBrush,
                    Height = 300 * percentageOfTotal,
                    Width = 30
                };
                canvas.Children.Add(rect);
                rect.SetValue(Canvas.TopProperty, 0);
                rect.SetValue(Canvas.LeftProperty, x);
                ToolTip t = new ToolTip();
                t.Content = file.FullName;
                ToolTipService.SetToolTip(rect, t);
                rect.PointerEntered += (object sender, PointerRoutedEventArgs e) =>
                {
                    fileText.Text = file.FullName;
                    rect.Fill = new SolidColorBrush(Colors.Azure);
                };
                rect.PointerExited += (object sender, PointerRoutedEventArgs e) =>
                {
                    rect.Fill = radialBrush;
                };
                x += 35;
            }
        }

        public List<FileInfo> Files { get; set; }
    }
}
