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
    
    private ConcurrentDictionary<uint, List<ActionEffectInfo>> _store;
    private ulong _lastCleanup;
    
    public ActionEffectStore()
    {
        _store = new();
        _lastCleanup = GetTick();
    }

    private ulong GetTick()
    {
        return (ulong) Environment.TickCount64;
    }

    public void Cleanup()
    {
        if (_store == null) return;

        var tick = GetTick();
        if (tick - _lastCleanup < CleanupInterval) return;

        // FlyTextLog($"pre-cleanup flytext: {futureFlyText.Values.Count}");
        // FlyTextLog($"pre-cleanup text: {text.Count}");
        _lastCleanup = tick;

        var toRemove = new List<uint>();

        foreach (uint key in _store.Keys)
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

        foreach (uint key in toRemove)
            _store.TryRemove(key, out var unused);
            
        // FlyTextLog($"post-cleanup flytext: {futureFlyText.Values.Count}");
        // FlyTextLog($"post-cleanup text: {text.Count}");
    }

    public void Dispose()
    {
        _store.Clear();
        _store = null;
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
            tmpList = new List<ActionEffectInfo>();
            tmpList.Add(info);
            _store.TryAdd(info.value, tmpList);
        }
        StoreLog($"Added effect {info}");
    }

    public void UpdateEffect(uint actionId, uint sourceId, uint targetId, uint value, FlyTextKind logKind)
    {
        StoreLog($"Updating effect {actionId} {sourceId} {targetId} {value} with {logKind}");
        if (!_store.TryGetValue(value, out var list))
            return;

        var effect = list.FirstOrDefault(x => x.actionId == actionId
                                              && x.sourceId == sourceId
                                              && x.targetId == targetId);

        if (!list.Remove(effect))
            return;

        effect.kind = logKind;

        list.Add(effect);
        StoreLog($"Updated effect {effect}");
    }

    public bool TryGetEffect(uint value, FlyTextKind ftKind, out ActionEffectInfo info)
    {
        info = default;
        if (!_store.TryGetValue(value, out var list))
            return false;
        
        var effect = list.FirstOrDefault(x => x.value == value && x.kind == ftKind);

        if (!list.Remove(effect))
            return false;

        info = effect;
        StoreLog($"Retrieved effect {effect}");
        return true;
    }

    private void StoreLog(string msg)
    {
        PluginLog.Debug($"[Store] {msg}");
    }
}