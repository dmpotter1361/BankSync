# BankSync — notes for Claude Code

Windows system-tray app that tracks Bank of America / M&T / E*TRADE balances and
syncs them to Google Sheets. C# / .NET 10, Windows-only. Repo `dmpotter1361/BankSync`.

## Run / build

```powershell
dotnet run                 # launch the tray app
dotnet build -c Release    # release build
```

No tests. Plain `dotnet` is enough.

## Layout

- **`Program.cs`** — entry point.
- **`TrayApp.cs`** — main `ApplicationContext`: balance polling + Sheets sync.
- **`TrayIcon.cs`** — tray icon + context menu.

## Secrets — important

- **`credentials.json`** holds Google API credentials. **Never commit real secrets.**
  Confirm it is git-ignored (or templated) before any `git add`/push.
- Code-signing: a "BankSync" Public Trust cert profile exists but full verification
  is blocked on a DUNS number — releases are currently unsigned/provisional.

## Conventions

- Forms hand-written in code; **UI must survive font/display-scaling changes**
  (TableLayoutPanel/FlowLayoutPanel + AutoSize + font fallback).
