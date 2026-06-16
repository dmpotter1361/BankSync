using BankSync.Helpers;
using BankSync.Models;
using BankSync.Services;

namespace BankSync.Forms;

public class FirstRunWizard : Form
{
    private readonly AppConfig _config;
    private Panel   _pageWelcome = null!;
    private Panel   _pageSheets  = null!;
    private Panel   _pageDone    = null!;
    private Panel[] _pages       = null!;
    private int     _currentPage = 0;

    private TextBox _sheetUrlBox = null!;
    private Label   _authStatus  = null!;
    private Button  _connectBtn  = null!;
    private Button  _nextBtn     = null!;
    private Button  _backBtn     = null!;

    public AppConfig UpdatedConfig      => _config;
    public bool      ShouldOpenSettings { get; private set; }

    public FirstRunWizard(AppConfig config)
    {
        _config = config;
        InitializeComponent();
        GoToPage(0);
    }

    private void InitializeComponent()
    {
        Text            = "BankSync Setup";
        Icon            = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        ClientSize      = new Size(440, 380);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterScreen;
        BackColor       = AppTheme.FormBack;
        ForeColor       = AppTheme.TextPrimary;
        Font            = new Font("Segoe UI", 9);

        _pageWelcome = BuildWelcomePage();
        _pageSheets  = BuildSheetsPage();
        _pageDone    = BuildDonePage();
        _pages       = [_pageWelcome, _pageSheets, _pageDone];

        foreach (var p in _pages)
        {
            p.Location = new Point(0, 0);
            p.Size     = new Size(440, 320);
            p.Visible  = false;
            Controls.Add(p);
        }

        var nav = new Panel
        {
            Location  = new Point(0, 320),
            Size      = new Size(440, 60),
            BackColor = AppTheme.NavBack
        };

        _backBtn = new Button { Text = "← Back", Location = new Point(12, 16),  Width = 90 };
        _nextBtn = new Button { Text = "Next →",  Location = new Point(338, 16), Width = 90 };
        StyleButton(_backBtn, AppTheme.BtnGray);
        StyleButton(_nextBtn, AppTheme.BtnGreen);
        _backBtn.Click += (_, _) => GoToPage(_currentPage - 1);
        _nextBtn.Click += OnNext;

        nav.Controls.AddRange(new Control[] { _backBtn, _nextBtn });
        Controls.Add(nav);
        DwmHelper.RoundCorners(this);
    }

    private Panel BuildWelcomePage()
    {
        var p = MakePage();
        AddLabel(p, "Welcome to BankSync", AppTheme.TextPrimary, 15, bold: true, y: 36);
        AddLabel(p,
            "BankSync tracks your bank balances automatically\n" +
            "and keeps them synced with a Google Spreadsheet.",
            AppTheme.TextSecondary, 10, y: 90);
        AddLabel(p,
            "This wizard will help you connect your spreadsheet.\n" +
            "You'll add bank accounts afterward in Settings.",
            AppTheme.TextMuted, 10, y: 158);
        AddLabel(p, "Click Next to get started →", AppTheme.TextDisabled, 9, y: 260);
        return p;
    }

    private Panel BuildSheetsPage()
    {
        var p = MakePage();
        AddLabel(p, "Google Sheets Sync", AppTheme.TextPrimary, 13, bold: true, y: 28);
        AddLabel(p,
            "BankSync can update a Google Spreadsheet with\n" +
            "your latest balances after each sync.",
            AppTheme.TextSecondary, 10, y: 70);
        AddLabel(p, "Spreadsheet URL or ID:", AppTheme.TextMuted, 9, y: 126);

        _sheetUrlBox = new TextBox
        {
            Location    = new Point(28, 144),
            Width       = 384,
            BackColor   = AppTheme.InputBack,
            ForeColor   = AppTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle
        };

        _connectBtn = new Button { Text = "Connect Google Account", Location = new Point(28, 184), Width = 190 };
        StyleButton(_connectBtn, AppTheme.BtnBlue);
        _connectBtn.Click += OnConnectGoogle;

        _authStatus = new Label
        {
            Text      = "Not connected  —  you can skip this for now",
            ForeColor = AppTheme.TextDisabled,
            AutoSize  = true,
            Location  = new Point(28, 222)
        };

        p.Controls.AddRange(new Control[] { _sheetUrlBox, _connectBtn, _authStatus });
        return p;
    }

    private Panel BuildDonePage()
    {
        var p = MakePage();
        AddLabel(p, "You're all set!", AppTheme.Positive, 15, bold: true, y: 44);
        AddLabel(p, "BankSync is running in your system tray.", AppTheme.TextSecondary, 10, y: 104);
        AddLabel(p,
            "To add your bank accounts:\n" +
            "Right-click the tray icon → Settings → Accounts → Add",
            AppTheme.TextSecondary, 10, y: 148);

        var openBtn = new Button { Text = "Open Settings Now", Location = new Point(28, 218), Width = 160 };
        StyleButton(openBtn, AppTheme.BtnBlue);
        openBtn.Click += (_, _) => { ShouldOpenSettings = true; DialogResult = DialogResult.OK; Close(); };
        p.Controls.Add(openBtn);
        return p;
    }

    private async void OnConnectGoogle(object? sender, EventArgs e)
    {
        _connectBtn.Enabled   = false;
        _authStatus.Text      = "Opening browser for Google sign-in…";
        _authStatus.ForeColor = AppTheme.Warning;
        try
        {
            await GoogleSheetsService.GetServiceAsync(_config);
            _authStatus.Text      = "✓ Google account connected";
            _authStatus.ForeColor = AppTheme.Positive;
        }
        catch (Exception ex)
        {
            var msg = ex.Message.Length > 60 ? ex.Message[..60] + "…" : ex.Message;
            _authStatus.Text      = $"Failed: {msg}";
            _authStatus.ForeColor = AppTheme.Negative;
            _connectBtn.Enabled   = true;
        }
    }

    private void OnNext(object? sender, EventArgs e)
    {
        if (_currentPage == 1)
            _config.SpreadsheetId = AppConfig.ExtractSheetId(_sheetUrlBox.Text.Trim());

        if (_currentPage == _pages.Length - 1)
        {
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        GoToPage(_currentPage + 1);
    }

    private void GoToPage(int idx)
    {
        if (idx < 0 || idx >= _pages.Length) return;
        _pages[_currentPage].Visible = false;
        _currentPage = idx;
        _pages[_currentPage].Visible = true;

        _backBtn.Visible = _currentPage > 0;
        _nextBtn.Text    = _currentPage == _pages.Length - 1 ? "Finish" : "Next →";

        if (_currentPage == 1)
        {
            _sheetUrlBox.Text = _config.SpreadsheetId;
            if (GoogleSheetsService.HasToken)
            {
                _authStatus.Text      = "✓ Google account connected";
                _authStatus.ForeColor = AppTheme.Positive;
            }
        }
    }

    private Panel MakePage() => new Panel { BackColor = AppTheme.FormBack };

    private static void AddLabel(Panel p, string text, Color color, float size, bool bold = false, int y = 0)
    {
        p.Controls.Add(new Label
        {
            Text      = text,
            ForeColor = color,
            Font      = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
            AutoSize  = true,
            Location  = new Point(28, y)
        });
    }

    private static void StyleButton(Button btn, Color bg)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.BackColor = bg;
        btn.ForeColor = Color.White;
        btn.FlatAppearance.BorderSize = 0;
        btn.Height = 28;
    }
}
