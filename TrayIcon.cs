namespace BankSync;

// Generates a dollar-sign tray icon at runtime — no .ico file needed
public static class TrayIcon
{
    public static Icon Create(Color fg, Color bg)
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);

        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        // Circle background
        using var bgBrush = new SolidBrush(bg);
        g.FillEllipse(bgBrush, 1, 1, 30, 30);

        // Dollar sign
        using var font = new Font("Segoe UI", 16, FontStyle.Bold, GraphicsUnit.Pixel);
        using var fgBrush = new SolidBrush(fg);
        var text = "$";
        var sz = g.MeasureString(text, font);
        g.DrawString(text, font, fgBrush, (32 - sz.Width) / 2f, (32 - sz.Height) / 2f);

        IntPtr hIcon = bmp.GetHicon();
        var icon = Icon.FromHandle(hIcon);
        return (Icon)icon.Clone();
    }

    public static Icon Default => Create(Color.White, Color.FromArgb(30, 160, 80));
    public static Icon Paused  => Create(Color.White, Color.FromArgb(120, 120, 120));
    public static Icon Syncing => Create(Color.White, Color.FromArgb(60, 120, 210));
    public static Icon Error   => Create(Color.White, Color.FromArgb(200, 60, 60));
}
