using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Utils;
using System.Drawing;

namespace RadarColorFix;

[MinimumApiVersion(300)]
public class RadarColorFix : BasePlugin, IPluginConfig<BaseConfigs>
{
	public override string ModuleName => "RadarColorFix";
	public override string ModuleVersion => "1.0.0";
	public override string ModuleAuthor => "luca.uy";
	public override string ModuleDescription => "Fixes the issue of duplicate colors on the radar";

	public required BaseConfigs Config { get; set; }

	private readonly Dictionary<int, HashSet<int>> teamUsedColors = new();
	private readonly Dictionary<ulong, PlayerColorInfo> playerColorMap = new();

	private class PlayerColorInfo
	{
		public int Team { get; set; }
		public int Color { get; set; }
	}

	public void OnConfigParsed(BaseConfigs config)
	{
		Config = config;
		Utils.Config = config;
	}

	public override void Load(bool hotReload)
	{
		if (hotReload)
		{
			Utils.DebugMessage("Reloading plugin...");
			RebuildColorState();
		}

		RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
		RegisterEventHandler<EventRoundStart>(OnRoundStart);
		RegisterListener<Listeners.OnMapEnd>(OnMapEnd);
	}

	private void RebuildColorState()
	{
		teamUsedColors.Clear();
		playerColorMap.Clear();
		Utilities.GetPlayers().Where(player => player.IsValid && !player.IsBot).ToList().ForEach(TrackPlayerColor);
	}

	private void TrackPlayerColor(CCSPlayerController player)
	{
		var steamId = player.SteamID;

		if (playerColorMap.TryGetValue(steamId, out var previous))
		{
			if (teamUsedColors.TryGetValue(previous.Team, out var oldSet))
			{
				oldSet.Remove(previous.Color);
			}
			playerColorMap.Remove(steamId);
		}

		bool isValidTeam = player.Team == CsTeam.Terrorist || player.Team == CsTeam.CounterTerrorist;
		bool isValidColor = player.CompTeammateColor >= 0 && player.CompTeammateColor <= 4;

		if (isValidTeam && isValidColor)
		{
			if (!teamUsedColors.TryGetValue(player.TeamNum, out var usedColors))
			{
				usedColors = new HashSet<int>();
				teamUsedColors[player.TeamNum] = usedColors;
			}

			usedColors.Add(player.CompTeammateColor);

			playerColorMap[steamId] = new PlayerColorInfo
			{
				Team = player.TeamNum,
				Color = player.CompTeammateColor
			};

			Utils.DebugMessage($"Player '{player.PlayerName}' (Team {player.TeamNum}) has color {player.CompTeammateColor}");
		}
	}

	[GameEventHandler]
	public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
	{
		Utils.DebugMessage("=== ROUND START ===");

		var players = Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot && IsValidTeam(p.Team)).ToList();

		Utils.DebugMessage($"Connected players: {players.Count}");
		foreach (var player in players)
		{
			Utils.DebugMessage($"{player.PlayerName} (Team {player.TeamNum}) - Current color: {player.CompTeammateColor}");
		}

		foreach (var teamGroup in players.GroupBy(p => p.TeamNum))
		{
			var usedColors = new HashSet<int>();
			var team = teamGroup.Key;

			Utils.DebugMessage($"Processing team {team} with {teamGroup.Count()} players");

			var conflicts = new Dictionary<int, List<CCSPlayerController>>();

			foreach (var player in teamGroup)
			{
				int color = player.CompTeammateColor;

				if (color < 0 || color > 4)
				{
					color = 0;
					player.CompTeammateColor = color;
					player.TeammatePreferredColor = color;
				}

				if (usedColors.Contains(color))
				{
					Utils.DebugMessage($"Color conflict detected: {player.PlayerName} has color {color} which is already in use");

					if (!conflicts.ContainsKey(color))
						conflicts[color] = new List<CCSPlayerController>();

					conflicts[color].Add(player);
				}
				else
				{
					usedColors.Add(color);
					Utils.DebugMessage($"Color {color} assigned to {player.PlayerName}");
				}
			}

			foreach (var conflict in conflicts)
			{
				var color = conflict.Key;
				var playersInConflict = conflict.Value;

				Utils.DebugMessage($"Resolving conflict for color {color} with {playersInConflict.Count} players");

				foreach (var player in playersInConflict)
				{
					var availableColors = Enumerable.Range(0, 5).Where(c => !usedColors.Contains(c)).ToList();
					Utils.DebugMessage($"Available colors for {player.PlayerName}: {string.Join(",", availableColors)}");

					if (availableColors.Count == 0)
					{
						Utils.DebugMessage($"No colors available for {player.PlayerName}, keeping color {color}");
						continue;
					}

					int newColor = availableColors[0];
					usedColors.Add(newColor);

					player.CompTeammateColor = newColor;
					player.TeammatePreferredColor = newColor;

					playerColorMap[player.SteamID] = new PlayerColorInfo
					{
						Team = player.TeamNum,
						Color = newColor
					};

					Utils.DebugMessage($"Color corrected for '{player.PlayerName}' -> {newColor} (Team {team})");
				}
			}

			teamUsedColors[team] = usedColors;
			Utils.DebugMessage($"Final color state for team {team}: {string.Join(",", usedColors)}");
		}

		Utils.DebugMessage("=== FINAL COLOR STATE ===");
		foreach (var player in players)
		{
			Utils.DebugMessage($"{player.PlayerName} (Team {player.TeamNum}) - Final color: {player.CompTeammateColor}");
		}

		return HookResult.Continue;
	}

	private bool IsValidTeam(CsTeam team) => team == CsTeam.Terrorist || team == CsTeam.CounterTerrorist;

	[GameEventHandler]
	private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
	{
		if (@event.Userid is not CCSPlayerController player)
			return HookResult.Continue;

		HandlePlayerDisconnect(player);
		return HookResult.Continue;
	}

	private void HandlePlayerDisconnect(CCSPlayerController player)
	{
		var steamId = player.SteamID;
		var playerName = player.PlayerName ?? "Unknown";

		Utils.DebugMessage($"Player disconnected: {playerName} (SteamID: {steamId})");

		if (playerColorMap.TryGetValue(steamId, out var infoColor) && teamUsedColors.TryGetValue(infoColor.Team, out var usedSet) && usedSet.Remove(infoColor.Color))
		{
			Utils.DebugMessage($"Color {infoColor.Color} released from '{playerName}' (team {infoColor.Team}). Available now: {GetAvailableColorsString(infoColor.Team)}");
		}

		playerColorMap.Remove(steamId);
	}

	private string GetAvailableColorsString(int team) => teamUsedColors.TryGetValue(team, out var usedColors)
		? string.Join(",", Enumerable.Range(0, 5).Where(c => !usedColors.Contains(c))) : "0,1,2,3,4";

	public static Color GetPlayerTeammateColor(CCSPlayerController playerController) =>
		playerController.CompTeammateColor switch
		{
			1 => Color.FromArgb(50, 255, 0),
			2 => Color.FromArgb(255, 255, 0),
			3 => Color.FromArgb(255, 132, 0),
			4 => Color.FromArgb(255, 0, 255),
			0 => Color.FromArgb(0, 187, 255),
			_ => Color.FromArgb(255, 0, 0),
		};

	private void OnMapEnd()
	{
		teamUsedColors.Clear();
		playerColorMap.Clear();
	}
}