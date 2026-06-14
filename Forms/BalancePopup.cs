using BankSync.Models;

namespace BankSync.Forms;

public class BalancePopup : Form
{
    private AppConfig _config;
    private readonly Func<Account, Task>? _syncOne;
    private Panel _panel = null!;
    private FlowLayoutPanel _layout = null!;
    private int _activeSyncs;

    private static readonly string[] SpinFrames = ["↺", "↻"];
    private static readonly Color BlueA = Color.FromArgb(60, 150, 255);
    private static readonly Color BlueB = Color.FromArgb(130, 200, 255);
    private static readonly Color IdleGray = Color.FromArgb(120, 120, 120);

    public BalancePopup(AppConfig config, Func<Account, Task>? syncOne = null)
    {
        _config  = config;
        _syncOne = syncOne;
        InitializeComponent();
        BuildContent();
        PositionNearTray();
    }

    private void InitializeComponent()
    {
        Text = "BankSync";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(30, 30, 30);
        Padding = new Padding(1);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        StartPosition = FormStartPosition.Manual;
        Deactivate += (s, e) => { if (_activeSyncs == 0) BeginInvoke(Close); };
    }

    private void BuildContent()
    {
        _panel = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.FromArgb(40, 40, 40),
            Padding = new Padding(16, 12, 16, 12),
            Margin = new Padding(1)
        };

