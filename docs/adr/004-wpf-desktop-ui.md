# ADR-004: WPF + CommunityToolkit.Mvvm for the desktop frontend

## Status
Accepted

## Context

A desktop GUI is planned as a second frontend alongside the CLI. The tool targets
Windows (where it will likely remain, given the PDF workflow use case). The UI
framework needs to be mature, well-documented, and practical for a small-team
or solo project.

Candidates evaluated:

| Framework | Notes |
|---|---|
| **WPF** | Mature, large ecosystem, excellent tooling in Visual Studio, Windows-only |
| WinUI 3 | Modern Windows UI, but tooling and packaging story still rough in practice |
| .NET MAUI | Cross-platform; significant overhead and complexity for a Windows-only tool |
| Avalonia | Cross-platform, actively developed; less documentation and fewer examples than WPF |

For the MVVM layer:

| Library | Notes |
|---|---|
| **CommunityToolkit.Mvvm** | Microsoft-maintained, source-generator based, minimal boilerplate |
| ReactiveUI | Powerful but steep learning curve; overkill for this scope |
| Plain MVVM (no library) | Viable but verbose; reinvents what CommunityToolkit provides |

## Decision

Use **WPF** with **CommunityToolkit.Mvvm**.

WPF is the lowest-friction choice for a Windows desktop tool: mature, stable,
well-understood, and fully supported in .NET 8+. CommunityToolkit.Mvvm provides
`[ObservableProperty]`, `[RelayCommand]`, and `ObservableObject` via source
generators — eliminating boilerplate without adding complexity.

Each operation gets a ViewModel that calls the corresponding Core operation class
directly. Views contain no business logic.

## Consequences

- Windows-only — acceptable given the tool's target audience
- WPF's XAML learning curve applies if contributors are unfamiliar with it
- If cross-platform desktop support is ever needed, this ADR should be revisited
  (Avalonia would be the natural migration target given its WPF compatibility)
