using CounterStrikeSharp.API.Core;
using System.Text.Json.Serialization;

namespace RadarColorFix.Configs;

public class BaseConfigs : BasePluginConfig
{

    [JsonPropertyName("EnableDebug")]
    public bool EnableDebug { get; set; } = false;

}