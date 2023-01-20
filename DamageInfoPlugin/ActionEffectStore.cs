using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Gui.FlyText;
using Dalamud.Logging;

namespace DamageInfoPlugin;

public class ActionEffectStore
{
    private ulong CleanupInterval = 30000;

    private readonly ConcurrentDictionary<uint, List<ActionEffectInfo>> _store;
    private readonly Configuration _config;
    private ulong _lastCleanup;

    public ActionEffectStore(Configuration config)
    {
        _config = config;
        _store = new ConcurrentDictionary<uint, List<ActionEffectInfo>>();
        _lastCleanup = GetTick();
    }

    private ulong GetTick()
    {
        return (ulong)Environment.TickCount64;
    }

    public void Cleanup()
    {
        if (_store == null) return;

        var tick = GetTick();
        if (tick - _lastCleanup < CleanupInterval) return;

        StoreLog($"pre-cleanup: {_store.Values.Count}");
        _lastCleanup = tick;

        var toRemove = new List<uint>();

        foreach (var key in _store.Keys)
        {
            if (!_store.TryGetValue(key, out var list)) continue;
            if (list == null)
            {
                toRemove.Add(key);
                continue;
            }

            for (int i = 0; i < list.Count; i++)
            {
                var diff = tick - list[i].tick;
                if (diff <= 10000) continue;
                list.Remove(list[i]);
                i--;
            }

            if (list.Count == 0)
                toRemove.Add(key);
        }

        foreach (var key in toRemove)
            _store.TryRemove(key, out var unused);

        StoreLog($"post-cleanup: {_store.Values.Count}");
    }

    public void Dispose()
    {
        _store.Clear();
    }

    public void AddEffect(ActionEffectInfo info)
    {
        info.tick = GetTick();

        if (_store.TryGetValue(info.value, out var tmpList))
        {
            tmpList.Add(info);
        }
        else
        {
            tmpList = new List<ActionEffectInfo> { info };
            _store.TryAdd(info.value, tmpList);
        }

        StoreLog($"Added effect {info}");
    }

    public void UpdateEffect(uint actionId, uint sourceId, uint targetId, uint value, uint damageType, FlyTextKind logKind)
    {
        StoreLog($"Updating effect {actionId} {sourceId} {targetId} {value} {damageType} with {logKind}...");
        if (!_store.TryGetValue(value, out var list))
            return;
            
        var effect = list.FirstOrDefault(x => x.step == ActionStep.Effect 
                                              && x.actionId == actionId
                                              && x.sourceId == sourceId
                                              && x.targetId == targetId
                                              && (uint)x.damageType == damageType);

        if (!list.Remove(effect))
            return;

        effect.kind = logKind;
        effect.step = ActionStep.Screenlog;

        list.Add(effect);
        StoreLog($"Updated effect {effect}");
    }

    public bool TryGetEffect(uint value, DamageType damageType, FlyTextKind targetKind, uint charaId, List<uint> petIds, out ActionEffectInfo info)
    {
        StoreLog($"Looking for effect {value} {damageType} {targetKind} {charaId}...");
        info = default;
        if (!_store.TryGetValue(value, out var list))
            return false;

        var effect = list.FirstOrDefault(x => x.value == value
                                              && x.damageType == damageType
                                              && x.step == ActionStep.Screenlog
                                              && KindCheck(x, targetKind)
                                              && TargetCheck(x, charaId, petIds));

        if (!list.Remove(effect))
            return false;

        info = effect;
        StoreLog($"Retrieved effect {effect}");
        return true;
    }

    private bool KindCheck(ActionEffectInfo info, FlyTextKind targetKind)
    {
        var result = targetKind == info.kind;
        // Screenlog will log misses from enemies as Named/Miss, but they will show up to us as Named/Dodge
        if (!result)
            return targetKind is FlyTextKind.NamedDodge or FlyTextKind.Dodge && info.kind is FlyTextKind.NamedMiss or FlyTextKind.Miss;
        return true;
    }

    private bool TargetCheck(ActionEffectInfo info, uint charaId, List<uint> petIds)
    {
        return info.sourceId == charaId || info.targetId == charaId || petIds.Contains(info.sourceId);
    }

    private void StoreLog(string msg)
    {
        if (_config.DebugLogEnabled)
            PluginLog.Debug($"[Store] {msg}");
    }
}