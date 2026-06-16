using BankSync.Helpers;
using BankSync.Models;
using BankSync.Services;

namespace BankSync.Forms;

public class SettingsForm : Form
{
    private readonly AppConfig _original;
    public AppConfig UpdatedConfig { get; private set; }

    private static readonly bool HasBundledCredentials =
        File.Exists(Path.Combine(AppContext.BaseDirectory, "credentials.json"));

    // Controls
    private ListView       _accountList     = null!;
    private Button         _addBtn          = null!;
    private Button         _editBtn         = null!;
    private Button         _removeBtn       = null!;
    private TextBox        _sheetUrlBox     = null!;
    private TextBox        _clientIdBox     = null!;
    private TextBox        _clientSecretBox = null!;
    private NumericUpDown  _refreshSpinner  = null!;
    private CheckBox       _startupCheck    = null!;
    private ComboBox       _themeCombo      = null!;
    private Button         _sheetAuthBtn    = null!;
    private Button         _sheetResetBtn   = null!;
    private Label          _sheetStatusLabel = null!;

    public SettingsForm(AppConfig config)
    {
        _original     = config;
        UpdatedConfig = config;
        InitializeComponent();
        PopulateFields();
    }

    private void InitializeComponent()
    {
        Text            = "BankSync Settings";
        Icon            = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Size            = new Size(560, 500);
        MinimumSize     = new Size(560, 500);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        StartPosition   = FormStartPosition.CenterScreen;
        BackColor       = AppTheme.FormBack;
        ForeColor       = AppTheme.TextPrimary;
        Font            = new Font("Segoe UI", 9);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        StyleTabControl(tabs);

        tabs.TabPages.Add(BuildAccountsTab());
        tabs.TabPages.Add(BuildSheetsTab());
        tabs.TabPages.Add(BuildGeneralTab());

        var ok     = new Button { Text = "Save",   DialogResult = DialogResult.OK,     Width = 80 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80 };
        StyleButton(ok,     AppTheme.BtnGreen);
        StyleButton(cancel, AppTheme.BtnGray);
        ok.Click += OnSave;

        var btnPanel = new FlowLayoutPanel
        {
            Dock          = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height        = 40,
            Padding       = new Padding(4),
            BackColor     = AppTheme.NavBack
        };
        btnPanel.Controls.Add(cancel);
        btnPanel.Controls.Add(ok);

        Controls.Add(tabs);
        Controls.Add(btnPanel);
        AcceptButton = ok;
        CancelButton = cancel;
        DwmHelper.RoundCorners(this);
    }

    private TabPage BuildAccountsTab()
    {
        var tab = new TabPage("Accounts") { BackColor = AppTheme.FormBack, ForeColor = AppTheme.TextPrimary };

        _accountList = new ListView
        {
            View          = View.Details,
            FullRowSelect = true,
            GridLines     = false,
            HeaderStyle   = ColumnHeaderStyle.Nonclickable,
            BackColor     = AppTheme.ListBack,
            ForeColor     = AppTheme.TextPrimary,
            BorderStyle   = BorderStyle.FixedSingle,
            Location      = new Point(12, 12),
            Size          = new Size(516, 280)
        };
        _accountList.Columns.Add("Account", 180);
        _accountList.Columns.Add("Bank",    130);
        _accountList.Columns.Add("Cell",     60);
        _accountList.Columns.Add("Enabled",  60);
        _accountList.SelectedIndexChanged += (_, _) => UpdateAccountButtons();

        _addBtn    = new Button { Text = "Add",    Location = new Point(12,  302), Width = 80 };
        _editBtn   = new Button { Text = "Edit",   Location = new Point(100, 302), Width = 80 };
        _removeBtn = new Button { Text = "Remove", Location = new Point(188, 302), Width = 80 };

        StyleButton(_addBtn,    AppTheme.BtnGreen);
        StyleButton(_editBtn,   AppTheme.BtnBlue);
        StyleButton(_removeBtn, AppTheme.BtnRed);

        _addBtn.Click    += OnAddAccount;
        _editBtn.Click   += OnEditAccount;
        _removeBtn.Click += OnRemoveAccount;

        tab.Controls.AddRange(new Control[] { _accountList, _addBtn, _editBtn, _removeBtn });
        return tab;
    }

