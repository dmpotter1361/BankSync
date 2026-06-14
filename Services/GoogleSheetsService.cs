using BankSync.Models;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace BankSync.Services;

public static class GoogleSheetsService
{
    private static SheetsService? _service;

    private static readonly string TokenPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "BankSync", "google-token");

    public static bool IsConfigured(AppConfig config) =>
        !string.IsNullOrWhiteSpace(config.GoogleClientId) &&
        !string.IsNullOrWhiteSpace(config.GoogleClientSecret);

    public static bool HasToken => Directory.Exists(TokenPath) && Directory.GetFiles(TokenPath).Length > 0;

    public static async Task<SheetsService?> GetServiceAsync(AppConfig config)
    {
        if (_service != null) return _service;
        if (!IsConfigured(config)) return null;

        var secrets = new ClientSecrets { ClientId = config.GoogleClientId, ClientSecret = config.GoogleClientSecret };
        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,
            new[] { SheetsService.Scope.Spreadsheets },
            "user",
            CancellationToken.None,
            new Google.Apis.Util.Store.FileDataStore(TokenPath, true));

        _service = new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "BankSync"
        });

        return _service;
    }

    public static async Task<bool> UpdateCellAsync(AppConfig config, string spreadsheetId, string cell, decimal value)
    {
        var service = await GetServiceAsync(config);
        if (service == null) return false;

        try
        {
            var body = new ValueRange
            {
                Values = new List<IList<object>> { new List<object> { (double)value } }
            };

            await service.Spreadsheets.Values
                .Update(body, spreadsheetId, cell)
                .ExecuteAsync();

            // Also set format to currency
            await FormatAsCurrencyAsync(service, spreadsheetId, cell);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static async Task SyncAllAccountsAsync(AppConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.SpreadsheetId)) return;

        var service = await GetServiceAsync(config);
        if (service == null) return;

        var updates = new List<ValueRange>();

        foreach (var account in config.Accounts.Where(a => a.Enabled && !string.IsNullOrWhiteSpace(a.SheetCell) && a.LastBalance != null))
        {
            updates.Add(new ValueRange
            {
                Range = account.SheetCell,
                Values = new List<IList<object>> { new List<object> { (double)account.LastBalance!.Ledger } }
            });
        }

        if (updates.Count == 0) return;

        var batchBody = new BatchUpdateValuesRequest
        {
            ValueInputOption = "RAW",
            Data = updates
        };

        try
        {
            await service.Spreadsheets.Values
                .BatchUpdate(batchBody, config.SpreadsheetId)
                .ExecuteAsync();
        }
        catch (Exception ex)
        {
            WriteErrorLog($"Google Sheets sync failed: {ex.Message}");
        }
    }

    private static void WriteErrorLog(string message)
    {
        try
        {
            var logPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BankSync", "error.log");
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
        }
        catch { }
    }

    private static async Task FormatAsCurrencyAsync(SheetsService service, string spreadsheetId, string cellRef)
    {
        try
        {
            // Parse cell ref like "C2" to grid coords
            var col = cellRef.TrimEnd("0123456789".ToCharArray());
            var row = int.Parse(cellRef[col.Length..]) - 1;
            var colIdx = col.Aggregate(0, (acc, c) => acc * 26 + (c - 'A' + 1)) - 1;

            var request = new BatchUpdateSpreadsheetRequest
            {
                Requests = new List<Request>
                {
                    new Request
                    {
                        RepeatCell = new RepeatCellRequest
                        {
                            Range = new GridRange { StartRowIndex = row, EndRowIndex = row + 1, StartColumnIndex = colIdx, EndColumnIndex = colIdx + 1 },
                            Cell = new CellData
                            {
                                UserEnteredFormat = new CellFormat
                                {
                                    NumberFormat = new NumberFormat { Type = "CURRENCY", Pattern = "$#,##0.00" }
                                }
                            },
                            Fields = "userEnteredFormat.numberFormat"
                        }
                    }
                }
            };

            await service.Spreadsheets.BatchUpdate(request, spreadsheetId).ExecuteAsync();
        }
        catch { }
    }

    public static void ResetAuth()
    {
        _service = null;
        if (Directory.Exists(TokenPath))
            Directory.Delete(TokenPath, true);
    }
}
