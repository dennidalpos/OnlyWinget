namespace OnlyWinget.Models;

public sealed class AppEntry : ObservableObject
{
    private string _name = string.Empty;
    private string _id = string.Empty;
    private string _action = "Install";
    private string _status = string.Empty;

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

    public string Action
    {
        get => _action;
        set => SetProperty(ref _action, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }
}