        _layout = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
        };

        PopulateLayout();
        _panel.Controls.Add(_layout);
        Controls.Add(_panel);
    }

    private void PopulateLayout()
    {
        _layout.Controls.Clear();

        var enabledAccounts = _config.Accounts.Where(a => a.Enabled).ToList();

        if (enabledAccounts.Count == 0)
        {
            _layout.Controls.Add(MakeLabel("No accounts configured.", Color.Gray, 11));
        }
        else
        {
            bool first = true;
            foreach (var account in enabledAccounts)
            {
                if (!first) _layout.Controls.Add(MakeSeparator());
                first = false;

                // Create the timestamp label first so MakeAccountHeader can reference it
                Label? timestampLbl = null;
                if (account.LastBalance != null)
                    timestampLbl = MakeLabel($"  Updated {FormatAgo(account.LastBalance.AsOf)}",
                                             Color.FromArgb(100, 100, 100), 8);

                _layout.Controls.Add(MakeAccountHeader(account, timestampLbl));

                if (account.LastBalance == null)
                {
                    _layout.Controls.Add(MakeLabel("  Not yet synced", Color.Gray, 10));
                }
                else
                {
                    var bal = account.LastBalance;
                    _layout.Controls.Add(MakeRow("Balance:", FormatMoney(bal.Ledger), Color.FromArgb(100, 220, 100)));
                    _layout.Controls.Add(MakeRow("Available:", FormatMoney(bal.Available), Color.White));
                    _layout.Controls.Add(timestampLbl!);

                    if (Math.Abs(bal.Pending) > 0)
                        _layout.Controls.Add(MakeRow("Pending:", FormatMoney(bal.Pending), Color.FromArgb(255, 180, 80)));

                    var pending = bal.RecentTransactions.Where(t => t.IsPending).ToList();
                    var recent  = bal.RecentTransactions.Where(t => !t.IsPending).Take(5).ToList();

                    if (pending.Count > 0)
                    {
                        _layout.Controls.Add(MakeLabel("  Pending:", Color.FromArgb(255, 180, 80), 10));
                        foreach (var tx in pending) _layout.Controls.Add(MakeTxRow(tx));
                    }
                    if (recent.Count > 0)
                    {
                        _layout.Controls.Add(MakeLabel("  Recent:", Color.Gray, 10));
                        foreach (var tx in recent) _layout.Controls.Add(MakeTxRow(tx));
                    }
                }
            }
        }

        _layout.Controls.Add(MakeSeparator());
        _layout.Controls.Add(MakeLabel(
            _config.LastSync.HasValue ? $"Synced {FormatAgo(_config.LastSync.Value)}" : "Never synced",
            Color.Gray, 9));

        if (!string.IsNullOrWhiteSpace(_config.SpreadsheetId))
        {
            var sheetUrl = $"https://docs.google.com/spreadsheets/d/{_config.SpreadsheetId}/edit";
            var link = MakeLabel("Open spreadsheet ↗", Color.FromArgb(100, 150, 220), 9);
            link.Cursor = Cursors.Hand;
            link.Margin = new Padding(0, 2, 0, 0);
            link.Click += (_, _) =>
            {
                Close();
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(sheetUrl) { UseShellExecute = true }); } catch { }
            };
            _layout.Controls.Add(link);
        }
    }

    public new void Refresh()
    {
        if (IsDisposed) return;
        if (InvokeRequired) { Invoke(Refresh); return; }
        if (_activeSyncs > 0) return; // don't rebuild while animation is running
        _config = AppConfig.Load();
        PopulateLayout();
        PositionNearTray();
    }

    private void RefreshContent() => Refresh();

    private Control MakeAccountHeader(Account account, Label? timestampLbl)
    {
        var row = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 2, 0, 2),
            WrapContents = false
        };

        var nameLink = MakeLabel(account.DisplayName, Color.White, 12, bold: true);
        if (!string.IsNullOrWhiteSpace(account.LoginUrl))
        {
            var url = account.LoginUrl;
            nameLink.Cursor = Cursors.Hand;
            nameLink.ForeColor = Color.FromArgb(130, 190, 255);
            nameLink.Click += (_, _) =>
            {
                Close();
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
            };
        }
        row.Controls.Add(nameLink);

        if (_syncOne != null)
        {
            var syncLbl = new Label
            {
                Text      = "↺",
                ForeColor = IdleGray,
                Font      = new Font("Segoe UI", 11),
                AutoSize  = true,
                Cursor    = Cursors.Hand,
                Margin    = new Padding(6, 2, 0, 0)
            };

            bool spinning = false;
            syncLbl.Click += async (_, _) =>
            {
                if (spinning) return;
                spinning = true;
                _activeSyncs++;

                // Immediate feedback before the first timer tick
                syncLbl.ForeColor = BlueA;
                syncLbl.Refresh();
                if (timestampLbl != null && !timestampLbl.IsDisposed)
                {
                    timestampLbl.Text      = "  Updating...";
                    timestampLbl.ForeColor = BlueA;
                    timestampLbl.Refresh();
                }

                int elapsed = 0;
                int frame   = 0;
                var timer = new System.Windows.Forms.Timer { Interval = 300 };
                timer.Tick += (_, _) =>
                {
                    if (syncLbl.IsDisposed) { timer.Stop(); return; }

                    syncLbl.Text      = SpinFrames[frame % SpinFrames.Length];
                    syncLbl.ForeColor = frame % 2 == 0 ? BlueA : BlueB;
                    syncLbl.Refresh();

                    elapsed += 300;
                    if (timestampLbl != null && !timestampLbl.IsDisposed)
                    {
                        int secs = elapsed / 1000;
                        timestampLbl.Text      = secs > 0 ? $"  Updating... {secs}s" : "  Updating...";
                        timestampLbl.ForeColor = frame % 2 == 0 ? BlueA : BlueB;
                        timestampLbl.Refresh();
                    }

                    frame++;
                };
                timer.Start();

                try   { await _syncOne(account); }
                catch { }
                finally
                {
                    timer.Stop();
                    timer.Dispose();
                    _activeSyncs--;
                    spinning = false;
                    if (!syncLbl.IsDisposed)
                    {
                        syncLbl.Text      = "↺";
                        syncLbl.ForeColor = IdleGray;
                    }
                    if (!IsDisposed) RefreshContent();
                }
            };

            row.Controls.Add(syncLbl);
        }

        return row;
    }

    private static Label MakeLabel(string text, Color color, float size, bool bold = false) => new Label
    {
        Text      = text,
        ForeColor = color,
        Font      = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
        AutoSize  = true,
        Margin    = new Padding(0, 2, 0, 2)
    };

    private static Control MakeRow(string label, string value, Color valueColor)
    {
        var row = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Margin = new Padding(0, 1, 0, 1) };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.Controls.Add(MakeLabel("  " + label, Color.FromArgb(170, 170, 170), 10), 0, 0);
        row.Controls.Add(MakeLabel(value, valueColor, 10, bold: true), 1, 0);
        return row;
    }

    private static Control MakeTxRow(Transaction tx)
    {
        var name  = tx.Name.Length > 22 ? tx.Name[..22] + "…" : tx.Name;
        var color = tx.Amount < 0 ? Color.FromArgb(255, 120, 120) : Color.FromArgb(100, 220, 100);
        var row   = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Margin = new Padding(0, 0, 0, 0) };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        row.Controls.Add(MakeLabel("    " + name, Color.FromArgb(200, 200, 200), 9), 0, 0);
        row.Controls.Add(MakeLabel(FormatMoney(tx.Amount), color, 9), 1, 0);
        return row;
    }

    private static Panel MakeSeparator() => new Panel
    {
        Height = 1, Width = 260,
        BackColor = Color.FromArgb(70, 70, 70),
        Margin = new Padding(0, 6, 0, 6)
    };

    private static string FormatMoney(decimal v) => v < 0 ? $"-${Math.Abs(v):N2}" : $"${v:N2}";

    private static string FormatAgo(DateTime dt)
    {
        var ago = DateTime.Now - dt;
        if (ago.TotalMinutes < 2)  return "just now";
        if (ago.TotalMinutes < 60) return $"{(int)ago.TotalMinutes}m ago";
        if (ago.TotalHours   < 24) return $"{(int)ago.TotalHours}h ago";
        return $"{(int)ago.TotalDays}d ago";
    }

    private void PositionNearTray()
    {
        var screen = Screen.PrimaryScreen!.WorkingArea;
        var size   = PreferredSize;
        Location = new Point(screen.Right - size.Width - 12, screen.Bottom - size.Height - 12);
    }
}
