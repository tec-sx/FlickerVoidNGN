; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
FVG001 | FlickerVoid | Error | EntityClassGenerator, a partial property of an entity class that is not a component by ref
FVG002 | FlickerVoid | Error | EntityClassGenerator, an entity class without components
FVG003 | FlickerVoid | Error | EntityClassGenerator, a nested or generic entity class
