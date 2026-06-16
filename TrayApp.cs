using BankSync.Forms;
using BankSync.Helpers;
using BankSync.Models;
using BankSync.Services;
using Microsoft.Win32;

namespace BankSync;

public class TrayApp : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _timer;
    private AppConfig _config;
    private bool _paused;
    private bool _syncing;
    private BalancePopup? _popup;

    private ToolStripMenuItem _pauseItem = null!;
    private ToolStripMenuItem _statusItem = null!;
    private ToolStripMenuItem _errorItem = null!;

    private readonly Dictionary<string, string> _lastErrors = new();
    private readonly Dictionary<Guid, FileSystemWatcher> _watchers = new();
    private readonly SynchronizationContext _uiContext;

    public TrayApp()
    {
        _uiContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _config = AppConfig.Load();
        AppTheme.Apply(_config.Theme);
        ApplyStartupRegistry();

        _tray = new NotifyIcon
        {
            Icon = TrayIcon.Default,
            Visible = true,
            Text = "BankSync"
        };
        _tray.MouseClick += OnTrayClick;
        _tray.ContextMenuStrip = BuildMenu();

        _timer = new System.Windows.Forms.Timer { Interval = 60_000 };
        _timer.Tick += OnTimerTick;
        _timer.Start();

        SetupWatchers();

        if (!_config.WizardCompleted)
        {
            var wizTimer = new System.Windows.Forms.Timer { Interval = 300 };
            wizTimer.Tick += (_, _) => { wizTimer.Stop(); wizTimer.Dispose(); ShowFirstRunWizard(); };
            wizTimer.Start();
        }

        _ = Task.Delay(2000).ContinueWith(_ => SyncAsync(), TaskScheduler.Default);
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Renderer = new ThemeMenuRenderer();

        _statusItem = new ToolStripMenuItem("Last sync: never") { Enabled = false };
        _errorItem  = new ToolStripMenuItem("") { ForeColor = Color.FromArgb(255, 120, 120), Visible = false };
        _errorItem.Click += (_, _) =>
        {
            if (_lastErrors.Count == 0) return;
            var msg = string.Join("\n\n", _lastErrors.Select(kv => $"{kv.Key}:\n{kv.Value}"));
            MessageBox.Show(msg, "Sync Errors", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        };

        var syncNow  = new ToolStripMenuItem("Sync Now", null, (_, _) => _ = SyncAsync());
        _pauseItem   = new ToolStripMenuItem("Pause",    null, OnPauseToggle);
        var settings = new ToolStripMenuItem("Settings", null, OnSettings);
        var exit     = new ToolStripMenuItem("Exit",     null, (_, _) => ExitApp());

        menu.Items.AddRange(new ToolStripItem[]
        {
            _statusItem,
            _errorItem,
            new ToolStripSeparator(),
            syncNow,
            _pauseItem,
            new ToolStripSeparator(),
            settings,
            new ToolStripSeparator(),
            exit
        });

        return menu;
    }

    // --- Tray click: show/hide balance popup ---

    private void OnTrayClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;

        if (_popup != null && !_popup.IsDisposed)
        {
            _popup.Close();
            _popup = null;
            return;
        }

        _config = AppConfig.Load();
        _popup = new BalancePopup(_config, SyncOneAsync);
        _popup.FormClosed += (_, _) => _popup = null;
        _popup.Show();
        _popup.Activate();
    }

    // --- Timer: check each account's own schedule ---

    private async void OnTimerTick(object? sender, EventArgs e)
    {
        UpdateMenuText();
        if (_paused || _syncing) return;

        var accountsDue = _config.Accounts
            .Where(a => a.Enabled && !a.IsWatchFolder && IsAccountDue(a))
            .ToList();

        foreach (var account in _config.Accounts.Where(a => a.Enabled && a.IsScraper && IsAccountDue(a)))
            _ = SyncScraperAccountAsync(account);

        if (accountsDue.Count > 0)
            await SyncAccountsAsync(accountsDue);
    }

    private bool IsAccountDue(Account account)
    {
        if (account.LastBalance == null) return true; // always sync on first run
        var hours = account.RefreshHours == -1 ? _config.RefreshHours : account.RefreshHours;
        if (hours == 0) return false; // manual only after first sync
        return (DateTime.Now - account.LastBalance.AsOf).TotalHours >= hours;
    }

    // --- Sync OFX accounts ---

    private async Task SyncAsync()
    {
        var ofxAccounts     = _config.Accounts.Where(a => a.Enabled && !a.IsWatchFolder && !a.IsScraper).ToList();
        var scraperAccounts = _config.Accounts.Where(a => a.Enabled && a.IsScraper).ToList();

        await SyncAccountsAsync(ofxAccounts);

        foreach (var account in scraperAccounts)
            await SyncScraperAccountAsync(account);

        // Watch-folder accounts that just open browser on demand
        foreach (var account in _config.Accounts.Where(a => a.Enabled && a.OpensBrowser))
            PromptBrowserSync(account);
    }

    private Task SyncOneAsync(Account account)
    {
        if (account.IsScraper) return SyncScraperAccountAsync(account);
        return SyncSingleOFXAsync(account);
    }

    private async Task SyncSingleOFXAsync(Account account)
    {
        var configAccount = _config.Accounts.FirstOrDefault(a => a.Id == account.Id) ?? account;
        try
        {
            configAccount.LastBalance = await OFXService.GetBalanceAsync(account);
            _lastErrors.Remove(account.DisplayName);
            _config.LastSync = DateTime.Now;
            _config.Save();
            await GoogleSheetsService.SyncAllAccountsAsync(_config);
            _tray.Icon = TrayIcon.Default;
            UpdateErrorMenuItem();
        }
        catch (Exception ex)
        {
            _lastErrors[account.DisplayName] = ex.Message;
            WriteErrorLog(account.DisplayName, ex.Message);
            UpdateErrorMenuItem();
        }
        finally
        {
            UpdateMenuText();
            RefreshPopupIfOpen();
        }
    }

    private async Task SyncScraperAccountAsync(Account account)
    {
        if (!ScraperService.HasSession(account.Id))
        {
            _lastErrors[account.DisplayName] = ScraperService.HadSession(account.Id)
                ? "Session expired — re-login needed. Go to Settings → Accounts → Setup Login."
                : "Login not set up. Go to Settings → Accounts → Setup Login.";
            UpdateErrorMenuItem();
            return;
        }

        try
        {
            var balance = await ScraperService.GetBalanceAsync(account);
            var configAccount = _config.Accounts.FirstOrDefault(a => a.Id == account.Id) ?? account;
            configAccount.LastBalance = new Models.Balance
            {
                Ledger    = balance,
                Available = balance,
                AsOf      = DateTime.Now
            };
            _lastErrors.Remove(account.DisplayName);
            _config.LastSync = DateTime.Now;
            _config.Save();
            await GoogleSheetsService.SyncAllAccountsAsync(_config);
            _tray.Icon = TrayIcon.Default;
            UpdateMenuText();
            UpdateErrorMenuItem();
            RefreshPopupIfOpen();
        }
        catch (Exception ex)
        {
            _lastErrors[account.DisplayName] = ex.Message;
            WriteErrorLog(account.DisplayName, ex.Message);
            UpdateErrorMenuItem();
        }
    }

    private void PromptBrowserSync(Account account)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(account.LoginUrl) { UseShellExecute = true }); }
        catch { }

        _tray.BalloonTipTitle = $"Action needed — {account.DisplayName}";
        _tray.BalloonTipText  = $"Log in to {account.Institution}, then download transactions as QFX (Quicken) format. BankSync will pick up the file automatically.";
        _tray.BalloonTipIcon  = ToolTipIcon.Info;
        _tray.ShowBalloonTip(8000);
    }

    private async Task SyncAccountsAsync(List<Account> accounts)
    {
        if (_paused || _syncing || accounts.Count == 0) return;
        _syncing = true;
        _tray.Icon = TrayIcon.Syncing;
        _tray.Text = "BankSync — syncing…";

        try
        {
            _config = AppConfig.Load();
            bool anyError = false;

            foreach (var account in accounts)
            {
                var configAccount = _config.Accounts.FirstOrDefault(a => a.Id == account.Id) ?? account;
                try
                {
                    configAccount.LastBalance = await OFXService.GetBalanceAsync(account);
                    _lastErrors.Remove(account.DisplayName);
                }
                catch (Exception ex)
                {
                    anyError = true;
                    _lastErrors[account.DisplayName] = ex.Message;
                    WriteErrorLog(account.DisplayName, ex.Message);
                }
            }

            _config.LastSync = DateTime.Now;
            _config.Save();

            await GoogleSheetsService.SyncAllAccountsAsync(_config);

            _tray.Icon = anyError ? TrayIcon.Error : TrayIcon.Default;
            _tray.Text = anyError ? "BankSync — some accounts failed" : "BankSync";
            UpdateErrorMenuItem();
        }
        catch
        {
            _tray.Icon = TrayIcon.Error;
            _tray.Text = "BankSync — sync failed";
        }
        finally
        {
            _syncing = false;
            UpdateMenuText();
            RefreshPopupIfOpen();
        }
    }

    // --- Watch folder: auto-import .qfx files ---

    private void SetupWatchers()
    {
        // Tear down old watchers
        foreach (var w in _watchers.Values) { w.EnableRaisingEvents = false; w.Dispose(); }
        _watchers.Clear();

        foreach (var account in _config.Accounts.Where(a => a.Enabled && a.IsWatchFolder))
        {
            if (!Directory.Exists(account.WatchFolderPath)) continue;

            Directory.CreateDirectory(account.WatchFolderPath);

            var watcher = new FileSystemWatcher(account.WatchFolderPath, "*.qfx")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };

            var accountId = account.Id;
            watcher.Created += (_, fe) => OnQfxFileDetected(accountId, fe.FullPath);
            watcher.Changed += (_, fe) => OnQfxFileDetected(accountId, fe.FullPath);

            _watchers[accountId] = watcher;
        }
    }

    private void OnQfxFileDetected(Guid accountId, string filePath)
    {
        // Small delay to ensure the file is fully written
        Thread.Sleep(800);

        try
        {
            var account = _config.Accounts.FirstOrDefault(a => a.Id == accountId);
            if (account == null) return;

            account.LastBalance = OFXService.ParseQfxFile(filePath);
            _lastErrors.Remove(account.DisplayName);
            _config.LastSync = DateTime.Now;
            _config.Save();

            // Delete after successful parse so financial data doesn't sit on disk
            try { File.Delete(filePath); } catch { }

            _uiContext.Post(_ =>
            {
                _tray.Icon = TrayIcon.Default;
                _tray.Text = "BankSync";
                UpdateMenuText();
                UpdateErrorMenuItem();
                RefreshPopupIfOpen();
            }, null);

            _ = GoogleSheetsService.SyncAllAccountsAsync(_config);
        }
        catch (Exception ex)
        {
            var account = _config.Accounts.FirstOrDefault(a => a.Id == accountId);
            if (account != null)
            {
                _lastErrors[account.DisplayName] = $"QFX parse error: {ex.Message}";
                WriteErrorLog(account.DisplayName, ex.Message);
            }
        }
    }

    // --- Settings ---

    private void OnPauseToggle(object? sender, EventArgs e)
    {
        _paused = !_paused;
        _pauseItem.Text = _paused ? "Resume" : "Pause";
        _tray.Icon = _paused ? TrayIcon.Paused : TrayIcon.Default;
        _tray.Text = _paused ? "BankSync — paused" : "BankSync";
    }

    private void ShowFirstRunWizard()
    {
        using var wiz = new FirstRunWizard(_config);
        wiz.ShowDialog();
        _config = wiz.UpdatedConfig;
        _config.WizardCompleted = true;
        _config.Save();
        if (wiz.ShouldOpenSettings)
            OnSettings(null, EventArgs.Empty);
    }

    private void OnSettings(object? sender, EventArgs e)
    {
        _config = AppConfig.Load();
        using var form = new SettingsForm(_config);
        if (form.ShowDialog() == DialogResult.OK)
        {
            _config = form.UpdatedConfig;
            AppTheme.Apply(_config.Theme);
            _config.Save();
            ApplyStartupRegistry();
            SetupWatchers();
            _tray.ContextMenuStrip = BuildMenu(); // rebuild menu with updated theme
        }
    }

    // --- Helpers ---

    private void UpdateMenuText()
    {
        if (_statusItem.IsDisposed) return;
        _statusItem.Text = _config.LastSync.HasValue
            ? $"Last sync: {FormatAgo(_config.LastSync.Value)}"
            : "Last sync: never";
    }

    private void UpdateErrorMenuItem()
    {
        if (_errorItem.IsDisposed) return;
        if (_lastErrors.Count == 0) { _errorItem.Visible = false; return; }
        var first = _lastErrors.First();
        var msg   = first.Value.Split('\n')[0]; // first line only
        if (msg.Length > 60) msg = msg[..60] + "…";
        _errorItem.Text    = $"⚠ {first.Key}: {msg}";
        _errorItem.Visible = true;
    }

    private void RefreshPopupIfOpen()
    {
        if (_popup == null || _popup.IsDisposed) return;
        _popup.Refresh();
    }

    private void ApplyStartupRegistry()
    {
        const string runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        using var key = Registry.CurrentUser.OpenSubKey(runKey, writable: true)!;
        if (_config.LaunchAtStartup)
            key.SetValue("BankSync", $"\"{Application.ExecutablePath}\"");
        else
            key.DeleteValue("BankSync", throwOnMissingValue: false);
    }

    private void ExitApp()
    {
        _timer.Stop();
        foreach (var w in _watchers.Values) w.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        Application.Exit();
    }

    private static void WriteErrorLog(string account, string message)
    {
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BankSync", "error.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {account}: {message}\n");
        }
        catch { }
    }

    private static string FormatAgo(DateTime dt)
    {
        var ago = DateTime.Now - dt;
        if (ago.TotalMinutes < 2) return "just now";
        if (ago.TotalMinutes < 60) return $"{(int)ago.TotalMinutes}m ago";
        if (ago.TotalHours < 24) return $"{(int)ago.TotalHours}h ago";
        return $"{(int)ago.TotalDays}d ago";
    }
}

internal class ThemeMenuRenderer : ToolStripProfessionalRenderer
{
    public ThemeMenuRenderer() : base(new ThemeColorTable()) { }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? AppTheme.MenuText : AppTheme.MenuTextDim;
        base.OnRenderItemText(e);
    }
}

internal class ThemeColorTable : ProfessionalColorTable
{
    public override Color MenuBorder                    => AppTheme.MenuBorder;
    public override Color MenuItemBorder                => AppTheme.MenuBorder;
    public override Color MenuItemSelected              => AppTheme.MenuSelected;
    public override Color MenuItemSelectedGradientBegin => AppTheme.MenuSelected;
    public override Color MenuItemSelectedGradientEnd   => AppTheme.MenuSelected;
    public override Color ToolStripDropDownBackground   => AppTheme.MenuBack;
    public override Color ImageMarginGradientBegin      => AppTheme.MenuBack;
    public override Color ImageMarginGradientMiddle     => AppTheme.MenuBack;
    public override Color ImageMarginGradientEnd        => AppTheme.MenuBack;
    public override Color SeparatorDark                 => AppTheme.MenuSep;
    public override Color SeparatorLight                => AppTheme.MenuSep;
}
