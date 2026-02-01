using System;
using Avalonia.Controls;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls.Models.TreeDataGrid;
using TreeMapLib;
using TreeMapLib.Models;

namespace AvaloniaUI.ViewModels;

public class MainViewModel : ViewModelBase
{
    public ObservableCollection<FlavorGridItem> FlavorGridItems { get; } = new ObservableCollection<FlavorGridItem>();

    public readonly ObservableCollection<ITreeMapInput> Items = new();
    public HierarchicalTreeDataGridSource<ITreeMapInput> TreeSource { get; }
    public IColorer Colorer { get; set; }

    public IEnumerable<FlavorGridItem> RecalculateFlavorGridItems()
    {
        List<FlavorGridItem> ret = new();
        Dictionary<string, FlavorGridItem> map = new();
        double totalSize = 0;
        foreach (var i in Items)
        {
            string flavor = Colorer.GetFlavor(i.Item);
            if (flavor == null || flavor == "") flavor = "<empty>";
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

    public MainViewModel()
    {
        TreeSource = new HierarchicalTreeDataGridSource<ITreeMapInput>(Items)
        {
            Columns =
            {
                new HierarchicalExpanderColumn<ITreeMapInput>(
                    new TextColumn<ITreeMapInput, string>("Label", i => i.Label), i => i.Children
                ),
                new TextColumn<ITreeMapInput, double>("Size", i => i.Size),
                new TextColumn<ITreeMapInput, string>("Last Modified", i => i.GetInfo("Last Modified"))
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
        IEnumerable<FlavorGridItem> target = RecalculateFlavorGridItems();
        FlavorGridItems.Clear();
        foreach (var i in target) FlavorGridItems.Add(i);
        //OnPropertyChanged(new PropertyChangedEventArgs("FlavorGridItems"));
    }
}

public record FlavorGridItem(string Flavor)
{
    public readonly string Flavor = Flavor;
    public int Count;
    public double Size;
    public double Percentage;
}