    private TabPage BuildSheetsTab()
    {
        var tab = new TabPage("Google Sheets") { BackColor = AppTheme.FormBack, ForeColor = AppTheme.TextPrimary };

        var note = new Label
        {
            Text      = "Optional — leave blank to skip Google Sheets sync.",
            ForeColor = AppTheme.TextDisabled,
            AutoSize  = true,
            Location  = new Point(12, 12)
        };

        var urlLabel = new Label { Text = "Spreadsheet URL or ID:", AutoSize = true, Location = new Point(12, 40), ForeColor = AppTheme.TextPrimary };
        _sheetUrlBox = new TextBox
        {
            Location    = new Point(12, 58),
            Width       = 516,
            BackColor   = AppTheme.InputBack,
            ForeColor   = AppTheme.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle
        };

        var extraControls = new List<Control>();
        int credBottom;

        if (HasBundledCredentials)
        {
            var bundledNote = new Label
            {
                Text      = "Google OAuth credentials: bundled with this app — no setup needed.",
                ForeColor = AppTheme.Positive,
                AutoSize  = true,
                Location  = new Point(12, 96)
            };
            _clientIdBox     = new TextBox { Visible = false };
            _clientSecretBox = new TextBox { Visible = false };
            extraControls.Add(bundledNote);
            credBottom = 128;
        }
        else
        {
            var credNote = new Label
            {
                Text      = "OAuth credentials — from Google Cloud Console → APIs & Services → Credentials:",
                ForeColor = AppTheme.TextDisabled,
                AutoSize  = true,
                Location  = new Point(12, 96)
            };
            var clientIdLabel = new Label { Text = "Client ID:", AutoSize = true, Location = new Point(12, 120), ForeColor = AppTheme.TextPrimary };
            _clientIdBox = new TextBox
            {
                Location    = new Point(12, 138),
                Width       = 516,
                BackColor   = AppTheme.InputBack,
                ForeColor   = AppTheme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle
            };
            var clientSecretLabel = new Label { Text = "Client Secret:", AutoSize = true, Location = new Point(12, 168), ForeColor = AppTheme.TextPrimary };
            _clientSecretBox = new TextBox
            {
                Location     = new Point(12, 186),
                Width        = 516,
                BackColor    = AppTheme.InputBack,
                ForeColor    = AppTheme.TextPrimary,
                BorderStyle  = BorderStyle.FixedSingle,
                PasswordChar = '●'
            };
            extraControls.AddRange(new Control[] { credNote, clientIdLabel, _clientIdBox, clientSecretLabel, _clientSecretBox });
            credBottom = 220;
        }

        _sheetStatusLabel = new Label
        {
            Text      = "Google credentials: not configured",
            ForeColor = AppTheme.TextDisabled,
            AutoSize  = true,
            Location  = new Point(12, credBottom)
        };

        _sheetAuthBtn  = new Button { Text = "Set Up Google Auth", Location = new Point(12,  credBottom + 28), Width = 160 };
        _sheetResetBtn = new Button { Text = "Reset Auth",          Location = new Point(180, credBottom + 28), Width = 100 };

        StyleButton(_sheetAuthBtn,  AppTheme.BtnGreen);
        StyleButton(_sheetResetBtn, AppTheme.BtnOrange);

        _sheetAuthBtn.Click  += OnSheetAuth;
        _sheetResetBtn.Click += OnSheetReset;

        var all = new List<Control> { note, urlLabel, _sheetUrlBox };
        all.AddRange(extraControls);
        all.AddRange(new Control[] { _sheetStatusLabel, _sheetAuthBtn, _sheetResetBtn });
        tab.Controls.AddRange(all.ToArray());
        return tab;
    }

