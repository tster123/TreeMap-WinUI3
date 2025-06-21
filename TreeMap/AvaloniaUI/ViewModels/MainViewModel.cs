using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls.Models.TreeDataGrid;
using TreeMapLib;
using TreeMapLib.Models.FileSystem;
using TreeMapLib.Models;

namespace AvaloniaUI.ViewModels;

public class MainViewModel : ViewModelBase
{
    public readonly ObservableCollection<ITreeMapInput> Items = new();
    public HierarchicalTreeDataGridSource<ITreeMapInput> TreeSource { get; }
    public IColorer Colorer { get; set; }

    public IEnumerable<FlavorGridItem> FlavorGridItems
    {
        get
        {
            List<FlavorGridItem> ret = new();
            Dictionary<string, FlavorGridItem> map = new();
            double totalSize = 0;
            foreach (var i in Items)
            {
                string flavor = Colorer.GetFlavor(i.Item);
                if (!map.TryGetValue(flavor, out FlavorGridItem gi))
                {
                    gi = new FlavorGridItem(flavor);
                    map[flavor] = gi;
                    ret.Add(gi);
                }

                totalSize += i.Size;
                gi.Size += i.Size;
                gi.Count++;
            }

            foreach (FlavorGridItem gi in ret)
            {
                gi.Percentage = gi.Size / totalSize;
            }

            return ret.OrderByDescending(r => r.Size);
        }
    }

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
        Items.CollectionChanged += Items_CollectionChanged;
    }

    public void SetModel(IViewableModel model)
    {
        /*
        TreeSource.Columns.Clear();
        TreeSource.Columns.AddRange([
            new HierarchicalExpanderColumn<ITreeMapInput>(
                new TextColumn<ITreeMapInput, string>("Label", i => i.Label), i => i.Children
            ),
            new TextColumn<ITreeMapInput, double>("Size", i => i.Size)
        ]);
        foreach (string c in model.InfoColumns)
        {
            TreeSource.Columns.Add(new TextColumn<ITreeMapInput, string>(c, i => i.GetInfo(c)));
        }
        */
    }

    private void Items_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(new PropertyChangedEventArgs("FlavorGridItems"));
    }
}

public record FlavorGridItem(string Flavor)
{
    public readonly string Flavor = Flavor;
    public int Count;
    public double Size;
    public double Percentage;
}