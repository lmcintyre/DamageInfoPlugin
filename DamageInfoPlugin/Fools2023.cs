using System;
using System.Collections.Generic;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace DamageInfoPlugin;

public static class Fools2023
{
    // bunny, 60376
    // karakul, 93550
    // xiv, 61396
    // msq, 61432
    // music, 76612
    // shiba, 76972
    // house, 60752
    // indifferent, 76651
    // omniclass, 62576
    // GM 61582
    // yoshida 61581
    // sprout 61572
    private static List<Tuple<int, string>> _damageTypes;
    private static Random _r = new Random(Environment.TickCount);
    
    static Fools2023()
    {
        _damageTypes = new List<Tuple<int, string>>
        {
            new(60376, "Bunny"),
            new(93550, "Karakul")
            // karakul, 93550
            // xiv, 61396
            // msq, 61432
            // music, 76612
            // shiba, 76972
            // house, 60752
            // indifferent, 76651
            // omniclass, 62576
            // GM 61582
            // yoshida 61581
            // sprout 61572
        };
    }

    public static bool IsFools()
    {
        var date = DateTime.Now;
        return date is { Year: 2023, Month: 4, Day: 1 };
    }

    public static (int, SeString) GetRareDamageType(int icon, SeString text)
    {
        var trigger = _r.Next(3);
        if (trigger != 0) return (icon, text);

        var index = _r.Next(_damageTypes.Count);
        var result = _damageTypes[index];
        var str = new SeString(new TextPayload($"{result.Item2} "));
        str.Append(text.Payloads);
        return (result.Item1, str);
    }
    
    
}