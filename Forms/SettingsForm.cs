using BankSync.Models;
using BankSync.Services;

namespace BankSync.Forms;

public class SettingsForm : Form
{
    private readonly AppConfig _original;
    public AppConfig UpdatedConfig { get; private set; }

    // Controls
    private ListView _accountList = null!;
    private Button _addBtn = null!;
    private Button _editBtn = null!;
    private Button _removeBtn = null!;
    private TextBox _sheetUrlBox = null!;
    private TextBox _clientIdBox = null!;
    private TextBox _clientSecretBox = null!;
    private NumericUpDown _refreshSpinner = null!;
    private CheckBox _startupCheck = null!;
    private Button _sheetAuthBtn = null!;
    private Button _sheetResetBtn = null!;
    private Label _sheetStatusLabel = null!;

    public SettingsForm(AppConfig config)
    {
        _original = config;
        UpdatedConfig = config;
        InitializeComponent();
        PopulateFields();
    }

    private void InitializeComponent()
    {
        Text = "BankSync Settings";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Size = new Size(560, 500);
        MinimumSize = new Size(560, 500);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(35, 35, 35);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        StyleTabControl(tabs);

        tabs.TabPages.Add(BuildAccountsTab());
        tabs.TabPages.Add(BuildSheetsTab());
        tabs.TabPages.Add(BuildGeneralTab());

        var ok = new Button { Text = "Save", DialogResult = DialogResult.OK, Width = 80 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80 };
        StyleButton(ok, Color.FromArgb(30, 140, 70));
        StyleButton(cancel, Color.FromArgb(70, 70, 70));

        ok.Click += OnSave;

        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 40,
            Padding = new Padding(4),
            BackColor = Color.FromArgb(30, 30, 30)
        };
        btnPanel.Controls.Add(cancel);
        btnPanel.Controls.Add(ok);

        Controls.Add(tabs);
        Controls.Add(btnPanel);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private TabPage BuildAccountsTab()
    {
        var tab = new TabPage("Accounts") { BackColor = Color.FromArgb(35, 35, 35), ForeColor = Color.White };

        _accountList = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            GridLines = false,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            BackColor = Color.FromArgb(45, 45, 45),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(12, 12),
            Size = new Size(516, 280)
        };
        _accountList.Columns.Add("Account", 180);
        _accountList.Columns.Add("Bank", 130);
        _accountList.Columns.Add("Cell", 60);
        _accountList.Columns.Add("Enabled", 60);
        _accountList.SelectedIndexChanged += (_, _) => UpdateAccountButtons();

        _addBtn    = new Button { Text = "Add",    Location = new Point(12,  302), Width = 80 };
        _editBtn   = new Button { Text = "Edit",   Location = new Point(100, 302), Width = 80 };
        _removeBtn = new Button { Text = "Remove", Location = new Point(188, 302), Width = 80 };

        StyleButton(_addBtn,    Color.FromArgb(30, 140, 70));
        StyleButton(_editBtn,   Color.FromArgb(50, 100, 160));
        StyleButton(_removeBtn, Color.FromArgb(160, 50, 50));

        _addBtn.Click    += OnAddAccount;
        _editBtn.Click   += OnEditAccount;
        _removeBtn.Click += OnRemoveAccount;

