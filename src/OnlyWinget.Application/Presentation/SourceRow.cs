using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OnlyWinget.Application.Presentation;

public sealed class SourceRow : INotifyPropertyChanged
{
    private string name;
    private string argument;
    private bool isExplicit;
    private string type;
    private string status;
    private bool isEnabled;

    public SourceRow(string name, string argument, bool isExplicit, string type, string status, bool isEnabled)
    {
        this.name = name;
        this.argument = argument;
        this.isExplicit = isExplicit;
        this.type = type;
        this.status = status;
        this.isEnabled = isEnabled;
    }

    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    public string Argument
    {
        get => argument;
        set => SetProperty(ref argument, value);
    }

    public bool IsExplicit
    {
        get => isExplicit;
        set => SetProperty(ref isExplicit, value);
    }

    public string Type
    {
        get => type;
        set => SetProperty(ref type, value);
    }

    public string Status
    {
        get => status;
        set => SetProperty(ref status, value);
    }

    public bool IsEnabled
    {
        get => isEnabled;
        set => SetProperty(ref isEnabled, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return;
        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public override bool Equals(object? obj) =>
        obj is SourceRow other &&
        name == other.name &&
        argument == other.argument &&
        isExplicit == other.isExplicit &&
        type == other.type &&
        status == other.status &&
        isEnabled == other.isEnabled;

    public override int GetHashCode() =>
        global::System.HashCode.Combine(name, argument, isExplicit, type, status, isEnabled);
}
