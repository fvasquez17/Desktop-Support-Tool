using System.Drawing.Drawing2D;
using System.Drawing.Text;
using DrawColor = System.Drawing.Color;
using DrawBrush = System.Drawing.Brush;
using DrawIcon = System.Drawing.Icon;

namespace DesktopSupportTool.Helpers;

/// <summary>
/// Generates application icons programmatically at runtime — no external .ico files needed.
/// Creates a professional blue gradient icon with a white wrench glyph.
/// Uses fully-qualified System.Drawing types to avoid conflicts with WPF.
/// </summary>
public static class IconGenerator
{
    /// <summary>
    /// Creates a 32x32 icon suitable for the system tray.
    /// </summary>
    public static DrawIcon CreateTrayIcon()
    {
        return CreateIcon(32, isCircle: true);
    }

    /// <summary>
    /// Creates a 64x64 icon for the window title bar.
    /// </summary>
    public static DrawIcon CreateWindowIcon()
    {
        return CreateIcon(64, isCircle: false);
    }

    /// <summary>
    /// Creates a 256x256 high-resolution icon.
    /// </summary>
    public static DrawIcon CreateLargeIcon()
    {
        return CreateIcon(256, isCircle: false);
    }

    private static DrawIcon CreateIcon(int size, bool isCircle)
    {
        using var bmp = new System.Drawing.Bitmap(size, size);
        using var g = System.Drawing.Graphics.FromImage(bmp);

        g.SmoothingMode = SmoothingMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.Clear(DrawColor.Transparent);

        var rect = new System.Drawing.Rectangle(1, 1, size - 2, size - 2);

        // Gradient background
        using var gradientBrush = new LinearGradientBrush(
            new System.Drawing.Point(rect.X, rect.Y),
            new System.Drawing.Point(rect.Right, rect.Bottom),
            DrawColor.FromArgb(59, 130, 246),   // #3B82F6 - bright blue
            DrawColor.FromArgb(37, 99, 235));    // #2563EB - deeper blue

        if (isCircle)
        {
            g.FillEllipse(gradientBrush, rect);
        }
        else
        {
            FillRoundedRectangle(g, gradientBrush, rect, size / 5);
        }

        // White wrench/tool icon using Segoe MDL2 Assets font
        // \uE90F = Repair icon (wrench)
        float fontSize = size * 0.5f;

        // Try Segoe Fluent Icons first (Win11), fallback to Segoe MDL2 Assets (Win10)
        System.Drawing.Font? font = null;
        foreach (var fontName in new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" })
        {
            try
            {
                var testFont = new System.Drawing.Font(fontName, fontSize, System.Drawing.FontStyle.Regular);
                if (testFont.Name.Equals(fontName, StringComparison.OrdinalIgnoreCase))
                {
                    font = testFont;
                    break;
                }
                testFont.Dispose();
            }
            catch { }
        }

        if (font != null)
        {
            using (font)
            using (var whiteBrush = new System.Drawing.SolidBrush(DrawColor.White))
            {
                var sf = new System.Drawing.StringFormat
                {
                    Alignment = System.Drawing.StringAlignment.Center,
                    LineAlignment = System.Drawing.StringAlignment.Center
                };
                g.DrawString("\uE90F", font, whiteBrush, new System.Drawing.RectangleF(0, 0, size, size), sf);
            }
        }
        else
        {
            // Fallback: draw a simple gear shape manually
            DrawSimpleGear(g, size);
        }

        var hIcon = bmp.GetHicon();
        return DrawIcon.FromHandle(hIcon);
    }

    /// <summary>
    /// Fallback gear drawing when Segoe icon fonts are unavailable.
    /// </summary>
    private static void DrawSimpleGear(System.Drawing.Graphics g, int size)
    {
        using var pen = new System.Drawing.Pen(DrawColor.White, size * 0.08f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        float cx = size / 2f;
        float cy = size / 2f;
        float r = size * 0.25f;

        // Draw gear shape
        g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
        float inner = r * 0.5f;
        g.DrawEllipse(pen, cx - inner, cy - inner, inner * 2, inner * 2);

        // Draw gear teeth as lines
        for (int i = 0; i < 6; i++)
        {
            double angle = i * Math.PI / 3;
            float x1 = cx + (float)(r * 0.8 * Math.Cos(angle));
            float y1 = cy + (float)(r * 0.8 * Math.Sin(angle));
            float x2 = cx + (float)(r * 1.3 * Math.Cos(angle));
            float y2 = cy + (float)(r * 1.3 * Math.Sin(angle));
            g.DrawLine(pen, x1, y1, x2, y2);
        }
    }

    private static void FillRoundedRectangle(System.Drawing.Graphics g, DrawBrush brush,
        System.Drawing.Rectangle rect, int radius)
    {
        using var path = new GraphicsPath();
        int d = radius * 2;

        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();

        g.FillPath(brush, path);
    }
}
