using OnlyWinget.Presentation;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Xunit;

namespace OnlyWinget.Tests;

public sealed class ObservableCollectionExtensionsTests
{
    private record TestRow(string Id, string Value, int Count = 0);

    [Fact]
    public void ReplaceWithReplacesAllItems()
    {
        var target = new ObservableCollection<string>(["old1", "old2"]);
        target.ReplaceWith(["new1", "new2", "new3"]);

        Assert.Equal(["new1", "new2", "new3"], target);
    }

    [Fact]
    public void SynchronizeWithKeepsExistingOrderAndRemovesMissing()
    {
        var target = new ObservableCollection<TestRow>([
            new("A", "Alpha"),
            new("B", "Beta"),
            new("C", "Gamma")
        ]);

        var desired = new[]
        {
            new TestRow("A", "Alpha-Updated"),
            new TestRow("C", "Gamma-Updated")
        };

        target.SynchronizeWith(desired, row => row.Id);

        Assert.Equal(2, target.Count);
        Assert.Equal("A", target[0].Id);
        Assert.Equal("Alpha-Updated", target[0].Value);
        Assert.Equal("C", target[1].Id);
        Assert.Equal("Gamma-Updated", target[1].Value);
    }

    [Fact]
    public void SynchronizeWithHandlesInsertionsAndReordering()
    {
        var target = new ObservableCollection<TestRow>([
            new("A", "1"),
            new("B", "2"),
            new("C", "3")
        ]);

        var desired = new[]
        {
            new TestRow("C", "3"),
            new TestRow("D", "4"),
            new TestRow("A", "1")
        };

        target.SynchronizeWith(desired, row => row.Id);

        Assert.Equal(["C", "D", "A"], target.Select(row => row.Id));
        Assert.Equal(["3", "4", "1"], target.Select(row => row.Value));
    }

    [Fact]
    public void SynchronizeWithUsesCustomUpdateActionWhenProvided()
    {
        var item1 = new MutableRow { Id = "1", Title = "Old1" };
        var item2 = new MutableRow { Id = "2", Title = "Old2" };
        var target = new ObservableCollection<MutableRow>([item1, item2]);

        var desired = new[]
        {
            new MutableRow { Id = "1", Title = "New1" },
            new MutableRow { Id = "2", Title = "New2" }
        };

        target.SynchronizeWith(desired, r => r.Id, (existing, incoming) => existing.Title = incoming.Title);

        Assert.Same(item1, target[0]);
        Assert.Same(item2, target[1]);
        Assert.Equal("New1", target[0].Title);
        Assert.Equal("New2", target[1].Title);
    }

    [Fact]
    public void SynchronizeWithPerformanceOnLargeCollection()
    {
        const int count = 2000;
        var initial = Enumerable.Range(0, count)
            .Select(i => new TestRow($"id_{i}", $"val_{i}"))
            .ToArray();

        var target = new ObservableCollection<TestRow>(initial);

        // Update 5 items, remove 2, insert 2 at the end
        var desired = initial
            .Where(r => r.Id != "id_10" && r.Id != "id_20")
            .Select(r => r.Id == "id_5" ? r with { Value = "val_5_mod" } : r)
            .Concat([new TestRow("id_new_1", "val_new_1"), new TestRow("id_new_2", "val_new_2")])
            .ToArray();

        var sw = Stopwatch.StartNew();
        target.SynchronizeWith(desired, r => r.Id);
        sw.Stop();

        Assert.Equal(count, target.Count);
        Assert.Equal("val_5_mod", target.First(r => r.Id == "id_5").Value);
        Assert.Contains(target, r => r.Id == "id_new_1");
        Assert.DoesNotContain(target, r => r.Id == "id_10");

        // Synchronize on 2000 items with linear search fast path should execute in well under 100ms
        Assert.True(sw.ElapsedMilliseconds < 500, $"SynchronizeWith took {sw.ElapsedMilliseconds} ms");
    }

    private sealed class MutableRow
    {
        public required string Id { get; init; }
        public required string Title { get; set; }
    }
}
