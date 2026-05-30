using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;

using RadarColorFix.Configs;
using RadarColorFix.Services;

namespace RadarColorFix;

[MinimumApiVersion(369)]
public class RadarColorFix : BasePlugin, IPluginConfig<BaseConfigs>
{
	public override string ModuleName => "RadarColorFix";
	public override string ModuleVersion => "1.0.2";
	public override string ModuleAuthor => "luca.uy";
	public override string ModuleDescription => "Fixes the issue of duplicate colors on the radar";

	public required BaseConfigs Config { get; set; }

	private ColorManagementService? _colorService;

	public void OnConfigParsed(BaseConfigs config)
	{
		Config = config;
		Utils.Logger.Config = config;
	}

	public override void Load(bool hotReload)
	{
		_colorService = new ColorManagementService();

		if (hotReload)
		{
			Utils.Logger.LogInfo("Plugin", "Reloading plugin...");
			_colorService.RebuildColorState();
		}

		RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
		RegisterEventHandler<EventRoundStart>(OnRoundStart);
		RegisterListener<Listeners.OnMapEnd>(OnMapEnd);
	}

	[GameEventHandler]
	public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
	{
		_colorService?.ProcessRoundStart();
		return HookResult.Continue;
	}

	[GameEventHandler]
	private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
	{
		if (@event.Userid is not CCSPlayerController player)
			return HookResult.Continue;

		_colorService?.HandlePlayerDisconnect(player);
		return HookResult.Continue;
	}

	private void OnMapEnd()
	{
		_colorService?.Clear();
	}

	public override void Unload(bool hotReload)
	{
		Utils.Logger.LogDebug("Plugin", "Plugin unloaded, clearing data...");
		_colorService?.Clear();
		_colorService = null;
	}
}