        tab.Controls.AddRange(new Control[] { _accountList, _addBtn, _editBtn, _removeBtn });
        return tab;
    }

    private TabPage BuildSheetsTab()
    {
        var tab = new TabPage("Google Sheets") { BackColor = Color.FromArgb(35, 35, 35), ForeColor = Color.White };

        var note = new Label
        {
            Text = "Optional — leave blank to skip Google Sheets sync.",
            ForeColor = Color.Gray,
            AutoSize = true,
            Location = new Point(12, 12)
        };

        var urlLabel = new Label { Text = "Spreadsheet URL or ID:", AutoSize = true, Location = new Point(12, 40) };
        _sheetUrlBox = new TextBox
        {
            Location = new Point(12, 58),
            Width = 516,
            BackColor = Color.FromArgb(55, 55, 55),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        var credNote = new Label
        {
            Text = "OAuth credentials — from Google Cloud Console → APIs & Services → Credentials:",
            ForeColor = Color.Gray,
            AutoSize = true,
            Location = new Point(12, 96)
        };

        var clientIdLabel = new Label { Text = "Client ID:", AutoSize = true, Location = new Point(12, 120) };
        _clientIdBox = new TextBox
        {
            Location = new Point(12, 138),
            Width = 516,
            BackColor = Color.FromArgb(55, 55, 55),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        var clientSecretLabel = new Label { Text = "Client Secret:", AutoSize = true, Location = new Point(12, 168) };
        _clientSecretBox = new TextBox
        {
            Location = new Point(12, 186),
            Width = 516,
            BackColor = Color.FromArgb(55, 55, 55),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            PasswordChar = '●'
        };

        _sheetStatusLabel = new Label
        {
            Text = "Google credentials: not configured",
            ForeColor = Color.Gray,
            AutoSize = true,
            Location = new Point(12, 224)
        };

        _sheetAuthBtn  = new Button { Text = "Set Up Google Auth", Location = new Point(12,  250), Width = 160 };
        _sheetResetBtn = new Button { Text = "Reset Auth",          Location = new Point(180, 250), Width = 100 };

        StyleButton(_sheetAuthBtn,  Color.FromArgb(30, 140, 70));
        StyleButton(_sheetResetBtn, Color.FromArgb(160, 80, 30));

        _sheetAuthBtn.Click  += OnSheetAuth;
        _sheetResetBtn.Click += OnSheetReset;

        tab.Controls.AddRange(new Control[]
        {
            note, urlLabel, _sheetUrlBox,
            credNote, clientIdLabel, _clientIdBox, clientSecretLabel, _clientSecretBox,
            _sheetStatusLabel, _sheetAuthBtn, _sheetResetBtn
        });
        return tab;
    }

    private TabPage BuildGeneralTab()
    {
        var tab = new TabPage("General") { BackColor = Color.FromArgb(35, 35, 35), ForeColor = Color.White };

        var refreshLabel = new Label { Text = "Refresh every (hours):", AutoSize = true, Location = new Point(12, 20) };
        _refreshSpinner = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 24,
            Value = _original.RefreshHours,
            Location = new Point(180, 18),
            Width = 60,
            BackColor = Color.FromArgb(55, 55, 55),
            ForeColor = Color.White
        };

        _startupCheck = new CheckBox
        {
            Text = "Launch at Windows startup",
            Checked = _original.LaunchAtStartup,
            AutoSize = true,
            Location = new Point(12, 58),
            ForeColor = Color.White
        };

        tab.Controls.AddRange(new Control[] { refreshLabel, _refreshSpinner, _startupCheck });
        return tab;
    }

    private void PopulateFields()
    {
        RefreshAccountList();
        _sheetUrlBox.Text      = _original.SpreadsheetId;
        _clientIdBox.Text      = _original.GoogleClientId;
        _clientSecretBox.Text  = _original.GoogleClientSecret;
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
            item.Tag = acc;
            item.ForeColor = acc.Enabled ? Color.White : Color.Gray;
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
        // Save current field values into config so GetServiceAsync can read them
        _original.GoogleClientId     = _clientIdBox.Text.Trim();
        _original.GoogleClientSecret = _clientSecretBox.Text.Trim();

        if (!GoogleSheetsService.IsConfigured(_original))
        {
            _sheetStatusLabel.Text      = "Enter Client ID and Client Secret first.";
            _sheetStatusLabel.ForeColor = Color.FromArgb(255, 180, 80);
            return;
        }

        _sheetAuthBtn.Enabled = false;
        _sheetStatusLabel.Text = "Connecting to Google…";
        try
        {
            GoogleSheetsService.ResetAuth(); // force fresh auth with current credentials
            await GoogleSheetsService.GetServiceAsync(_original);
            UpdateSheetStatus();
        }
        catch (Exception ex)
        {
            _sheetStatusLabel.Text      = $"Auth failed: {ex.Message}";
            _sheetStatusLabel.ForeColor = Color.FromArgb(255, 100, 100);
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
            _sheetStatusLabel.ForeColor = Color.Gray;
            _sheetAuthBtn.Enabled       = true;
            _sheetResetBtn.Enabled      = false;
        }
        else if (GoogleSheetsService.HasToken)
        {
            _sheetStatusLabel.Text      = "Google account: connected";
            _sheetStatusLabel.ForeColor = Color.FromArgb(100, 220, 100);
            _sheetAuthBtn.Enabled       = true;
            _sheetResetBtn.Enabled      = true;
        }
        else
        {
            _sheetStatusLabel.Text      = "Google account: not signed in — click \"Set Up Google Auth\"";
            _sheetStatusLabel.ForeColor = Color.FromArgb(255, 180, 80);
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

    private static void StyleTabControl(TabControl tc)
    {
        tc.DrawMode = TabDrawMode.OwnerDrawFixed;
        tc.DrawItem += (s, e) =>
        {
            var tab = tc.TabPages[e.Index];
            e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(45, 45, 45)), e.Bounds);
            var selected = e.Index == tc.SelectedIndex;
            using var brush = new SolidBrush(selected ? Color.White : Color.Gray);
            e.Graphics.DrawString(tab.Text, tc.Font, brush, e.Bounds, new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            });
        };
    }
}
