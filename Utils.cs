namespace RadarColorFix;

public class Utils
{
    public static BaseConfigs? Config { get; set; }

    public static void DebugMessage(string message)
    {
        if (Config?.EnableDebug != true) return;
        Console.WriteLine($"================================= [ RadarColorFix ] =================================");
        Console.WriteLine(message);
        Console.WriteLine("=========================================================================================");
    }

}