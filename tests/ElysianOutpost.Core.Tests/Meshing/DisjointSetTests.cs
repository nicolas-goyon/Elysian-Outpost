// SPDX-License-Identifier: MPL-2.0

using ElysianOutpost.Core.Meshing;

namespace ElysianOutpost.Core.Tests.Meshing;

public sealed class DisjointSetTests
{
    [Fact]
    public void Constructor_NegativeCount_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DisjointSet(-1));
    }

    [Fact]
    public void Find_NewStructure_GivesEachElementAsItsOwnRepresentative()
    {
        var set = new DisjointSet(5);

        Assert.Equal(5, set.SetCount);
        for (int element = 0; element < 5; element++)
        {
            Assert.Equal(element, set.Find(element));
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public void Find_InvalidElement_ThrowsArgumentOutOfRangeException(int element)
    {
        var set = new DisjointSet(5);

        Assert.Throws<ArgumentOutOfRangeException>(() => set.Find(element));
    }

    [Fact]
    public void Union_SetsOfTheSameSize_KeepsTheRepresentativeOfTheFirstElement()
    {
        var set = new DisjointSet(4);

        set.Union(1, 0);
        set.Union(2, 3);
        set.Union(3, 0);

        Assert.Equal(2, set.Find(0));
        Assert.Equal(2, set.Find(1));
        Assert.Equal(2, set.Find(3));
        Assert.Equal(1, set.SetCount);
    }

    // The set of 3 has two elements and the set of 0 has three elements.
    // A union by rank gives the same rank to the two sets, and keeps 3 as the representative.
    [Fact]
    public void Union_SmallerFirstSet_AttachesItToTheLargerSet()
    {
        var set = new DisjointSet(5);
        set.Union(0, 1);
        set.Union(0, 2);
        set.Union(3, 4);

        set.Union(3, 0);

        Assert.Equal(0, set.Find(3));
        Assert.Equal(0, set.Find(4));
        Assert.Equal(1, set.SetCount);
    }

    // The set of 3 gets four elements in two unions. The set of 0 gets three elements in two unions.
    // A size that counts the unions, not the elements, gives the same size to the two sets.
    [Fact]
    public void Union_SetSizes_CountTheElementsOfTheSets()
    {
        var set = new DisjointSet(7);
        set.Union(0, 1);
        set.Union(0, 2);
        set.Union(3, 4);
        set.Union(5, 6);
        set.Union(3, 5);

        set.Union(0, 3);

        Assert.Equal(3, set.Find(0));
        Assert.Equal(3, set.Find(6));
        Assert.Equal(1, set.SetCount);
    }

    [Fact]
    public void Union_ElementsOfTheSameSet_DoesNotChangeTheSets()
    {
        var set = new DisjointSet(3);
        set.Union(0, 1);

        set.Union(1, 0);

        Assert.Equal(2, set.SetCount);
        Assert.Equal(0, set.Find(1));
        Assert.Equal(2, set.Find(2));
    }

    // Compares the sets with a simple reference: an array that gives a label to each element.
    [Fact]
    public void Union_RandomOperations_GivesTheSameSetsAsAReference()
    {
        const int Count = 60;
        var random = new Random(31);
        var set = new DisjointSet(Count);
        int[] labels = Enumerable.Range(0, Count).ToArray();

        for (int operation = 0; operation < 200; operation++)
        {
            int first = random.Next(Count);
            int second = random.Next(Count);
            set.Union(first, second);

            int oldLabel = labels[second];
            int newLabel = labels[first];
            for (int element = 0; element < Count; element++)
            {
                if (labels[element] == oldLabel)
                {
                    labels[element] = newLabel;
                }
            }

            int a = random.Next(Count);
            int b = random.Next(Count);
            Assert.Equal(labels[a] == labels[b], set.Find(a) == set.Find(b));
        }

        Assert.Equal(labels.Distinct().Count(), set.SetCount);
    }
}
