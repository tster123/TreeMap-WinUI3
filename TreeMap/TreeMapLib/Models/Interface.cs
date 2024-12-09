using System.Collections;

namespace TreeMapLib.Models;


public interface IViewableModel
{
    public RenderMode[] RenderModes { get; }
    public ITreeMapInput[] GetTreeMapInputs();
    public string GetHoverText(object item);
}

public class RenderMode(string name, IColorer colorer)
{
    public readonly string Name = name;
    public readonly IColorer Colorer = colorer;
    public override string ToString() => Name;
}

public interface IColorer
{
    public bool UsesFlavors { get; }
    public bool UsesStrength { get; }

    string GetFlavor(object item);
    double GetColorStrength(object item);

    void Initialize(IEnumerable allItems)
    {
    }
}

public abstract class TypedColorer<T> : IColorer
{
    public abstract bool UsesFlavors { get; }
    public abstract bool UsesStrength { get; }
    public string GetFlavor(object item)
    {
        return GetFlavorTyped((T)item);
    }

    public abstract string GetFlavorTyped(T item);

    public double GetColorStrength(object item)
    {
        return GetColorStrengthTyped((T)item);
    }
    public abstract double GetColorStrengthTyped(T item);

    public void Initialize(IEnumerable allItems)
    {
        InitializeTyped(allItems.Cast<T>());
    }

    public virtual void InitializeTyped(IEnumerable<T> allItems)
    {

    }
}
