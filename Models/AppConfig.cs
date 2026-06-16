using Newtonsoft.Json;

namespace BankSync.Models;

public class AppConfig
{
    public string SpreadsheetId      { get; set; } = "";
    public string GoogleClientId     { get; set; } = "";
    public string GoogleClientSecret { get; set; } = "";
    public bool   WizardCompleted    { get; set; } = false;
    public string Theme              { get; set; } = "dark";
    public int    RefreshHours       { get; set; } = 6;
    public bool   LaunchAtStartup    { get; set; } = false;
    public List<Account> Accounts    { get; set; } = new();
    public DateTime? LastSync        { get; set; }

    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BankSync", "config.json");

    private static readonly string BundledCredPath = Path.Combine(
        AppContext.BaseDirectory, "credentials.json");

    public static AppConfig Load()
    {
        AppConfig cfg;
        try
        {
            cfg = File.Exists(ConfigPath)
                ? JsonConvert.DeserializeObject<AppConfig>(File.ReadAllText(ConfigPath)) ?? new AppConfig()
                : new AppConfig();
        }
        catch { cfg = new AppConfig(); }

        // If no credentials in config, try bundled credentials.json next to the exe
        if (string.IsNullOrWhiteSpace(cfg.GoogleClientId) && File.Exists(BundledCredPath))
        {
            try
            {
                var bundled = JsonConvert.DeserializeObject<BundledCreds>(File.ReadAllText(BundledCredPath));
                if (bundled != null)
                {
                    cfg.GoogleClientId     = bundled.GoogleClientId     ?? "";
                    cfg.GoogleClientSecret = bundled.GoogleClientSecret ?? "";
                }
            }
            catch { }
        }

        return cfg;
    }

    private class BundledCreds
    {
        public string? GoogleClientId     { get; set; }
        public string? GoogleClientSecret { get; set; }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(this, Formatting.Indented));
    }

    public static string ExtractSheetId(string urlOrId)
    {
        if (urlOrId.Contains("/d/"))
        {
            var start = urlOrId.IndexOf("/d/") + 3;
            var end = urlOrId.IndexOf("/", start);
            return end > start ? urlOrId[start..end] : urlOrId[start..];
        }
        return urlOrId;
    }
}
