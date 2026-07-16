using OnlyWinget.Domain.Selection;

namespace OnlyWinget.Tests;

public sealed class SelectionStateTests
{
    [Fact]
    public void ToggleAllSelectsEveryRowFromUnchecked()
    {
        var selection = new SelectionState<string>();
        selection.ReplaceAvailable(["a", "b", "c"]);

        selection.ToggleAll();

        Assert.Equal(SelectionHeaderState.Checked, selection.HeaderState);
        Assert.Equal(["a", "b", "c"], selection.Selected.Order());
    }

    [Fact]
    public void ToggleAllSelectsEveryRowFromMixed()
    {
        var selection = new SelectionState<string>();
        selection.ReplaceAvailable(["a", "b", "c"]);
        selection.SetSelected("a", true);

        selection.ToggleAll();

        Assert.Equal(SelectionHeaderState.Checked, selection.HeaderState);
        Assert.Equal(["a", "b", "c"], selection.Selected.Order());
    }

    [Fact]
    public void ToggleAllClearsEveryRowFromChecked()
    {
        var selection = new SelectionState<string>();
        selection.ReplaceAvailable(["a", "b"]);
        selection.ToggleAll();

        selection.ToggleAll();

        Assert.Equal(SelectionHeaderState.Unchecked, selection.HeaderState);
        Assert.Empty(selection.Selected);
    }

    [Fact]
    public void Toggle_StaleKey_IsNoOp()
    {
        var selection = new SelectionState<string>();
        selection.ReplaceAvailable(["a", "b"]);
        selection.SetSelected("a", true);

        // Simulate a refresh that removes "a" from available
        selection.ReplaceAvailable(["b"]);

        // "a" is now stale — Toggle must not throw and state must be unchanged
        selection.Toggle("a");

        Assert.Empty(selection.Selected);
        Assert.Equal(SelectionHeaderState.Unchecked, selection.HeaderState);
    }

    [Fact]
    public void SetSelected_StaleKey_IsNoOp()
    {
        var selection = new SelectionState<string>();
        selection.ReplaceAvailable(["a", "b"]);
        selection.SetSelected("b", true);

        // Simulate a refresh that removes "b" from available
        selection.ReplaceAvailable(["a"]);

        // "b" is now stale — SetSelected must not throw and state must be unchanged
        selection.SetSelected("b", true);

        Assert.Empty(selection.Selected);
        Assert.Equal(SelectionHeaderState.Unchecked, selection.HeaderState);
    }
}
