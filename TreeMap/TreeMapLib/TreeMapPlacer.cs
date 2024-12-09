using System.Diagnostics;

namespace TreeMapLib;

public record Rect(double X, double Y, double Width, double Height)
{
    public readonly double X = X, Y = Y, Width = Width, Height = Height;
}
public class TreeMapBox(object item, string label, double size, Rect rectangle)
{
    public object Item = item;
    public Rect Rectangle = rectangle;
    public string Label = label;
    public double Size = size;
    public bool IsContainer;
    public double ContainerHeaderHeightPixels;
    public double BorderThicknessPixels;

    public override string ToString() => $"{nameof(Label)}: {Label}, {nameof(IsContainer)}: {IsContainer}, {nameof(Rectangle)}: {Rectangle}";
}

public interface ITreeMapInput
{
    public double Size { get; }
    public object Item { get; }
    public string Label { get; }
    public ITreeMapInput[] Children { get; }
}

public class TreeMapInput(double size, object item, string label, ITreeMapInput[] children) : ITreeMapInput
{
    public double Size { get; } = size;
    public object Item { get; } = item;
    public string Label { get; } = label;
    public ITreeMapInput[] Children { get; } = children;
}

public class TreeMapPlacer
{
    /// <summary>
    /// Set to true to enable containers to render around the leaves
    /// </summary>
    public bool RenderContainers = false;
    /// <summary>
    /// Percentage of the total area that a container must occupy in order to render the container
    /// </summary>
    public double MinimumAreaForContainerRender = 0.005;

    /// <summary>
    /// Pixels of header height for rendering containers
    /// </summary>
    public double ContainerHeaderHeightPixels = 15;

    public double ContainerBorderWidthPixels = 2;

    private double _totalArea;
    public IEnumerable<TreeMapBox> GetPlacements(IEnumerable<ITreeMapInput> input, double width, double height)
    {
        var sorted = input.Where(i => i.Size > 0).OrderByDescending(i => i.Size).ToList();
        if (sorted.Count == 0) throw new ArgumentException("Must have some elements to sort");
        _totalArea = width * height;
        return GetPlacementsSorted(sorted, 0, 0, width, height);
    }

    private IEnumerable<TreeMapBox> GetPlacementsSorted(List<ITreeMapInput> sorted, double x, double y, double xPrime, double yPrime)
    {
        if (sorted.Count == 0) yield break;

        Debug.Assert(x < xPrime);
        Debug.Assert(y < yPrime);

        if (sorted.Count == 1)
        {
            foreach (var i in RenderNode(sorted[0], new Rect(x, y, xPrime - x, yPrime - y))) yield return i;
            yield break;
        }
        
        double totalSize = sorted.Sum(i => i.Size);
        double percentage = sorted[0].Size / totalSize;

        double width = xPrime - x;
        double height = yPrime - y;
        double area = width * height;
        double boxArea = area * percentage;
        Debug.Assert(boxArea > 0);
        double smallSide = Math.Min(xPrime - x, yPrime - y);
        if (smallSide * smallSide < boxArea)
        {
            // the box will fill up the small side completely.
            if (width > height)
            {
                double boxWidth = boxArea / (yPrime - y);
                foreach (TreeMapBox i in RenderNode(sorted[0], new Rect(x, y, boxWidth, yPrime - y))) yield return i;
                foreach (TreeMapBox i in GetPlacementsSorted(sorted.Slice(1, sorted.Count - 1), x + boxWidth, y, xPrime, yPrime))
                {
                    yield return i;
                }
            }
            else
            {
                double boxHeight = boxArea / (xPrime - x);
                foreach (TreeMapBox i in RenderNode(sorted[0], new Rect(x, y, xPrime - x, boxHeight))) yield return i;
                foreach (TreeMapBox i in GetPlacementsSorted(sorted.Slice(1, sorted.Count - 1), x, y + boxHeight, xPrime, yPrime))
                {
                    yield return i;
                }
            }
        }
        else
        {
            // box won't fill the side. 
            (int numAdditionalBoxesToInclude, double bigSideLength) = CalculateAdditionalBoxesToInclude(sorted, totalSize, area, smallSide, Math.Sqrt(boxArea));
            Debug.Assert(bigSideLength > 0);
            if (width > height)
            {
                double boxHeight = boxArea / bigSideLength;
                foreach (var i in RenderNode(sorted[0], new Rect(x, y, bigSideLength, boxHeight))) yield return i;
                // plus the items below that box
                var under = GetPlacementsSorted(sorted.Slice(1, numAdditionalBoxesToInclude), x, y + boxHeight, x + bigSideLength, yPrime);
                // and the rest of the items
                var restOfItems = sorted.Slice(1 + numAdditionalBoxesToInclude, sorted.Count - (1 + numAdditionalBoxesToInclude));
                var right = GetPlacementsSorted(restOfItems, x + bigSideLength, y, xPrime, yPrime);
                foreach (var i in under.Concat(right))
                {
                    yield return i;
                }
            }
            else
            {
                double boxWidth = boxArea / bigSideLength;
                foreach (var i in RenderNode(sorted[0], new Rect(x, y, boxWidth, bigSideLength))) yield return i;
                // plus the items to the right of that box
                var right = GetPlacementsSorted(sorted.Slice(1, numAdditionalBoxesToInclude), x + boxWidth, y, xPrime, y + bigSideLength);
                // and the rest of the items
                var restOfItems = sorted.Slice(1 + numAdditionalBoxesToInclude, sorted.Count - (1 + numAdditionalBoxesToInclude));
                var under = GetPlacementsSorted(restOfItems, x, y + bigSideLength, xPrime, yPrime);
                foreach (var i in right.Concat(under))
                {
                    yield return i;
                }
            }
        }
    }

