﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace TreeMap;

public record Rect(double X, double Y, double Width, double Height)
{
    public readonly double X = X, Y = Y, Width = Width, Height = Height;
}
public class TreeMapBox<T>(T item, Rect rectangle)
{
    public T Item = item;
    public Rect Rectangle = rectangle;
}

public interface ITreeMapInput<T>
{
    public double Size { get; }
    public T Item { get; }
}

public class TreeMapInput<T>(double size, T item) : ITreeMapInput<T>
{
    public double Size { get; } = size;
    public T Item { get; } = item;
}

public class TreeMapPlacer
{
    public IEnumerable<TreeMapBox<T>> GetPlacements<T>(IEnumerable<ITreeMapInput<T>> input, double width, double height)
    {
        var sorted = input.OrderByDescending(i => i.Size).ToList();
        if (sorted.Count == 0) throw new ArgumentException("Must have some elements to sort");
        return GetPlacementsSorted(sorted, 0, 0, width, height);
    }

    private IEnumerable<TreeMapBox<T>> GetPlacementsSorted<T>(List<ITreeMapInput<T>> sorted, double x, double y, double xPrime, double yPrime)
    {
        if (sorted.Count == 0) yield break;
        if (sorted.Count == 1)
        {
            yield return new TreeMapBox<T>(sorted[0].Item, new Rect(x, y, xPrime - x, yPrime - y));
            yield break;
        }

        Debug.Assert(x < xPrime);
        Debug.Assert(y < yPrime);
        
        double totalSize = sorted.Sum(i => i.Size);
        double percentage = sorted[0].Size / totalSize;

        double width = xPrime - x;
        double height = yPrime - y;
        double area = width * height;
        double boxArea = area * percentage;

        double smallSide = Math.Min(xPrime - x, yPrime - y);
        if (smallSide * smallSide < boxArea)
        {
            // the box will fill up the small side completely.
            if (width > height)
            {
                double boxWidth = boxArea / (yPrime - y);
                yield return new(sorted[0].Item, new Rect(x, y, boxWidth, yPrime - y));
                foreach (var i in GetPlacementsSorted(sorted.Slice(1, sorted.Count - 1), x + boxWidth, y, xPrime, yPrime))
                {
                    yield return i;
                }
            }
            else
            {
                double boxHeight = boxArea / (xPrime - x);
                yield return new(sorted[0].Item, new Rect(x, y, xPrime - x, boxHeight));
                foreach (var i in GetPlacementsSorted(sorted.Slice(1, sorted.Count - 1), x, y + boxHeight, xPrime, yPrime))
                {
                    yield return i;
                }
            }
        }
        else
        {
            // box won't fill the side. 
            (int numAdditionalBoxesToInclude, double bigSideLength) = CalculateAdditionalBoxesToInclude(sorted, totalSize, area, smallSide, Math.Sqrt(boxArea));
            if (width > height)
            {
                double boxHeight = boxArea / bigSideLength;
                yield return new TreeMapBox<T>(sorted[0].Item, new Rect(x, y, bigSideLength, boxHeight));
                // plus the items below that box
                var under = GetPlacementsSorted(sorted.Slice(1, numAdditionalBoxesToInclude), x, y + boxHeight, xPrime, yPrime);
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
                yield return new TreeMapBox<T>(sorted[0].Item, new Rect(x, y, boxWidth, bigSideLength));
                // plus the items to the right of that box
                var right = GetPlacementsSorted(sorted.Slice(1, numAdditionalBoxesToInclude), x + boxWidth, y, xPrime, yPrime);
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

    private (int, double) CalculateAdditionalBoxesToInclude<T>(List<ITreeMapInput<T>> sorted, double totalSize, double totalArea, double smallSide, double idealBoxSide)
    {
        // for explanation's sake, assume width>height.  
        // Algorithm is to make a vertical partition, and we want to find how many of the following items should be included in
        // order to make the first item closest to a square

        double minDistance = double.MaxValue;
        double includedTotalArea = 0;
        for (int i = 0; i < sorted.Count - 1; i++)
        {
            includedTotalArea += totalArea * sorted[i].Size / totalSize;
            double includedBigSide = includedTotalArea / smallSide;
            // once the distance starts going up it won't come back down
            if (minDistance < Math.Abs(includedBigSide - idealBoxSide)) return (i, includedBigSide);
            minDistance = Math.Abs(includedBigSide - idealBoxSide);
        }

        return (sorted.Count - 1, (int)(totalSize / smallSide));
    }
}