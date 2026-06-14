namespace BankSync.Models;

public class Transaction
{
    public string Id { get; set; } = "";
    public DateTime Date { get; set; }
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }
    public bool IsPending { get; set; }
    public string Type { get; set; } = "";
}
