namespace BankSync;

static class Program
{
    [STAThread]
    static void Main()
    {
        using var mutex = new System.Threading.Mutex(true, "BankSync_SingleInstance", out var isNew);
        if (!isNew)
        {
            MessageBox.Show("BankSync is already running. Check the system tray.", "BankSync",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}