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
}
