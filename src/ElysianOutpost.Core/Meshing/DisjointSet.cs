// SPDX-License-Identifier: MPL-2.0

namespace ElysianOutpost.Core.Meshing;

/// <summary>
/// A union-find structure for a fixed number of elements. The elements are the numbers from 0 to the number of elements minus 1.
/// Each element is in one set.
/// <see cref="Union"/> joins two sets. <see cref="Find"/> gives the representative of the set of an element.
/// </summary>
/// <remarks>
/// <see cref="Union"/> attaches the smaller set to the larger set. If the two sets have the same size,
/// the representative of the first element stays the representative of the joined set.
/// <see cref="Find"/> attaches each element on its path directly to the representative.
/// Thus the next calls of <see cref="Find"/> for these elements are faster.
/// </remarks>
public sealed class DisjointSet
{
    private readonly int[] _parents;
    private readonly int[] _sizes;

    /// <summary>
    /// Initializes a new instance of the <see cref="DisjointSet"/> class. Each element is in a set of its own.
    /// </summary>
    /// <param name="count">The number of elements.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    public DisjointSet(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        _parents = new int[count];
        _sizes = new int[count];
        for (int element = 0; element < count; element++)
        {
            _parents[element] = element;
            _sizes[element] = 1;
        }

        SetCount = count;
    }

    /// <summary>
    /// Gets the number of sets.
    /// </summary>
    public int SetCount { get; private set; }

    /// <summary>
    /// Gives the representative of the set that contains an element.
    /// </summary>
    /// <param name="element">The element: a number from 0 to the number of elements minus 1.</param>
    /// <returns>The representative element of the set.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="element"/> is not a valid element.</exception>
    public int Find(int element)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(element);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(element, _parents.Length);

        int representative = element;
        while (_parents[representative] != representative)
        {
            representative = _parents[representative];
        }

        // Attach each element of the path directly to the representative.
        while (element != representative)
        {
            int next = _parents[element];
            _parents[element] = representative;
            element = next;
        }

        return representative;
    }

    /// <summary>
    /// Joins the set that contains <paramref name="first"/> and the set that contains <paramref name="second"/>.
    /// If the two elements are already in the same set, this method does nothing.
    /// </summary>
    /// <param name="first">An element of the first set.</param>
    /// <param name="second">An element of the second set.</param>
    /// <exception cref="ArgumentOutOfRangeException">An element is not a valid element.</exception>
    public void Union(int first, int second)
    {
        int larger = Find(first);
        int smaller = Find(second);
        if (larger == smaller)
        {
            return;
        }

        // If the two sets have the same size, the representative of the first element does not change.
        if (_sizes[larger] < _sizes[smaller])
        {
            (larger, smaller) = (smaller, larger);
        }

        _parents[smaller] = larger;
        _sizes[larger] += _sizes[smaller];
        SetCount--;
    }
}
