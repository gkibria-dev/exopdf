# ADR-005: Built-in WPF Fluent theme for the desktop look and feel

## Status
Accepted

## Context

The Desktop UI must look modern, follow the system light/dark setting, and let the
user override the theme in Settings (DUI-3, DUI-20). The default WPF controls use
the classic Windows look, so something has to provide the modern styling.

Candidates evaluated:

| Option | Notes |
|---|---|
| **Built-in Fluent theme (`ThemeMode`)** | Ships with WPF in .NET 9+. No extra package. Restyles the standard controls and supports System, Light and Dark. |
| WPF-UI (lepoco) | Third-party, MIT. Fluent styling plus extra controls (NavigationView, InfoBar). Adds a dependency with its own release cadence. |
| MahApps.Metro | Third-party, mature. Metro-style look rather than Fluent. |
| Hand-written styles | No dependency, but a large amount of styling work for every control. |

## Decision

Use the **built-in Fluent theme** (`Application.ThemeMode`).

The sidebar and the result/error panels are not provided by the built-in theme.
They are small custom styles owned by this project.

## Consequences

- No new UI package to track.
- On SDK 10.0.401, setting `ThemeMode` from C# raises the experimental-API
  diagnostic `WPF0001`. Setting it in XAML does not. The Desktop project
  suppresses `WPF0001` (project-wide, in its `.csproj`), and all runtime theme
  switching lives in one class, `ThemeService`, so an API change touches one file.
- The Fluent theme is designed for Windows 11. Its behaviour on Windows 10 must be
  checked during implementation.
- If the theme API is removed or changed incompatibly, or if the project needs
  controls the built-in theme lacks, revisit this ADR. WPF-UI is the likely
  replacement.
