using Newtonsoft.Json;

namespace BankSync.Models;

public class AppConfig
{
    public string SpreadsheetId    { get; set; } = "";
    public string GoogleClientId   { get; set; } = "";
    public string GoogleClientSecret { get; set; } = "";
    public int RefreshHours { get; set; } = 6;
    public bool LaunchAtStartup { get; set; } = false;
    public List<Account> Accounts { get; set; } = new();
    public DateTime? LastSync { get; set; }

    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BankSync", "config.json");

    public static AppConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
                return JsonConvert.DeserializeObject<AppConfig>(File.ReadAllText(ConfigPath)) ?? new AppConfig();
        }
        catch { }
        return new AppConfig();
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