    private TabPage BuildGeneralTab()
    {
        var tab = new TabPage("General") { BackColor = AppTheme.FormBack, ForeColor = AppTheme.TextPrimary };

        var refreshLabel = new Label { Text = "Refresh every (hours):", AutoSize = true, Location = new Point(12, 20), ForeColor = AppTheme.TextPrimary };
        _refreshSpinner = new NumericUpDown
        {
            Minimum   = 1,
            Maximum   = 24,
            Value     = _original.RefreshHours,
            Location  = new Point(180, 18),
            Width     = 60,
            BackColor = AppTheme.InputBack,
            ForeColor = AppTheme.TextPrimary
        };

        _startupCheck = new CheckBox
        {
            Text      = "Launch at Windows startup",
            Checked   = _original.LaunchAtStartup,
            AutoSize  = true,
            Location  = new Point(12, 58),
            ForeColor = AppTheme.TextPrimary
        };

        var themeLabel = new Label { Text = "Theme:", AutoSize = true, Location = new Point(12, 96), ForeColor = AppTheme.TextPrimary };
        _themeCombo = new ComboBox
        {
            Location      = new Point(180, 94),
            Width         = 100,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor     = AppTheme.InputBack,
            ForeColor     = AppTheme.TextPrimary,
            FlatStyle     = FlatStyle.Flat
        };
        _themeCombo.Items.AddRange(new[] { "Dark", "Light" });

        tab.Controls.AddRange(new Control[] { refreshLabel, _refreshSpinner, _startupCheck, themeLabel, _themeCombo });
        return tab;
    }

    private void PopulateFields()
    {
        RefreshAccountList();
        _sheetUrlBox.Text     = _original.SpreadsheetId;
        _clientIdBox.Text     = _original.GoogleClientId;
        _clientSecretBox.Text = _original.GoogleClientSecret;
        _themeCombo.SelectedItem = _original.Theme?.ToLower() == "light" ? "Light" : "Dark";
        UpdateSheetStatus();
        UpdateAccountButtons();
    }

    private void RefreshAccountList()
    {
        _accountList.Items.Clear();
        foreach (var acc in _original.Accounts)
        {
            var item = new ListViewItem(acc.DisplayName);
            item.SubItems.Add(acc.Institution);
            item.SubItems.Add(acc.SheetCell);
            item.SubItems.Add(acc.Enabled ? "Yes" : "No");
            item.Tag       = acc;
            item.ForeColor = acc.Enabled ? AppTheme.TextPrimary : AppTheme.TextDisabled;
            _accountList.Items.Add(item);
        }
    }

    private void UpdateAccountButtons()
    {
        var sel = _accountList.SelectedItems.Count > 0;
        _editBtn.Enabled   = sel;
        _removeBtn.Enabled = sel;
    }

