namespace OnlyWinget.Presentation;

public sealed class ValidatedField(Func<string, string?> validate) : ObservableObject
{
    private string value = string.Empty;
    private string? error;

    public string Value
    {
        get => value;
        set
        {
            if (SetProperty(ref this.value, value))
            {
                Error = validate(value.Trim());
                OnPropertyChanged(nameof(IsValid));
            }
        }
    }

    public string? Error
    {
        get => error;
        private set => SetProperty(ref error, value);
    }

    public bool IsValid => string.IsNullOrEmpty(Error);

    public void Validate()
    {
        Error = validate(Value.Trim());
        OnPropertyChanged(nameof(IsValid));
    }

    public void Clear()
    {
        value = string.Empty;
        error = null;
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(Error));
        OnPropertyChanged(nameof(IsValid));
    }
}
