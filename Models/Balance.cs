namespace BankSync.Models;

public class Balance
{
    public decimal Ledger { get; set; }
    public decimal Available { get; set; }
    public decimal Pending => Ledger - Available;
    public DateTime AsOf { get; set; }
    public List<Transaction> RecentTransactions { get; set; } = new();
}
