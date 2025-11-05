using CounterStrikeSharp.API.Modules.Utils;

namespace RadarColorFix.Utils;

public static class TeamValidator
{
    public static bool IsValidTeam(CsTeam team) =>
        team == CsTeam.Terrorist || team == CsTeam.CounterTerrorist;

    public static bool IsValidColor(int color) =>
        color >= Constants.ColorConstants.MinColorIndex &&
        color <= Constants.ColorConstants.MaxColorIndex;
}