    private IEnumerable<TreeMapBox> RenderNode(ITreeMapInput input, Rect rect)
    {
        if (input.Children.Length == 0)
        {
            yield return new TreeMapBox(input.Item, input.Label, input.Size, rect);
        }
        else
        {
            double x = rect.X;
            double y = rect.Y;
            double xPrime = x + rect.Width;
            double yPrime = y + rect.Height;

            if (RenderContainers && input.Children.Length > 0)
            {
                // check if the container is large enough to render as a container
                if (rect.Height * rect.Height > _totalArea * MinimumAreaForContainerRender)
                {
                    yield return new TreeMapBox(input.Item, input.Label, input.Size, rect)
                    {
                        IsContainer = true,
                        ContainerHeaderHeightPixels = ContainerHeaderHeightPixels - ContainerBorderWidthPixels,
                        BorderThicknessPixels = ContainerBorderWidthPixels
                    };
                    x += ContainerBorderWidthPixels;
                    xPrime -= ContainerBorderWidthPixels;
                    yPrime -= ContainerBorderWidthPixels;
                    y += ContainerHeaderHeightPixels;
                }
            }
            var sorted = input.Children.Where(i => i.Size > 0).OrderByDescending(i => i.Size).ToList();
            Debug.Assert(sorted.Count > 0);
            foreach (var p in GetPlacementsSorted(sorted, x, y, xPrime, yPrime))
            {
                yield return p;
            }
        }
    }

    private (int, double) CalculateAdditionalBoxesToInclude(List<ITreeMapInput> sorted, double totalSize, double totalArea, double smallSide, double idealBoxSide)
    {
        // for explanation's sake, assume width>height.  
        // Algorithm is to make a vertical partition, and we want to find how many of the following items should be included in
        // order to make the first item closest to a square

        double minDistance = double.MaxValue;
        double includedTotalArea = 0;
        for (int i = 0; i < sorted.Count; i++)
        {
            includedTotalArea += totalArea * sorted[i].Size / totalSize;
            double includedBigSide = includedTotalArea / smallSide;
            // once the distance starts going up it won't come back down
            if (minDistance < Math.Abs(includedBigSide - idealBoxSide)) return (i, includedBigSide);
            minDistance = Math.Abs(includedBigSide - idealBoxSide);
        }

        return (sorted.Count - 1, (int)(includedTotalArea / smallSide));
    }
}