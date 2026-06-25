namespace OnlyWinget.Domain.Validation;

public sealed record ValidationResult(IReadOnlyList<ValidationIssue> Issues)
{
    public static ValidationResult Success { get; } = new([]);

    public bool IsValid => Issues.Count == 0;
}
