using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace DamageInfoPlugin;

public static class Fools2023
{
    private static List<Tuple<int, string>> _damageTypes;
    private static Random _r = new Random(Environment.TickCount);
    private static Configuration _configuration;
    
    static Fools2023()
    {
        _damageTypes = new List<Tuple<int, string>>
        {
            new(60376, "Bunny"),
            new(65100, "Carrot"),
            new(93550, "Karakul"),
            new(61396, "XIV"),
            new(61432, "MSQ"),
            new(76612, "Musical"),
            new(76972, "Shiba"),
            new(60752, "House"),
            new(64501, "Indifferent"),
            new(62576, "Omni-class"),
            new(61582, "GM"),
            new(61581, "Yoshi-P"),
            new(61573, "Sprout"),
            new(61511, "AFK"),
            new(60346, "Miner"),
            new(76979, "Penguin"),
            new(4839, "Grebuloff"),
            new(900, "BUDDY"),
            new(61545, "RP"),
            new(66400, "APP-EAL"),
            new(26559, "Chest Coffer"),
            new(80178, "Ragnarok"),
            new(60436, "Bedge"),
            new(61503, "90k'd"),
            new(65918, "Crab"),
            new(10402, "Cleric Stance"),
            new(61832, "Ultimate"),
            new(76725, "Namazu"),
        };
    }

    public static void SetConfiguration(Configuration config)
    {
        _configuration = config;
    }

    public static bool IsFools()
    {
        var date = DateTime.Now;
        return date is { Year: 2023, Month: 4, Day: 1 };
    }

    public static (int, SeString) GetRareDamageType(int icon, SeString text)
    {
        var trigger = _r.Next(_configuration.FoolsFrequency);
        if (trigger != 0) return (icon, text);

        var index = _r.Next(_damageTypes.Count);
        var result = _damageTypes[index];
        var str = new SeString(new TextPayload($"{result.Item2} "));
        str.Append(text.Payloads);
        return (result.Item1, str);
    }
    
    
}