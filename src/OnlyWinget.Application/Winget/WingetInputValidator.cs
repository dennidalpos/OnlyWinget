namespace OnlyWinget.Application.Winget;

public static class WingetInputValidator
{
    private static readonly char[] DisallowedCharacters = ['"', '\'', '`', ';', '|', '&', '<', '>'];

    public static bool IsValid(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        return !input.Any(c => char.IsControl(c) || DisallowedCharacters.Contains(c));
    }

    public static void Validate(string input, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input, paramName);
        if (!IsValid(input))
        {
            throw new ArgumentException($"Invalid characters in argument: {paramName}", paramName);
        }
    }
}
