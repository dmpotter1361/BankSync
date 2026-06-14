using BankSync.Models;
using BankSync.Services;
using Microsoft.Playwright;

namespace BankSync.Forms;

public class BrowserSetupForm : Form
{
    private readonly Account _account;

    private Label _stepLabel   = null!;
    private Label _statusLabel = null!;
    private Button _actionBtn  = null!;
    private Button _cancelBtn  = null!;
    private ListBox _balanceList = null!;
    private Label _pickLabel   = null!;

    private IPlaywright? _pw;
    private IBrowser?    _browser;
    private IBrowserContext? _ctx;
    private IPage?       _page;

    private enum Step { NotStarted, BrowserOpen, PickBalance, Done }
    private Step _step = Step.NotStarted;
    private List<ScraperService.FoundBalance> _foundBalances = [];

    public BrowserSetupForm(Account account)
    {
        _account = account;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = $"Setup Login — {_account.DisplayName}";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        ClientSize = new Size(460, 420);   // client area, excludes title bar
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(35, 35, 35);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9);
        FormClosing += OnClosing;

        _stepLabel = new Label
        {
            Text = "Log in normally — including any 2FA codes.\nTip: check \"Remember me\" or \"Stay signed in\" if offered —\nthis keeps your session alive between syncs.\n\nThen navigate to your account balance page and click\n\"I can see my balance\".",
            Location = new Point(20, 20),
            Size = new Size(420, 105),
            ForeColor = Color.FromArgb(200, 200, 200)
        };

        _statusLabel = new Label
        {
            Text = "",
            Location = new Point(20, 132),
            Size = new Size(420, 24),
            ForeColor = Color.FromArgb(100, 200, 100)
        };

        _pickLabel = new Label
        {
            Text = "Which amount is your account balance?",
            Location = new Point(20, 162),
            Size = new Size(420, 20),
            ForeColor = Color.FromArgb(200, 200, 200),
            Visible = false
        };

        _balanceList = new ListBox
        {
            Location = new Point(20, 186),
            Size = new Size(420, 148),
            BackColor = Color.FromArgb(50, 50, 50),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Visible = false
        };
        _balanceList.DoubleClick += (_, _) => SaveSelectedBalance();

        _actionBtn = new Button
        {
            Text = "Open Chrome",
            Location = new Point(192, 372),
            Width = 160
        };
        StyleButton(_actionBtn, Color.FromArgb(30, 140, 70));
        _actionBtn.Click += OnActionClick;

        _cancelBtn = new Button
        {
            Text = "Cancel",
            Location = new Point(362, 372),
            Width = 80,
            DialogResult = DialogResult.Cancel
        };
        StyleButton(_cancelBtn, Color.FromArgb(70, 70, 70));

        Controls.AddRange(new Control[] { _stepLabel, _statusLabel, _pickLabel, _balanceList, _actionBtn, _cancelBtn });
        CancelButton = _cancelBtn;
    }

    private async void OnActionClick(object? sender, EventArgs e)
    {
        switch (_step)
        {
            case Step.NotStarted:
                await OpenBrowserAsync();
                break;
            case Step.BrowserOpen:
                await CapturSessionAsync();
                break;
            case Step.PickBalance:
                SaveSelectedBalance();
                break;
        }
    }

    private async Task OpenBrowserAsync()
    {
        _actionBtn.Enabled = false;
        _statusLabel.Text  = "Opening Chrome…";

        try
        {
            (_pw, _browser, _ctx, _page) = await ScraperService.LaunchLoginBrowserAsync(_account.LoginUrl);
            _step              = Step.BrowserOpen;
            _actionBtn.Text    = "I can see my balance";
            _actionBtn.Enabled = true;
            _statusLabel.Text  = "Log in to your bank. When you can see your balance, click the button →";
        }
        catch (Exception ex)
        {
            _statusLabel.Text      = $"Error: {ex.Message}";
            _statusLabel.ForeColor = Color.FromArgb(255, 100, 100);
            _actionBtn.Enabled     = true;
        }
    }

    private async Task CapturSessionAsync()
    {
        _actionBtn.Enabled = false;
        _statusLabel.Text  = "Saving your session…";

        try
        {
            var balanceUrl = _page!.Url;
            await ScraperService.SaveSessionAsync(_ctx!, _account.Id, balanceUrl);

            _statusLabel.Text = "Finding balance amounts on page…";
            var found = await ScraperService.FindBalancesOnCurrentPageAsync(_page!);

            if (found.Count == 0)
            {
                _statusLabel.Text      = "No dollar amounts found. Navigate to your account balance page first.";
                _statusLabel.ForeColor = Color.FromArgb(255, 180, 80);
                _actionBtn.Text        = "I can see my balance";
                _actionBtn.Enabled     = true;
                return;
            }

            // Show picker
            _step            = Step.PickBalance;
            _pickLabel.Visible = true;
            _balanceList.Visible = true;
            _stepLabel.Visible = false;
            _statusLabel.Text  = "";

            _foundBalances = found;
            _balanceList.Items.Clear();
            foreach (var b in found)
                _balanceList.Items.Add($"{b.Amount}  —  {b.Label.Trim()}");

            _balanceList.SelectedIndex = 0;
            _actionBtn.Text    = "This is my balance";
            _actionBtn.Enabled = true;
        }
        catch (Exception ex)
        {
            _statusLabel.Text      = $"Error: {ex.Message}";
            _statusLabel.ForeColor = Color.FromArgb(255, 100, 100);
            _actionBtn.Enabled     = true;
        }
    }

    private void SaveSelectedBalance()
    {
        if (_balanceList.SelectedIndex < 0) return;

        _actionBtn.Enabled = false;
        _statusLabel.Text  = "Confirming…";

        try
        {
            if (_balanceList.SelectedIndex < _foundBalances.Count)
            {
                _account.BalanceSelectorIndex = _balanceList.SelectedIndex;
                _account.BalanceSelector      = _foundBalances[_balanceList.SelectedIndex].Path;
            }

            _step              = Step.Done;
            _pickLabel.Visible = false;
            _balanceList.Visible = false;
            _statusLabel.Text  = "Done! BankSync will sync this balance automatically.";
            _statusLabel.ForeColor = Color.FromArgb(100, 220, 100);
            _actionBtn.Text    = "Close";
            _actionBtn.Enabled = true;
            _actionBtn.Click  -= OnActionClick;
            _actionBtn.Click  += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        }
        catch (Exception ex)
        {
            _statusLabel.Text      = $"Error: {ex.Message}";
            _statusLabel.ForeColor = Color.FromArgb(255, 100, 100);
            _actionBtn.Enabled     = true;
        }
    }

    private async void OnClosing(object? sender, FormClosingEventArgs e)
    {
        try
        {
            if (_browser != null) await _browser.CloseAsync();
            _pw?.Dispose();
        }
        catch { }
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
