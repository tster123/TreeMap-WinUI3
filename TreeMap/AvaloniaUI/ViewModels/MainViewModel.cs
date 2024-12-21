using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls.Models.TreeDataGrid;
using TreeMapLib;

namespace AvaloniaUI.ViewModels;

public class MainViewModel : ViewModelBase
{
    public readonly ObservableCollection<ITreeMapInput> Items = new();
    public HierarchicalTreeDataGridSource<ITreeMapInput> TreeSource { get; }

    public MainViewModel()
    {
        TreeSource = new HierarchicalTreeDataGridSource<ITreeMapInput>(Items)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<ITreeMapInput>(
                    new TextColumn<ITreeMapInput, string>("Label", i => i.Label), i => i.Children
                ),
                new TextColumn<ITreeMapInput, double>("Size", i => i.Size)
            }
        };
    }
}
