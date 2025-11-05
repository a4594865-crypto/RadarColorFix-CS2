using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

using RadarColorFix.Models;
using RadarColorFix.Utils;

namespace RadarColorFix.Services;

public class ColorManagementService
{
    private readonly Dictionary<int, HashSet<int>> _teamUsedColors = new();
    private readonly Dictionary<ulong, PlayerColorInfo> _playerColorMap = new();

    public void RebuildColorState()
    {
        Clear();
        Utilities.GetPlayers().Where(player => player.IsValid && !player.IsBot).ToList().ForEach(TrackPlayerColor);
    }

    public void ProcessRoundStart()
    {
        var players = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && TeamValidator.IsValidTeam(p.Team)).ToList();

        Logger.LogDebug("RoundStart", $"Connected players: {players.Count}");

        LogCurrentPlayerStates(players);
        ProcessTeamColors(players);
        LogFinalColorStates(players);
    }

    public void HandlePlayerDisconnect(CCSPlayerController player)
    {
        var steamId = player.SteamID;
        var playerName = player.PlayerName ?? "Unknown";

        Logger.LogDebug("Disconnect", $"Player disconnected: {playerName} (SteamID: {steamId})");

        if (_playerColorMap.TryGetValue(steamId, out var infoColor) && _teamUsedColors.TryGetValue(infoColor.Team, out var usedSet) && usedSet.Remove(infoColor.Color))
        {
            Logger.LogDebug("Disconnect", $"Color {infoColor.Color} released from '{playerName}' (team {infoColor.Team}). " + $"Available now: {GetAvailableColorsString(infoColor.Team)}");
        }

        _playerColorMap.Remove(steamId);
    }

    public void Clear()
    {
        _teamUsedColors.Clear();
        _playerColorMap.Clear();
    }

    private void TrackPlayerColor(CCSPlayerController player)
    {
        var steamId = player.SteamID;

        if (_playerColorMap.TryGetValue(steamId, out var previous))
        {
            if (_teamUsedColors.TryGetValue(previous.Team, out var oldSet))
            {
                oldSet.Remove(previous.Color);
            }
            _playerColorMap.Remove(steamId);
        }

        bool isValidTeam = TeamValidator.IsValidTeam(player.Team);
        bool isValidColor = TeamValidator.IsValidColor(player.CompTeammateColor);

        if (isValidTeam && isValidColor)
        {
            if (!_teamUsedColors.TryGetValue(player.TeamNum, out var usedColors))
            {
                usedColors = new HashSet<int>();
                _teamUsedColors[player.TeamNum] = usedColors;
            }

            usedColors.Add(player.CompTeammateColor);

            _playerColorMap[steamId] = new PlayerColorInfo
            {
                Team = player.TeamNum,
                Color = player.CompTeammateColor
            };

            Logger.LogDebug("TrackColor", $"Player '{player.PlayerName}' (Team {player.TeamNum}) has color {player.CompTeammateColor}");
        }
    }

    private void ProcessTeamColors(List<CCSPlayerController> players)
    {
        foreach (var teamGroup in players.GroupBy(p => p.TeamNum))
        {
            var team = teamGroup.Key;
            var usedColors = new HashSet<int>();

            Logger.LogDebug("TeamProcess", $"Processing team {team} with {teamGroup.Count()} players");

            var conflicts = DetectColorConflicts(teamGroup, usedColors);
            ResolveColorConflicts(conflicts, usedColors, team);

            _teamUsedColors[team] = usedColors;
            Logger.LogDebug("TeamProcess", $"Final color state for team {team}: {string.Join(",", usedColors)}");
        }
    }

    private Dictionary<int, List<CCSPlayerController>> DetectColorConflicts(IGrouping<byte, CCSPlayerController> teamGroup, HashSet<int> usedColors)
    {
        var conflicts = new Dictionary<int, List<CCSPlayerController>>();
        foreach (var player in teamGroup)
        {
            int color = NormalizePlayerColor(player);
            if (usedColors.Contains(color))
            {
                Logger.LogDebug("Conflict", $"Color conflict detected: {player.PlayerName} has color {color} which is already in use");

                if (!conflicts.ContainsKey(color))
                    conflicts[color] = new List<CCSPlayerController>();

                conflicts[color].Add(player);
            }
            else
            {
                usedColors.Add(color);
                Logger.LogDebug("Conflict", $"Color {color} assigned to {player.PlayerName}");
            }
        }

        return conflicts;
    }

    private int NormalizePlayerColor(CCSPlayerController player)
    {
        int color = player.CompTeammateColor;
        if (!TeamValidator.IsValidColor(color))
        {
            color = 0;
            player.CompTeammateColor = color;
            player.TeammatePreferredColor = color;
        }

        return color;
    }

    private void ResolveColorConflicts(Dictionary<int, List<CCSPlayerController>> conflicts, HashSet<int> usedColors, int team)
    {
        foreach (var conflict in conflicts)
        {
            var color = conflict.Key;
            var playersInConflict = conflict.Value;

            Logger.LogDebug("Resolve", $"Resolving conflict for color {color} with {playersInConflict.Count} players");

            foreach (var player in playersInConflict)
            {
                AssignNewColorToPlayer(player, usedColors, team);
            }
        }
    }

    private void AssignNewColorToPlayer(CCSPlayerController player, HashSet<int> usedColors, int team)
    {
        var availableColors = GetAvailableColors(usedColors);

        Logger.LogDebug("Resolve", $"Available colors for {player.PlayerName}: {string.Join(",", availableColors)}");

        if (availableColors.Count == 0)
        {
            Logger.LogWarning("Resolve", $"No colors available for {player.PlayerName}, keeping current color");
            return;
        }

        int newColor = availableColors[0];
        usedColors.Add(newColor);

        player.CompTeammateColor = newColor;
        player.TeammatePreferredColor = newColor;

        _playerColorMap[player.SteamID] = new PlayerColorInfo
        {
            Team = player.TeamNum,
            Color = newColor
        };

        Logger.LogInfo("ColorAssigned", $"Color corrected for '{player.PlayerName}' -> {newColor} (Team {team})");
    }

    private List<int> GetAvailableColors(HashSet<int> usedColors) =>
        Enumerable.Range(Constants.ColorConstants.MinColorIndex, Constants.ColorConstants.MaxColorIndex + 1).Where(c => !usedColors.Contains(c)).ToList();

    private string GetAvailableColorsString(int team) =>
        _teamUsedColors.TryGetValue(team, out var usedColors) ? string.Join(",", GetAvailableColors(usedColors)) : "0,1,2,3,4";

    private void LogCurrentPlayerStates(List<CCSPlayerController> players)
    {
        foreach (var player in players)
        {
            Logger.LogDebug("RoundStart", $"{player.PlayerName} (Team {player.TeamNum}) - Current color: {player.CompTeammateColor}");
        }
    }

    private void LogFinalColorStates(List<CCSPlayerController> players)
    {
        foreach (var player in players)
        {
            Logger.LogDebug("RoundStart", $"{player.PlayerName} (Team {player.TeamNum}) - Final color: {player.CompTeammateColor}");
        }
    }
}