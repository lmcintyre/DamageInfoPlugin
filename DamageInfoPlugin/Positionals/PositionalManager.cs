using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using CsvHelper;

namespace DamageInfoPlugin.Positionals;

public class PositionalManager
{
    private const string SheetUrl = "https://docs.google.com/spreadsheets/d/1UchGyajO-AG6gQwXQT1bsb3sh2ucwOU_vuqT8FRR8Ac/gviz/tq?tqx=out:csv&sheet=main1";
    private readonly HttpClient _client;

    private Dictionary<int, PositionalAction> _actionStore;
    
    public PositionalManager()
    {
        _client = new HttpClient();
        _actionStore = new Dictionary<int, PositionalAction>();
        Get();
        Load();
    }

    public void Reset()
    {
        Get();
        Load();
    }

    private void Get()
    {
        var text = _client.GetAsync(SheetUrl).Result.Content.ReadAsStringAsync().Result;
        if (!File.Exists("positionals.csv") || File.ReadAllText("positionals.csv") != text)
        {
            File.WriteAllText("positionals.csv", text);
        }
    }

    private void Load()
    {
        using var reader = new StreamReader("positionals.csv");
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        foreach (var record in csv.GetRecords<PositionalRecord>())
        {
            if (!_actionStore.TryGetValue(record.Id, out var action)) {
                action = new PositionalAction
                {
                    Id = record.Id,
                    ActionName = record.ActionName,
                    ActionPosition = record.ActionPosition,
                    Positionals = [],
                };
                _actionStore.Add(record.Id, action);
            }

            var parameters = new PositionalParameters
            {
                Percent = record.Percent,
                IsHit = record.IsHit == "TRUE",
                Comment = record.Comment,
            };
            action.Positionals.Add(record.Percent, parameters);
        }
    }

    public bool IsPositionalHit(int actionId, int percent)
    {
        if (!_actionStore.TryGetValue(actionId, out var action)) return false;
        if (!action.Positionals.TryGetValue(percent, out var parameters)) return false;
        return parameters.IsHit;
    }

    public PositionalParameters? GetPositionalParameters(int actionId, int percent)
    {
        if (!_actionStore.TryGetValue(actionId, out var action)) return null;
        return action.Positionals.GetValueOrDefault(percent);
    }
}