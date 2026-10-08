# Source generation and interop review

Read this reference only when the requested change affects generated code or interop. OnlyWinget packaging does not require an AOT/trimming migration.

- Resolve settings from the actual project. Preserve existing generated ObservableProperty fields and command patterns; do not introduce a partial-property migration to follow a generic sample.
- Keep existing JSON contracts, serializer options and persisted schemas. A source-generated serializer must preserve compatibility and be verified through actual callers.
- Preserve WinUI binding modes and generated property names. x:Bind compilation does not verify interactive editor behavior.
- Native Windows Update uses asynchronous COM jobs with callback cleanup; reflection/dynamic access must remain compatible with that runtime path. Do not enable trimming blindly.
- Use existing interop infrastructure. Adding CsWin32 or another package requires the requested scope and repository dependency approval.
- Never edit generated output; change its source and regenerate through the toolchain.

Sources: [System.Text.Json source generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation), [ObservableProperty generator](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/generators/observableproperty).
