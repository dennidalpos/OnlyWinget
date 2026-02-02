namespace OnlyWinget.Models;

public sealed class UpdateEntry : ObservableObject
{
    private string _name = string.Empty;
    private string _id = string.Empty;
    private string _version = string.Empty;
    private string _available = string.Empty;
    private bool _selected;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string Version
    {
        get => _version;
        set => SetProperty(ref _version, value);
    }

    public string Available
    {
        get => _available;
        set => SetProperty(ref _available, value);
    }

    public bool Selected
    {
        get => _selected;
        set => SetProperty(ref _selected, value);
    }
}
