using BankSync.Models;
using BankSync.Services;

namespace BankSync.Forms;

public class AccountDialog : Form
{
    private readonly Account? _editing;
    public Account? Result { get; private set; }

    private TextBox _nameBox = null!;
    private ComboBox _bankCombo = null!;
    private TextBox _usernameBox = null!;
    private TextBox _passwordBox = null!;
    private TextBox _accountNumBox = null!;
    private ComboBox _accountTypeCombo = null!;
    private TextBox _cellBox = null!;
    private CheckBox _enabledCheck = null!;
    private ComboBox _refreshCombo = null!;
    private TextBox _watchFolderBox = null!;
    private TextBox _loginUrlBox = null!;
    private Button _browseFolderBtn = null!;
    private Panel _watchPanel = null!;
    private Panel _scraperPanel = null!;
    private Button _setupLoginBtn = null!;
    private Label _scraperStatusLabel = null!;
    private Panel _ofxPanel = null!;   // username/password/account fields

    // Custom OFX fields
    private Panel _customPanel = null!;
    private TextBox _customUrlBox = null!;
    private TextBox _customFidBox = null!;
    private TextBox _customOrgBox = null!;
    private TextBox _customBankIdBox = null!;

    public AccountDialog(Account? editing)
    {
        _editing = editing;
        InitializeComponent();
        PopulateFields();
    }

    private void InitializeComponent()
    {
        Text = _editing == null ? "Add Account" : "Edit Account";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(35, 35, 35);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 9);

        int y = 12;
        int labelX = 12, fieldX = 150, fieldW = 240;

        // Display name
        Controls.Add(MakeLabel("Display Name:", labelX, y));
        _nameBox = MakeTextBox(fieldX, y, fieldW); Controls.Add(_nameBox); y += 36;

        // Bank picker
        Controls.Add(MakeLabel("Bank:", labelX, y));
        _bankCombo = new ComboBox
        {
            Location = new Point(fieldX, y), Width = fieldW,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(55, 55, 55), ForeColor = Color.White, FlatStyle = FlatStyle.Flat
        };
        foreach (var k in OFXService.KnownBanks.Keys) _bankCombo.Items.Add(k);
        _bankCombo.Items.Add("Browser Login (auto-scrape)");
        _bankCombo.Items.Add("Watch Folder (QFX file)");
        _bankCombo.Items.Add("Custom OFX…");
        _bankCombo.SelectedIndexChanged += OnBankChanged;
        Controls.Add(_bankCombo); y += 36;

        // Custom OFX panel (hidden by default)
        _customPanel = new Panel { Location = new Point(labelX, y), Size = new Size(390, 132), Visible = false, BackColor = Color.FromArgb(35,35,35) };
        int py = 0;
        _customPanel.Controls.Add(MakeLabel("OFX URL:", 0, py));
        _customUrlBox = MakeTextBox(128, py, 256); _customPanel.Controls.Add(_customUrlBox); py += 32;
        _customPanel.Controls.Add(MakeLabel("FID:", 0, py));
        _customFidBox = MakeTextBox(128, py, 80); _customPanel.Controls.Add(_customFidBox); py += 32;
        _customPanel.Controls.Add(MakeLabel("Org:", 0, py));
        _customOrgBox = MakeTextBox(128, py, 80); _customPanel.Controls.Add(_customOrgBox); py += 32;
        _customPanel.Controls.Add(MakeLabel("Bank ID:", 0, py));
        _customBankIdBox = MakeTextBox(128, py, 120); _customPanel.Controls.Add(_customBankIdBox);
        Controls.Add(_customPanel);

        // OFX credentials panel
        _ofxPanel = new Panel { Location = new Point(0, y), Size = new Size(410, 110), BackColor = Color.FromArgb(35,35,35) };
        _ofxPanel.Controls.Add(MakeLabel("Username:", labelX, 0));
        _usernameBox = MakeTextBox(fieldX, 0, fieldW); _ofxPanel.Controls.Add(_usernameBox);
        _ofxPanel.Controls.Add(MakeLabel("Password:", labelX, 36));
        _passwordBox = MakeTextBox(fieldX, 36, fieldW);
        _passwordBox.UseSystemPasswordChar = true;
        _ofxPanel.Controls.Add(_passwordBox);
        _ofxPanel.Controls.Add(MakeLabel("Account Number:", labelX, 72));
        _accountNumBox = MakeTextBox(fieldX, 72, fieldW); _ofxPanel.Controls.Add(_accountNumBox);
        Controls.Add(_ofxPanel); y += 110;

