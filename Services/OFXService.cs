using BankSync.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;

namespace BankSync.Services;

public static class OFXService
{
    private static readonly HttpClient Http = new();

    public static readonly Dictionary<string, string> KnownLoginUrls = new()
    {
        ["Bank of America"] = "https://www.bankofamerica.com/",
        ["M&T Bank"]        = "https://www.mtb.com/",
        ["Chase"]           = "https://www.chase.com/",
        ["Wells Fargo"]     = "https://www.wellsfargo.com/",
        ["Citibank"]        = "https://online.citi.com/",
        ["US Bank"]         = "https://www.usbank.com/",
    };

    // Known OFX endpoints for common banks
    public static readonly Dictionary<string, (string Url, string Fid, string Org, string BankId)> KnownBanks = new()
    {
        ["Bank of America"] = ("https://eftx.bankofamerica.com/eftxweb/access.ofx", "5959", "HAN", "121000358"),
        ["M&T Bank"]        = ("https://ofx.mtb.com/ofxserver/ofxsrvr.dll", "1900", "M&T Bank", "022000046"),
        ["Chase"]           = ("https://ofx.chase.com", "10898", "B1", "072000326"),
        ["Wells Fargo"]     = ("https://ofxdc.wellsfargo.com/ofx/process.ofx", "3860", "WF", "121042882"),
        ["Citibank"]        = ("https://www.accountonline.com/cards/svc/CitiOfxManager.do", "24909", "Citigroup", "031100209"),
        ["US Bank"]         = ("https://ofx.usbank.com/axis/servlet/OfxSoapServlet", "1001", "US", "091000022"),
    };

    public static async Task<Balance> GetBalanceAsync(Account account)
    {
        var creds = CredentialService.GetPassword(account.Id.ToString());
        if (creds == null)
            throw new InvalidOperationException($"No credentials stored for {account.DisplayName}");

        var (username, password) = creds.Value;
        var now = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var dtStart = DateTime.UtcNow.AddDays(-14).ToString("yyyyMMdd");

        var request = $"""
            OFXHEADER:100
            DATA:OFXSGML
            VERSION:151
            SECURITY:NONE
            ENCODING:USASCII
            CHARSET:1252
            COMPRESSION:NONE
            OLDFILEUID:NONE
            NEWFILEUID:NONE

            <OFX>
            <SIGNONMSGSRQV1>
            <SONRQ>
            <DTCLIENT>{now}</DTCLIENT>
            <USERID>{username}</USERID>
            <USERPASS>{password}</USERPASS>
            <LANGUAGE>ENG</LANGUAGE>
            <FI>
            <ORG>{account.OfxOrg}</ORG>
            <FID>{account.OfxFid}</FID>
            </FI>
            <APPID>QWIN</APPID>
            <APPVER>2700</APPVER>
            </SONRQ>
            </SIGNONMSGSRQV1>
            <BANKMSGSRQV1>
            <STMTTRNRQ>
            <TRNUID>1001</TRNUID>
            <STMTRQ>
            <BANKACCTFROM>
            <BANKID>{account.OfxBankId}</BANKID>
            <ACCTID>{account.AccountNumber}</ACCTID>
            <ACCTTYPE>{account.AccountType}</ACCTTYPE>
            </BANKACCTFROM>
            <INCTRAN>
            <DTSTART>{dtStart}</DTSTART>
            <INCLUDE>Y</INCLUDE>
            </INCTRAN>
            </STMTRQ>
            </STMTTRNRQ>
            </BANKMSGSRQV1>
            </OFX>
            """;

        var content = new StringContent(request, Encoding.ASCII, "application/x-ofx");
        content.Headers.ContentType = new MediaTypeHeaderValue("application/x-ofx");

        HttpResponseMessage response;
        string body;
        try
        {
            response = await Http.PostAsync(account.OfxUrl, content);
            body = await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Network error: {ex.Message}");
        }

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}");

        // Check for OFX signon error codes before parsing
        CheckOfxStatus(body);

        return ParseBalance(body);
    }

    private static void CheckOfxStatus(string ofx)
    {
        // Look for SONRS status (signon response)
        var code = Extract(ofx[Math.Max(0, ofx.IndexOf("<SONRS>", StringComparison.OrdinalIgnoreCase))..], "CODE");
        if (string.IsNullOrEmpty(code)) return;

        if (code == "0" || code == "1") return; // success

        var message = Extract(ofx, "MESSAGE");
        throw new InvalidOperationException(code switch
        {
            "15000" => "Authentication failed — wrong username or password",
            "15500" => "MFA/2FA required — bank needs an app password for direct connect",
            "15501" => "MFA challenge required — use OFX PIN instead of online password",
            "15502" => "MFA challenge required — check your bank's direct connect settings",
            "1800"  => "Account locked — too many failed attempts",
            "1900"  => "Account not authorized for direct connect",
            "3000"  => $"Bank returned error: {(string.IsNullOrWhiteSpace(message) ? $"code {code}" : message)}",
            _       => $"OFX error code {code}{(string.IsNullOrWhiteSpace(message) ? "" : $": {message}")}"
        });
    }

    public static Balance ParseQfxFile(string filePath) =>
        ParseBalance(File.ReadAllText(filePath));

    private static Balance ParseBalance(string ofx)
    {
        var balance = new Balance { AsOf = DateTime.Now };

        // Ledger balance
        var ledger = ExtractValue(ofx, "BALAMT", after: "LEDGERBAL");
        if (decimal.TryParse(ledger, out var l)) balance.Ledger = l;

        // Available balance
        var avail = ExtractValue(ofx, "BALAMT", after: "AVAILBAL");
        if (decimal.TryParse(avail, out var a)) balance.Available = a;
        else balance.Available = balance.Ledger;

        // Transactions
        var txMatches = Regex.Matches(ofx, @"<STMTTRN>(.*?)</STMTTRN>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);

        foreach (Match m in txMatches)
        {
            var block = m.Groups[1].Value;
            var tx = new Transaction
            {
                Id      = Extract(block, "FITID"),
                Name    = Extract(block, "NAME"),
                Type    = Extract(block, "TRNTYPE"),
            };

            var dtStr = Extract(block, "DTPOSTED");
            if (dtStr.Length >= 8 && DateTime.TryParseExact(dtStr[..8], "yyyyMMdd",
                null, System.Globalization.DateTimeStyles.None, out var dt))
                tx.Date = dt;

            if (decimal.TryParse(Extract(block, "TRNAMT"), out var amt))
                tx.Amount = amt;

            tx.IsPending = tx.Type.Equals("DEP", StringComparison.OrdinalIgnoreCase) && tx.Date > DateTime.Today
                        || Extract(block, "MEMO").Contains("pending", StringComparison.OrdinalIgnoreCase);

            balance.RecentTransactions.Add(tx);
        }

        // Sort: pending first, then by date desc
        balance.RecentTransactions = balance.RecentTransactions
            .OrderByDescending(t => t.IsPending)
            .ThenByDescending(t => t.Date)
            .Take(20)
            .ToList();

        return balance;
    }

    private static string Extract(string text, string tag) =>
        Regex.Match(text, $@"<{tag}>(.*?)(?:<|$)", RegexOptions.IgnoreCase).Groups[1].Value.Trim();

    private static string ExtractValue(string text, string tag, string after)
    {
        var idx = text.IndexOf($"<{after}>", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return "";
        var sub = text[idx..];
        return Extract(sub, tag);
    }
}
