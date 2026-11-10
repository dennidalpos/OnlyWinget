namespace OnlyWinget.Application.Storage;

public static class AppDataPaths
{
    public const string RootEnvironmentVariable = "ONLYWINGET_DATA_ROOT";

    public static string Root { get; } = ResolveRoot(
        Environment.GetEnvironmentVariable(RootEnvironmentVariable),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    internal static string ResolveRoot(string? configuredRoot, string localApplicationData)
    {
        var root = configuredRoot ?? Path.Combine(localApplicationData, StorageConstants.ApplicationFolderName);
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root))
        {
            throw new ArgumentException($"{RootEnvironmentVariable} must specify a fully qualified directory path.");
        }

        return Path.GetFullPath(root);
    }
}