        // Watch folder panel
        _watchPanel = new Panel { Location = new Point(0, y - 110), Size = new Size(410, 76), Visible = false, BackColor = Color.FromArgb(35,35,35) };
        _watchPanel.Controls.Add(MakeLabel("QFX Folder:", labelX, 0));
        _watchFolderBox = MakeTextBox(fieldX, 0, 180); _watchPanel.Controls.Add(_watchFolderBox);
        _browseFolderBtn = new Button { Text = "Browse…", Location = new Point(fieldX + 186, 0), Width = 60 };
        StyleButton(_browseFolderBtn, Color.FromArgb(60, 60, 80));
        _browseFolderBtn.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog();
            if (dlg.ShowDialog() == DialogResult.OK)
                _watchFolderBox.Text = dlg.SelectedPath;
        };
        _watchPanel.Controls.Add(_browseFolderBtn);
        _watchPanel.Controls.Add(MakeLabel("Login URL:", labelX, 40));
        _loginUrlBox = MakeTextBox(fieldX, 40, 240);
        _loginUrlBox.PlaceholderText = "Optional — opens browser on sync";
        _watchPanel.Controls.Add(_loginUrlBox);
        Controls.Add(_watchPanel);

        // Browser scraper panel
        _scraperPanel = new Panel { Location = new Point(0, y - 110), Size = new Size(410, 76), Visible = false, BackColor = Color.FromArgb(35,35,35) };
        _scraperPanel.Controls.Add(MakeLabel("Login URL:", labelX, 0));
        var scraperUrlBox = MakeTextBox(fieldX, 0, 240);
        scraperUrlBox.Name = "scraperUrlBox";
        _scraperPanel.Controls.Add(scraperUrlBox);
        _setupLoginBtn = new Button { Text = "Setup Login…", Location = new Point(fieldX, 40), Width = 120 };
        StyleButton(_setupLoginBtn, Color.FromArgb(30, 100, 180));
        _setupLoginBtn.Click += OnSetupLogin;
        _scraperStatusLabel = new Label { Location = new Point(fieldX + 130, 44), AutoSize = true, ForeColor = Color.Gray, Text = "Not configured" };
        _scraperPanel.Controls.Add(_setupLoginBtn);
        _scraperPanel.Controls.Add(_scraperStatusLabel);
        Controls.Add(_scraperPanel);

        // Account type
        Controls.Add(MakeLabel("Account Type:", labelX, y));
        _accountTypeCombo = new ComboBox
        {
            Location = new Point(fieldX, y), Width = 120,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(55, 55, 55), ForeColor = Color.White, FlatStyle = FlatStyle.Flat
        };
        _accountTypeCombo.Items.AddRange(new[] { "CHECKING", "SAVINGS", "MONEYMRKT", "CREDITLINE", "BROKERAGE", "INVESTMENT" });
        _accountTypeCombo.SelectedIndex = 0;
        Controls.Add(_accountTypeCombo); y += 36;

        // Refresh frequency
        Controls.Add(MakeLabel("Refresh:", labelX, y));
        _refreshCombo = new ComboBox
        {
            Location = new Point(fieldX, y), Width = 180,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(55, 55, 55), ForeColor = Color.White, FlatStyle = FlatStyle.Flat
        };
        _refreshCombo.Items.AddRange(new object[] {
            "Use global setting",
            "On demand only",
            "Every 1 hour",
            "Every 2 hours",
            "Every 4 hours",
            "Every 6 hours",
            "Every 12 hours",
            "Every 24 hours"
        });
        _refreshCombo.SelectedIndex = 0;
        Controls.Add(_refreshCombo); y += 36;

        // Sheet cell
        Controls.Add(MakeLabel("Sheet Cell (opt.):", labelX, y));
        _cellBox = MakeTextBox(fieldX, y, 80);
        _cellBox.PlaceholderText = "e.g. C2";
        Controls.Add(_cellBox); y += 36;

        // Enabled
        _enabledCheck = new CheckBox
        {
            Text = "Enabled", Checked = true, AutoSize = true,
            Location = new Point(fieldX, y), ForeColor = Color.White
        };
        Controls.Add(_enabledCheck); y += 36;

        var ok     = new Button { Text = "Save",   DialogResult = DialogResult.OK,     Location = new Point(230, y), Width = 80 };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(318, y), Width = 80 };
        StyleButton(ok,     Color.FromArgb(30, 140, 70));
        StyleButton(cancel, Color.FromArgb(70, 70, 70));
        ok.Click += OnSave;
        Controls.Add(ok);
        Controls.Add(cancel);
        ClientSize = new Size(410, y + 44);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    private void OnBankChanged(object? sender, EventArgs e)
    {
        var selected   = _bankCombo.SelectedItem?.ToString() ?? "";
        var isCustom   = selected == "Custom OFX…";
        var isWatcher  = selected == "Watch Folder (QFX file)";
        var isScraper  = selected == "Browser Login (auto-scrape)";

        // Show/hide custom OFX fields
        var customWasVisible = _customPanel.Visible;
        _customPanel.Visible = isCustom;

        // Shift OFX panel and everything below it
        int customHeight = _customPanel.Height + 4;
        if (isCustom && !customWasVisible)
        {
            _customPanel.Location = new Point(12, _bankCombo.Bottom + 8);
            _ofxPanel.Top   += customHeight;
            _watchPanel.Top += customHeight;
            foreach (Control c in Controls)
                if (c != _customPanel && c != _bankCombo && c.Top > _bankCombo.Bottom + 4 && c != _ofxPanel && c != _watchPanel)
                    c.Top += customHeight;
            ClientSize = new Size(ClientSize.Width, ClientSize.Height + customHeight);
        }
        else if (!isCustom && customWasVisible)
        {
            _ofxPanel.Top   -= customHeight;
            _watchPanel.Top -= customHeight;
            foreach (Control c in Controls)
                if (c != _customPanel && c != _bankCombo && c.Top > _bankCombo.Bottom + 4 && c != _ofxPanel && c != _watchPanel)
                    c.Top -= customHeight;
            ClientSize = new Size(ClientSize.Width, ClientSize.Height - customHeight);
        }

        _ofxPanel.Visible     = !isWatcher && !isScraper;
        _watchPanel.Visible   = isWatcher;
        _scraperPanel.Visible = isScraper;

        // Auto-fill login URL for known banks
        if (isScraper && OFXService.KnownLoginUrls.TryGetValue(selected.Replace(" (Browser Login (auto-scrape))", ""), out _))
        {
            // noop — URL is set via Setup Login wizard
        }
        if (isScraper && _refreshCombo.SelectedIndex == 0)
            _refreshCombo.SelectedIndex = 1; // default to on-demand

        // Default refresh for watch-folder mode
        if (isWatcher && _refreshCombo.SelectedIndex == 0)
            _refreshCombo.SelectedIndex = 1; // manual/watch only

        // Auto-fill login URL from known banks
        if (isWatcher && string.IsNullOrWhiteSpace(_loginUrlBox.Text))
        {
            var bank = _bankCombo.SelectedItem?.ToString() ?? "";
            if (OFXService.KnownLoginUrls.TryGetValue(bank, out var url))
                _loginUrlBox.Text = url;
        }
    }

    private void PopulateFields()
    {
        if (_editing == null) return;

        _account_temp_selector = _editing.BalanceSelector;
        _nameBox.Text = _editing.DisplayName;
        _accountNumBox.Text = _editing.AccountNumber;
        _accountTypeCombo.SelectedItem = _editing.AccountType;
        _cellBox.Text = _editing.SheetCell;
        _enabledCheck.Checked = _editing.Enabled;
        _watchFolderBox.Text = _editing.WatchFolderPath;
        _loginUrlBox.Text    = _editing.LoginUrl;

        // Refresh selection
        _refreshCombo.SelectedIndex = _editing.RefreshHours switch
        {
            -1  => 0,
            0   => 1,
            1   => 2,
            2   => 3,
            4   => 4,
            6   => 5,
            12  => 6,
            24  => 7,
            _   => 0
        };

        // Show configured status for scraper accounts
        if (_editing.IsScraper && ScraperService.HasSession(_editing.Id))
        {
            _scraperStatusLabel.Text      = "Session saved";
            _scraperStatusLabel.ForeColor = Color.FromArgb(100, 220, 100);
        }

        // Bank selection
        if (_editing.IsWatchFolder)
        {
            _bankCombo.SelectedItem = "Watch Folder (QFX file)";
        }
        else if (_editing.IsScraper)
        {
            _bankCombo.SelectedItem = "Browser Login (auto-scrape)";
            var urlBox = _scraperPanel.Controls.OfType<TextBox>().FirstOrDefault();
            if (urlBox != null) urlBox.Text = _editing.LoginUrl;
        }
        else
        {
            var match = OFXService.KnownBanks.Keys.FirstOrDefault(k =>
                OFXService.KnownBanks[k].Url == _editing.OfxUrl);
            if (match != null)
                _bankCombo.SelectedItem = match;
            else if (!string.IsNullOrWhiteSpace(_editing.OfxUrl))
            {
                _bankCombo.SelectedItem = "Custom OFX…";
                _customUrlBox.Text    = _editing.OfxUrl;
                _customFidBox.Text    = _editing.OfxFid;
                _customOrgBox.Text    = _editing.OfxOrg;
                _customBankIdBox.Text = _editing.OfxBankId;
            }

            var creds = CredentialService.GetPassword(_editing.Id.ToString());
            if (creds.HasValue)
            {
                _usernameBox.Text = creds.Value.username;
                _passwordBox.Text = creds.Value.password;
            }
        }
    }

    private void OnSetupLogin(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_nameBox.Text))
        {
            MessageBox.Show("Enter a display name first.", "BankSync", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var acc = _editing ?? new Account();
        acc.DisplayName = _nameBox.Text.Trim();
        acc.Institution = _bankCombo.SelectedItem?.ToString() ?? "";

        // Get login URL from known banks or the URL box in scraper panel
        var urlBox = _scraperPanel.Controls.OfType<TextBox>().FirstOrDefault();
        var loginUrl = urlBox?.Text.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(loginUrl) && OFXService.KnownLoginUrls.TryGetValue(acc.Institution, out var known))
            loginUrl = known;

        acc.LoginUrl = loginUrl;

        using var dlg = new BrowserSetupForm(acc);
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            // BalanceSelector was set on acc by the form
            _account_temp_selector = acc.BalanceSelector;
            _scraperStatusLabel.Text      = "Session saved";
            _scraperStatusLabel.ForeColor = Color.FromArgb(100, 220, 100);
        }
    }

    private string _account_temp_selector = "";

    private void OnSave(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_nameBox.Text))
        {
            MessageBox.Show("Display name is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.None;
            return;
        }

        var acc = _editing ?? new Account();
        acc.DisplayName   = _nameBox.Text.Trim();
        acc.AccountType   = _accountTypeCombo.SelectedItem?.ToString() ?? "CHECKING";
        acc.SheetCell     = _cellBox.Text.Trim().ToUpper();
        acc.Enabled       = _enabledCheck.Checked;
        acc.Institution   = _bankCombo.SelectedItem?.ToString() ?? "";

        acc.RefreshHours = _refreshCombo.SelectedIndex switch
        {
            0 => -1,
            1 => 0,
            2 => 1,
            3 => 2,
            4 => 4,
            5 => 6,
            6 => 12,
            7 => 24,
            _ => -1
        };

        var selected = _bankCombo.SelectedItem?.ToString() ?? "";

        if (selected == "Browser Login (auto-scrape)")
        {
            var urlBox = _scraperPanel.Controls.OfType<TextBox>().FirstOrDefault();
            var loginUrl = urlBox?.Text.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(loginUrl) && OFXService.KnownLoginUrls.TryGetValue(acc.Institution, out var known))
                loginUrl = known;
            acc.LoginUrl         = loginUrl;
            acc.BalanceSelector  = _account_temp_selector;
            acc.WatchFolderPath  = "";
            acc.OfxUrl = acc.OfxFid = acc.OfxOrg = acc.OfxBankId = "";
        }
        else if (selected == "Watch Folder (QFX file)")
        {
            acc.WatchFolderPath = _watchFolderBox.Text.Trim();
            acc.LoginUrl        = _loginUrlBox.Text.Trim();
            acc.OfxUrl = acc.OfxFid = acc.OfxOrg = acc.OfxBankId = "";
        }
        else
        {
            acc.WatchFolderPath = "";
            acc.AccountNumber   = _accountNumBox.Text.Trim();

            if (selected == "Custom OFX…")
            {
                acc.OfxUrl    = _customUrlBox.Text.Trim();
                acc.OfxFid    = _customFidBox.Text.Trim();
                acc.OfxOrg    = _customOrgBox.Text.Trim();
                acc.OfxBankId = _customBankIdBox.Text.Trim();
            }
            else if (OFXService.KnownBanks.TryGetValue(selected, out var info))
            {
                acc.OfxUrl    = info.Url;
                acc.OfxFid    = info.Fid;
                acc.OfxOrg    = info.Org;
                acc.OfxBankId = info.BankId;
            }

            if (!string.IsNullOrWhiteSpace(_usernameBox.Text))
                CredentialService.SavePassword(acc.Id.ToString(), _usernameBox.Text.Trim(), _passwordBox.Text);
        }

        Result = acc;
    }

    private static Label MakeLabel(string text, int x, int y) => new Label
    {
        Text = text, Location = new Point(x, y + 3),
        AutoSize = true, ForeColor = Color.FromArgb(200, 200, 200)
    };

    private static TextBox MakeTextBox(int x, int y, int w) => new TextBox
    {
        Location = new Point(x, y), Width = w,
        BackColor = Color.FromArgb(55, 55, 55), ForeColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle
    };

    private static void StyleButton(Button btn, Color bg)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.BackColor = bg;
        btn.ForeColor = Color.White;
        btn.FlatAppearance.BorderSize = 0;
        btn.Height = 28;
    }
}
