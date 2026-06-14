namespace BankSync.Models;

public class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = "";
    public string Institution { get; set; } = "";
    public string OfxBankId { get; set; } = "";
    public string OfxFid { get; set; } = "";
    public string OfxOrg { get; set; } = "";
    public string OfxUrl { get; set; } = "";
    public string Username { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public string AccountType { get; set; } = "CHECKING";
    public string SheetCell { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public Balance? LastBalance { get; set; }

    // -1 = use global setting, 0 = manual/watch-folder only, >0 = hours
    public int RefreshHours { get; set; } = -1;
    public string WatchFolderPath { get; set; } = "";
    public string LoginUrl { get; set; } = "";

    // Browser scraper fields
    public string BalanceSelector      { get; set; } = "";
    public int    BalanceSelectorIndex { get; set; } = -1; // index in the found-amounts list; -1 = use first

    public bool IsWatchFolder => !string.IsNullOrWhiteSpace(WatchFolderPath);
    public bool OpensBrowser  => IsWatchFolder && !string.IsNullOrWhiteSpace(LoginUrl);
    public bool IsScraper     => !string.IsNullOrWhiteSpace(LoginUrl) && string.IsNullOrWhiteSpace(WatchFolderPath);
}
