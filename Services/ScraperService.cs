using BankSync.Models;
using Microsoft.Playwright;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace BankSync.Services;

public static class ScraperService
{
    private static readonly string SessionDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BankSync", "sessions");

    public static string SessionPath(Guid id) => Path.Combine(SessionDir, $"{id}.json");
    public static bool HasSession(Guid id) => File.Exists(SessionPath(id));
    public static bool HadSession(Guid id)  => File.Exists(MetaPath(id));

    private const string BalanceScanScript = @"() => {
        const regex = /^\$[\d,]+\.\d{2}$/;
        const found = [];
        const seen = new Set();
        const skip = new Set(['SCRIPT','STYLE','NOSCRIPT','HEAD','META','LINK']);
        document.querySelectorAll('*').forEach(el => {
            if (skip.has(el.tagName)) return;
            const text = (el.textContent || '').trim();
            if (!regex.test(text) || seen.has(text)) return;
            const hasMatchingChild = Array.from(el.children).some(c => (c.textContent || '').trim() === text);
            if (hasMatchingChild) return;
            seen.add(text);
            const label = (el.closest('tr,li,div[class],td')?.textContent || '').trim().replace(/\s+/g,' ').substring(0,80);
            const path = el.id ? '#'+el.id : el.className ? el.tagName.toLowerCase()+'.'+el.className.trim().split(/\s+/)[0] : el.tagName.toLowerCase();
            found.push({ amount: text, label: label || text, path: path });
        });
        return JSON.stringify(found);
    }";

    public static void ClearSession(Guid id)
    {
        var path = SessionPath(id);
        if (File.Exists(path)) File.Delete(path);
    }

    // Opens a visible browser for the user to log in.
    // Returns the page so the caller can save session + URL after user confirms.
    public static async Task<(IPlaywright pw, IBrowser browser, IBrowserContext ctx, IPage page)>
        LaunchLoginBrowserAsync(string loginUrl)
    {
        var pw = await Playwright.CreateAsync();
        var browser = await LaunchAsync(pw, visible: true); // visible so user can log in
        var ctx = await browser.NewContextAsync();
        var page = await ctx.NewPageAsync();
        await page.GotoAsync(loginUrl);
        return (pw, browser, ctx, page);
    }

    // Saves session state + the current URL as the balance page.
    public static async Task SaveSessionAsync(IBrowserContext ctx, Guid accountId, string balanceUrl)
    {
        Directory.CreateDirectory(SessionDir);
        await ctx.StorageStateAsync(new() { Path = SessionPath(accountId) });
        EncryptSessionFile(SessionPath(accountId));

        var meta = new ScraperMeta { BalanceUrl = balanceUrl };
        File.WriteAllText(MetaPath(accountId), JsonSerializer.Serialize(meta));
    }

    // Encrypts session file in-place using Windows DPAPI (current user only).
    private static void EncryptSessionFile(string path)
    {
        var plain = File.ReadAllBytes(path);
        var encrypted = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(path, encrypted);
    }

    // Decrypts to a temp file for Playwright to read, then caller deletes the temp file.
    private static string DecryptSessionToTemp(string path)
    {
        var encrypted = File.ReadAllBytes(path);
        byte[] plain;
        try   { plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser); }
        catch { plain = encrypted; } // graceful fallback for unencrypted legacy files
        var temp = Path.Combine(Path.GetTempPath(), $"banksync_{Guid.NewGuid():N}.json");
        File.WriteAllBytes(temp, plain);
        return temp;
    }

    // Scans the already-open page for dollar amounts (used during setup — no new browser needed).
    public static async Task<List<FoundBalance>> FindBalancesOnCurrentPageAsync(IPage page)
    {
        await page.WaitForTimeoutAsync(1500);
        var results = await page.EvaluateAsync<string>(BalanceScanScript);
        return JsonSerializer.Deserialize<List<FoundBalance>>(results ?? "[]",
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
    }

    // Returns list of dollar amounts found on the balance page (for user to confirm).
    public static async Task<List<FoundBalance>> FindBalancesOnPageAsync(Guid accountId)
    {
        var meta = LoadMeta(accountId);
        if (meta == null) return [];

        using var pw = await Playwright.CreateAsync();
        var browser = await LaunchAsync(pw);
        var tempSession = DecryptSessionToTemp(SessionPath(accountId));
        try
        {
            var ctx = await browser.NewContextAsync(new() { StorageStatePath = tempSession });
            var page = await ctx.NewPageAsync();
            await page.GotoAsync(meta.BalanceUrl, new() { WaitUntil = WaitUntilState.Load, Timeout = 60_000 });
            await page.WaitForTimeoutAsync(3000);

            var results = await page.EvaluateAsync<string>(BalanceScanScript);
            return JsonSerializer.Deserialize<List<FoundBalance>>(results ?? "[]",
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        }
        finally
        {
            await browser.CloseAsync();
            try { File.Delete(tempSession); } catch { }
        }
    }

    // The main sync method — runs off-screen Chrome with saved session.
    public static async Task<decimal> GetBalanceAsync(Account account)
    {
        if (!HasSession(account.Id))
            throw new InvalidOperationException("No login session saved. Open Settings → Accounts → Setup Login.");

        var meta = LoadMeta(account.Id) ?? throw new InvalidOperationException("No balance page saved.");

        using var pw = await Playwright.CreateAsync();
        var browser = await LaunchAsync(pw);
        var tempSession = DecryptSessionToTemp(SessionPath(account.Id));
        try
        {
            var ctx = await browser.NewContextAsync(new()
            {
                StorageStatePath = tempSession,
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/137.0.0.0 Safari/537.36"
            });
            var page = await ctx.NewPageAsync();

            await page.AddInitScriptAsync(@"
                Object.defineProperty(navigator, 'webdriver',  { get: () => undefined });
                Object.defineProperty(navigator, 'plugins',    { get: () => [1,2,3,4,5] });
                Object.defineProperty(navigator, 'languages',  { get: () => ['en-US','en'] });
                window.chrome = { runtime: {} };
            ");

            await page.GotoAsync(meta.BalanceUrl, new() { WaitUntil = WaitUntilState.Load, Timeout = 60_000 });
            await page.WaitForTimeoutAsync(2500);

            // Check if session expired by seeing if we landed on a different domain.
            // Compare root domain only (e.g. "bankofamerica.com") so subdomain redirects
            // like sitekey.bankofamerica.com don't incorrectly clear the session.
            var currentUrl    = page.Url;
            var expectedDomain = RootDomain(new Uri(meta.BalanceUrl).Host);
            var currentDomain  = RootDomain(new Uri(currentUrl).Host);
            if (currentDomain != expectedDomain)
            {
                ClearSession(account.Id);
                throw new InvalidOperationException("Session expired — re-login required. Open Settings → Accounts → Setup Login.");
            }

            // Scan for dollar amounts; retry once with a longer wait if the page hasn't rendered yet
            List<string> amounts = [];
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (attempt == 1)
                {
                    WriteDebugLog(account.DisplayName, "first scan empty — waiting 4s and retrying");
                    await page.WaitForTimeoutAsync(4000);
                }

                var allAmounts = await page.EvaluateAsync<string>(@"
                    () => {
                        const regex = /^\$[\d,]+\.\d{2}$/;
                        const found = [];
                        const seen = new Set();
                        const skip = new Set(['SCRIPT','STYLE','NOSCRIPT','HEAD','META','LINK']);
                        document.querySelectorAll('*').forEach(el => {
                            if (skip.has(el.tagName)) return;
                            const text = (el.textContent || '').trim();
                            if (!regex.test(text) || seen.has(text)) return;
                            const hasMatchingChild = Array.from(el.children).some(c =>
                                (c.textContent || '').trim() === text);
                            if (hasMatchingChild) return;
                            seen.add(text);
                            found.push(text);
                        });
                        return JSON.stringify(found);
                    }
                ");
                amounts = JsonSerializer.Deserialize<List<string>>(allAmounts ?? "[]") ?? [];
                if (amounts.Count > 0) break;
            }

            if (amounts.Count == 0)
            {
                var shot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "BankSync", $"debug-{account.DisplayName}-{DateTime.Now:HHmmss}.png");
                try { await page.ScreenshotAsync(new() { Path = shot, FullPage = true }); } catch { }
                WriteDebugLog(account.DisplayName, $"no amounts found — screenshot saved to {shot}");
            }

            int idx = account.BalanceSelectorIndex >= 0 ? account.BalanceSelectorIndex : 0;
            string? balanceText = idx < amounts.Count ? amounts[idx] : amounts.FirstOrDefault();

            WriteDebugLog(account.DisplayName, $"idx={idx} picked={balanceText} all=[{string.Join(", ", amounts)}]");

            // Refresh session after successful load (re-encrypt)
            await ctx.StorageStateAsync(new() { Path = SessionPath(account.Id) });
            EncryptSessionFile(SessionPath(account.Id));

            return ParseMoney(balanceText)
                ?? throw new InvalidOperationException("Could not find a balance amount on the page.");
        }
        finally
        {
            await browser.CloseAsync();
            try { File.Delete(tempSession); } catch { }
        }
    }

    private static async Task<IBrowser> LaunchAsync(IPlaywright pw, bool visible = false)
    {
        var args = new List<string>
        {
            "--no-sandbox",
            "--disable-blink-features=AutomationControlled",
        };

        if (!visible)
        {
            // Real rendering engine (avoids headless bot detection), but positioned off any screen
            args.Add("--window-position=-32000,-32000");
            args.Add("--window-size=800,600");
        }

        var pidsBefore = visible ? null : Process.GetProcessesByName("chrome")
                                                  .Select(p => p.Id).ToHashSet();

        var browser = await pw.Chromium.LaunchAsync(new()
        {
            Headless = false,
            Channel  = "chrome",
            Args     = args
        });

        if (!visible && pidsBefore != null)
            _ = HideNewChromeFromTaskbarAsync(pidsBefore); // run in background, don't block page load

        return browser;
    }

    // P/Invoke to strip the Chrome window from the taskbar
    [DllImport("user32.dll")] static extern int  GetWindowLong(IntPtr hwnd, int nIndex);
    [DllImport("user32.dll")] static extern int  SetWindowLong(IntPtr hwnd, int nIndex, int dwNewLong);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint lpdwProcessId);
    delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    private const int  GWL_EXSTYLE      = -20;
    private const int  WS_EX_TOOLWINDOW = 0x00000080;
    private const int  WS_EX_APPWINDOW  = 0x00040000;
    private const uint SWP_NOMOVE       = 0x0002;
    private const uint SWP_NOSIZE       = 0x0001;
    private const uint SWP_NOZORDER     = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const int  SW_HIDE          = 0;

    private static async Task HideNewChromeFromTaskbarAsync(HashSet<int> pidsBefore)
    {
        var hidden = new HashSet<IntPtr>();

        void HideAll()
        {
            var newPids = Process.GetProcessesByName("chrome")
                .Where(p => !pidsBefore.Contains(p.Id))
                .Select(p => (uint)p.Id)
                .ToHashSet();

            EnumWindows((hwnd, _) =>
            {
                if (hidden.Contains(hwnd)) return true;
                GetWindowThreadProcessId(hwnd, out uint pid);
                if (!newPids.Contains(pid)) return true;
                ShowWindow(hwnd, SW_HIDE);
                var style = (GetWindowLong(hwnd, GWL_EXSTYLE) | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;
                SetWindowLong(hwnd, GWL_EXSTYLE, style);
                SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
                hidden.Add(hwnd);
                return true;
            }, IntPtr.Zero);
        }

        // Poll for 5 seconds to catch windows that appear after initial launch
        for (int i = 0; i < 20; i++)
        {
            await Task.Delay(250);
            HideAll();
        }
    }

    private static string RootDomain(string host)
    {
        var parts = host.Split('.');
        return parts.Length >= 2 ? string.Join(".", parts[^2..]) : host;
    }

    private static decimal? ParseMoney(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var clean = System.Text.RegularExpressions.Regex.Replace(text, @"[^0-9.]", "");
        return decimal.TryParse(clean, out var v) ? v : null;
    }

    private static string MetaPath(Guid id) => Path.Combine(SessionDir, $"{id}.meta.json");

    private static ScraperMeta? LoadMeta(Guid id)
    {
        var path = MetaPath(id);
        if (!File.Exists(path)) return null;
        return JsonSerializer.Deserialize<ScraperMeta>(File.ReadAllText(path));
    }

    private class ScraperMeta
    {
        public string BalanceUrl { get; set; } = "";
    }

    private static void WriteDebugLog(string account, string message)
    {
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BankSync", "debug.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {account}: {message}\n");
        }
        catch { }
    }

    public class FoundBalance
    {
        public string Amount { get; set; } = "";
        public string Label  { get; set; } = "";
        public string Path   { get; set; } = "";
    }
}
