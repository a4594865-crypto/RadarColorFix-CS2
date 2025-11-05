using System.Drawing;

namespace RadarColorFix.Constants;

public static class ColorConstants
{
    public const int MinColorIndex = 0;
    public const int MaxColorIndex = 4;

    public static Color GetPlayerTeammateColor(int colorIndex) =>
        colorIndex switch
        {
            1 => Color.FromArgb(50, 255, 0),      // Green
            2 => Color.FromArgb(255, 255, 0),     // Yellow
            3 => Color.FromArgb(255, 132, 0),     // Orange
            4 => Color.FromArgb(255, 0, 255),     // Pink
            0 => Color.FromArgb(0, 187, 255),     // Blue
            _ => Color.FromArgb(0, 0, 0),       // No more colors (error)
        };
}