using Microsoft.UI.Xaml;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OnlyWinget.Controls;

public sealed class FixedTableLayout
{
    public GridLength Column0Width { get; set; }
    public GridLength Column1Width { get; set; }
    public GridLength Column2Width { get; set; }
    public GridLength Column3Width { get; set; }
    public GridLength Column4Width { get; set; }
    public GridLength Column5Width { get; set; }
    public GridLength Column6Width { get; set; }
    public GridLength Column7Width { get; set; }
    public GridLength Column8Width { get; set; }
    public GridLength Column9Width { get; set; }
}

public sealed class TableColumnLayout : DependencyObject, INotifyPropertyChanged
{
    private GridLength selectionWidth = new(44);
    private GridLength nameWidth = new(210);
    private GridLength packageIdWidth = new(250);
    private GridLength sourceWidth = new(100);
    private GridLength installedWidth = new(110);
    private GridLength availableWidth = new(120);
    private GridLength architectureWidth = new(130);
    private GridLength statusWidth = new(150);

    public event PropertyChangedEventHandler? PropertyChanged;

    public GridLength SelectionWidth { get => selectionWidth; set => Set(ref selectionWidth, value); }
    public GridLength NameWidth { get => nameWidth; private set => Set(ref nameWidth, value); }
    public GridLength PackageIdWidth { get => packageIdWidth; private set => Set(ref packageIdWidth, value); }
    public GridLength SourceWidth { get => sourceWidth; private set => Set(ref sourceWidth, value); }
    public GridLength InstalledWidth { get => installedWidth; private set => Set(ref installedWidth, value); }
    public GridLength AvailableWidth { get => availableWidth; private set => Set(ref availableWidth, value); }
    public GridLength ArchitectureWidth { get => architectureWidth; private set => Set(ref architectureWidth, value); }
    public GridLength StatusWidth { get => statusWidth; private set => Set(ref statusWidth, value); }

    public GridLength GetWidth(int index) => index switch
    {
        1 => NameWidth,
        2 => PackageIdWidth,
        3 => SourceWidth,
        4 => InstalledWidth,
        5 => AvailableWidth,
        6 => ArchitectureWidth,
        7 => StatusWidth,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };

    public void SetWidth(int index, double value)
    {
        var width = new GridLength(Math.Max(72, value));
        switch (index)
        {
            case 1: NameWidth = width; break;
            case 2: PackageIdWidth = width; break;
            case 3: SourceWidth = width; break;
            case 4: InstalledWidth = width; break;
            case 5: AvailableWidth = width; break;
            case 6: ArchitectureWidth = width; break;
            case 7: StatusWidth = width; break;
            default: throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    private void Set(ref GridLength field, GridLength value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
