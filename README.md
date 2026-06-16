# BankSync

<p align="center">
  <img src="logo.png" width="96" alt="BankSync icon">
</p>

A small Windows tray app that keeps an eye on your bank balances and pops up a
clean summary whenever you click it. It logs into your banks the way you
normally would (once), remembers the session, and can sync everything to a
Google Sheet on a schedule.

> Personal project. Not affiliated with, endorsed by, or sponsored by any bank
> or financial institution.

## Status

Early-stage / pre-release — there's no installer yet, so for now it's
build-and-run from source (see below).

## Features

- **Tray app** — runs quietly next to the clock; click to pop up a balance
  summary near the tray, with recent and pending transactions.
- **Two ways to connect a bank:**
  - **Browser session (recommended)** — a one-time guided login captures your
    session (DPAPI-encrypted, stored locally); BankSync then revisits the
    balance page in the background on a timer and reads whatever dollar
    amount you picked during setup. No bank API needed.
  - **OFX / Direct Connect** — for banks that support it (Bank of America,
    M&T, Chase, Wells Fargo, Citibank, US Bank presets included), BankSync can
    talk directly to the bank's OFX endpoint with stored credentials.
- **Google Sheets sync (optional)** — push balances to a cell in your own
  spreadsheet after each refresh.
- **First-run setup wizard** — walks a new user through connecting Google
  Sheets before they ever see the Settings window.
- **Light / dark theme**, rounded window corners, and a "launch at Windows
  startup" toggle.
- **Per-account refresh interval**, manual "sync now" from the popup, and a
  live spinner while a sync is running.

## Privacy

Everything runs locally. Bank passwords (for the OFX path) are stored in
**Windows Credential Manager**, not in a config file. Browser sessions are
encrypted at rest with Windows DPAPI (current user only) and never leave the
machine. Google OAuth tokens are stored locally under `%APPDATA%\BankSync\`.
There's no telemetry and no third-party server in the loop — BankSync only
talks to your bank's site and, if you enable it, Google's Sheets API.

## Build from source

Prerequisites:

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- Google Chrome installed (the browser-session path drives your real Chrome,
  not a bundled copy)

```powershell
dotnet build
dotnet run
```

The first time you build, Playwright needs its driver fetched once (browsers
themselves aren't downloaded, since BankSync drives your installed Chrome):

```powershell
pwsh bin/Debug/net10.0-windows/playwright.ps1 install
```

### Google Sheets sync (optional, for your own build)

The public repo doesn't ship Google OAuth credentials. To enable Sheets sync
on your own build:

1. In [Google Cloud Console](https://console.cloud.google.com/), create an
   OAuth Client ID (type **Desktop app**).
2. Open BankSync → Settings → Google Sheets, and paste in the Client ID and
   Client Secret.
3. Click **Set Up Google Auth** and sign in.

(If you're building this for someone else to install — not just running it
yourself — drop those same credentials into a `credentials.json` next to the
exe; BankSync will pick them up automatically and skip asking the end user
for them. This file is git-ignored on purpose — never commit it.)

## How it works

Banks don't offer a personal-use balance API, so BankSync drives a real
Chromium browser. During setup, a visible browser opens to the bank's login
page; you log in (including any 2FA) and navigate to your balance page
yourself, then tell BankSync which dollar amount on the page is your balance.
From then on, BankSync reopens that page invisibly (positioned off-screen, not
headless, so banks' bot-detection doesn't flag it) on a timer, re-reads the
same amount, and updates the tray popup — re-encrypting the session after
every successful run. If the bank ever invalidates the session, BankSync
detects the redirect and asks you to log in again.

For banks that support OFX Direct Connect, [`OFXService.cs`](Services/OFXService.cs)
talks to the bank's OFX endpoint directly instead, using credentials from
Windows Credential Manager.

## Continuing development with Claude Code

This project was built with AI assistance and is set up so you can keep going
the same way. To pick up where it left off on your own machine:

1. **Get the code onto your PC**

   ```bash
   git clone https://github.com/dmpotter1361/BankSync.git
   cd BankSync
   ```

2. **Install [Claude Code](https://claude.com/claude-code)** (Anthropic's coding CLI)
   and start it in the project folder:

   ```bash
   npm install -g @anthropic-ai/claude-code
   claude
   ```

   (You can also use the Claude Code extension for VS Code / JetBrains, or
   [claude.ai/code](https://claude.ai/code).)

3. **Point Claude at the project and ask for what you want.** A good first prompt:

   > Read the README, `ScraperService.cs`, and `OFXService.cs`, then build and
   > run the app so you understand it. I'd like to add &lt;your feature&gt;.

### Helpful map for a new contributor (human or AI)

- **`Services/ScraperService.cs`** — the browser-session login, balance
  scraping, and session encryption logic.
- **`Services/OFXService.cs`** — the OFX/Direct Connect path and known bank
  endpoint presets.
- **`Services/CredentialService.cs`** — Windows Credential Manager wrapper for
  OFX usernames/passwords.
- **`Services/GoogleSheetsService.cs`** — Google OAuth + Sheets API calls.
- **`Models/AppConfig.cs`** — settings persisted to `%APPDATA%\BankSync\config.json`,
  plus the bundled-credentials fallback.
- **`TrayApp.cs`** — the app's brain: tray icon, menu, sync scheduling.
- **`Forms/BalancePopup.cs`** — the popup shown on tray click.
- **`Forms/SettingsForm.cs` / `Forms/AccountDialog.cs` / `Forms/BrowserSetupForm.cs` / `Forms/FirstRunWizard.cs`** — the windows.
- **`Helpers/AppTheme.cs`** — the single source of truth for light/dark colors.

## Acknowledgments

BankSync was designed and built collaboratively with **Claude** (Anthropic's
AI), pair-programming with the author from the first idea through the
scraping logic, the Sheets sync, the UI and theming, and this README. The
direction, decisions, and real-world testing are human; a lot of the
implementation was AI-assisted — and we're happy to say so. 🤖🤝

## License

[MIT](LICENSE)
