namespace BankSync.Helpers;

public enum ThemeMode { Dark, Light }

public static class AppTheme
{
    public static ThemeMode Mode { get; private set; } = ThemeMode.Dark;
    public static bool IsDark => Mode == ThemeMode.Dark;

    public static void Apply(string? theme) =>
        Mode = theme?.ToLower() == "light" ? ThemeMode.Light : ThemeMode.Dark;

    // ── Surfaces ──────────────────────────────────────────────────────
    public static Color FormBack  => IsDark ? Color.FromArgb(35, 35, 35)  : Color.FromArgb(242, 242, 242);
    public static Color PanelBack => IsDark ? Color.FromArgb(40, 40, 40)  : Color.FromArgb(255, 255, 255);
    public static Color InputBack => IsDark ? Color.FromArgb(55, 55, 55)  : Color.FromArgb(255, 255, 255);
    public static Color NavBack   => IsDark ? Color.FromArgb(30, 30, 30)  : Color.FromArgb(215, 215, 215);
    public static Color ListBack  => IsDark ? Color.FromArgb(45, 45, 45)  : Color.FromArgb(255, 255, 255);
    public static Color TabBack   => IsDark ? Color.FromArgb(45, 45, 45)  : Color.FromArgb(225, 225, 225);
    public static Color Separator => IsDark ? Color.FromArgb(70, 70, 70)  : Color.FromArgb(205, 205, 205);

    // ── Text ─────────────────────────────────────────────────────────
    public static Color TextPrimary   => IsDark ? Color.White                   : Color.FromArgb(20,  20,  20);
    public static Color TextSecondary => IsDark ? Color.FromArgb(200, 200, 200) : Color.FromArgb(70,  70,  70);
    public static Color TextMuted     => IsDark ? Color.FromArgb(170, 170, 170) : Color.FromArgb(110, 110, 110);
    public static Color TextDisabled  => IsDark ? Color.Gray                    : Color.FromArgb(155, 155, 155);

    // ── Semantic ─────────────────────────────────────────────────────
    public static Color Positive => IsDark ? Color.FromArgb(100, 220, 100) : Color.FromArgb(22,  130, 50);
    public static Color Negative => IsDark ? Color.FromArgb(255, 120, 120) : Color.FromArgb(180, 40,  40);
    public static Color Warning  => IsDark ? Color.FromArgb(255, 180,  80) : Color.FromArgb(160, 110,  0);
    public static Color BlueLink => IsDark ? Color.FromArgb(130, 190, 255) : Color.FromArgb(30,  100, 200);

    // ── Buttons (accent colors — always white text) ───────────────────
    public static Color BtnGreen  => Color.FromArgb(30,  140,  70);
    public static Color BtnBlue   => Color.FromArgb(50,  100, 160);
    public static Color BtnRed    => Color.FromArgb(160,  50,  50);
    public static Color BtnOrange => Color.FromArgb(160,  80,  30);
    public static Color BtnGray   => Color.FromArgb(70,   70,  70);

    // ── Context menu / tray ──────────────────────────────────────────
    public static Color MenuBack     => IsDark ? Color.FromArgb(40, 40, 40)  : Color.FromArgb(245, 245, 245);
    public static Color MenuBorder   => IsDark ? Color.FromArgb(60, 60, 60)  : Color.FromArgb(180, 180, 180);
    public static Color MenuSep      => IsDark ? Color.FromArgb(70, 70, 70)  : Color.FromArgb(190, 190, 190);
    public static Color MenuSelected => IsDark ? Color.FromArgb(60, 60, 60)  : Color.FromArgb(200, 215, 235);
    public static Color MenuText     => IsDark ? Color.White                  : Color.FromArgb(20,  20,  20);
    public static Color MenuTextDim  => IsDark ? Color.Gray                   : Color.FromArgb(150, 150, 150);

    // ── Balance popup sync animation ─────────────────────────────────
    public static Color SyncBlueA => Color.FromArgb(60,  150, 255);
    public static Color SyncBlueB => Color.FromArgb(130, 200, 255);
    public static Color SyncIdle  => IsDark ? Color.FromArgb(120, 120, 120) : Color.FromArgb(160, 160, 160);
}
