using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace TreeMap
{
    public sealed class TreeMapView : Control
    {
        public TreeMapView()
        {
            this.DefaultStyleKey = typeof(TreeMapView);
            
        }

        private IEnumerable<ITreeMapNode> _nodes;

        public IEnumerable<ITreeMapNode> Nodes
        {
            get => _nodes;
            set
            {
                _nodes = value;
                RenderTreeMap();
            }
        }

        private void RenderTreeMap()
        {
            
        }
    }
}