    private void OnAddAccount(object? sender, EventArgs e)
    {
        using var dlg = new AccountDialog(null);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            _original.Accounts.Add(dlg.Result!);
            RefreshAccountList();
        }
    }

    private void OnEditAccount(object? sender, EventArgs e)
    {
        if (_accountList.SelectedItems.Count == 0) return;
        var acc = (Account)_accountList.SelectedItems[0].Tag!;
        using var dlg = new AccountDialog(acc);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            var idx = _original.Accounts.IndexOf(acc);
            _original.Accounts[idx] = dlg.Result!;
            RefreshAccountList();
        }
    }

    private void OnRemoveAccount(object? sender, EventArgs e)
    {
        if (_accountList.SelectedItems.Count == 0) return;
        var acc = (Account)_accountList.SelectedItems[0].Tag!;
        if (MessageBox.Show($"Remove \"{acc.DisplayName}\"?", "Confirm", MessageBoxButtons.YesNo) == DialogResult.Yes)
        {
            CredentialService.DeletePassword(acc.Id.ToString());
            _original.Accounts.Remove(acc);
            RefreshAccountList();
        }
    }

    private async void OnSheetAuth(object? sender, EventArgs e)
    {
        _original.GoogleClientId     = _clientIdBox.Text.Trim();
        _original.GoogleClientSecret = _clientSecretBox.Text.Trim();

        if (!GoogleSheetsService.IsConfigured(_original))
        {
            _sheetStatusLabel.Text      = "Enter Client ID and Client Secret first.";
            _sheetStatusLabel.ForeColor = AppTheme.Warning;
            return;
        }

        _sheetAuthBtn.Enabled  = false;
        _sheetStatusLabel.Text = "Connecting to Google…";
        try
        {
            GoogleSheetsService.ResetAuth();
            await GoogleSheetsService.GetServiceAsync(_original);
            UpdateSheetStatus();
        }
        catch (Exception ex)
        {
            _sheetStatusLabel.Text      = $"Auth failed: {ex.Message}";
            _sheetStatusLabel.ForeColor = AppTheme.Negative;
        }
        finally
        {
            _sheetAuthBtn.Enabled = true;
        }
    }

    private void OnSheetReset(object? sender, EventArgs e)
    {
        GoogleSheetsService.ResetAuth();
        UpdateSheetStatus();
    }

    private void UpdateSheetStatus()
    {
        if (!GoogleSheetsService.IsConfigured(_original))
        {
            _sheetStatusLabel.Text      = "Enter Client ID and Client Secret, then click \"Set Up Google Auth\"";
            _sheetStatusLabel.ForeColor = AppTheme.TextDisabled;
            _sheetAuthBtn.Enabled       = true;
            _sheetResetBtn.Enabled      = false;
        }
        else if (GoogleSheetsService.HasToken)
        {
            _sheetStatusLabel.Text      = "Google account: connected";
            _sheetStatusLabel.ForeColor = AppTheme.Positive;
            _sheetAuthBtn.Enabled       = true;
            _sheetResetBtn.Enabled      = true;
        }
        else
        {
            _sheetStatusLabel.Text      = "Google account: not signed in — click \"Set Up Google Auth\"";
            _sheetStatusLabel.ForeColor = AppTheme.Warning;
            _sheetAuthBtn.Enabled       = true;
            _sheetResetBtn.Enabled      = true;
        }
    }

    private void OnSave(object? sender, EventArgs e)
    {
        _original.SpreadsheetId      = AppConfig.ExtractSheetId(_sheetUrlBox.Text.Trim());
        _original.GoogleClientId     = _clientIdBox.Text.Trim();
        _original.GoogleClientSecret = _clientSecretBox.Text.Trim();
        _original.RefreshHours       = (int)_refreshSpinner.Value;
        _original.LaunchAtStartup    = _startupCheck.Checked;
        _original.Theme              = _themeCombo.SelectedItem?.ToString()?.ToLower() ?? "dark";
        UpdatedConfig = _original;
    }

    private static void StyleButton(Button btn, Color bg)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.BackColor = bg;
        btn.ForeColor = Color.White;
        btn.FlatAppearance.BorderSize = 0;
        btn.Height = 28;
    }

    private void StyleTabControl(TabControl tc)
    {
        tc.DrawMode = TabDrawMode.OwnerDrawFixed;
        tc.DrawItem += (s, e) =>
        {
            var tab      = tc.TabPages[e.Index];
            var selected = e.Index == tc.SelectedIndex;

            // Selected = elevated; unselected = recessed into the nav bar
            var bgColor = selected
                ? (AppTheme.IsDark ? Color.FromArgb(52, 52, 52) : AppTheme.FormBack)
                : (AppTheme.IsDark ? Color.FromArgb(26, 26, 26) : AppTheme.TabBack);

            e.Graphics.FillRectangle(new SolidBrush(bgColor), e.Bounds);

            // Blue accent stripe at the bottom of the active tab
            if (selected)
            {
                e.Graphics.FillRectangle(
                    new SolidBrush(Color.FromArgb(50, 120, 210)),
                    e.Bounds.X + 3, e.Bounds.Bottom - 3, e.Bounds.Width - 6, 3);
            }

            // Text — shifted up to clear the accent stripe
            using var brush = new SolidBrush(selected ? AppTheme.TextPrimary : AppTheme.TextDisabled);
            var textRect = new Rectangle(e.Bounds.X, e.Bounds.Y, e.Bounds.Width, e.Bounds.Height - 3);
            e.Graphics.DrawString(tab.Text, tc.Font, brush, textRect, new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            });
        };
    }
}
