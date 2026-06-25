namespace OnlyWinget.Application.Presentation;

public sealed record PresentationCommand(string Id, string ResourceKey, bool IsEnabled);
