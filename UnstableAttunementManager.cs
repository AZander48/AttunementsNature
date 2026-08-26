using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using System;

namespace AttunementsNature;

public class UnstableAttunmentManager
{
    public static UnstableAttunmentManager? Instance { get; private set; }

    private ManualLogSource _log = null!;
    private ConfigEntry<bool> _debug = null!;

    readonly UnstableAttunementstate _state = new();

    public UnstableAttunmentManager(ManualLogSource log, ConfigFile config)
    {
        Instance = this;
        _log = log;
        InitConfigEntries(config);
    }

    public void Reset()
    {
        _state.Value = 0;
        _state.BurnoutActive = false;
        _state.Element = AttunmentElement.Burn;
    }

    public void AddValue(int amount)
    {
        if (_state.BurnoutActive) return; // or different rules
        _state.Value = Math.Min(_state.Max, _state.Value + amount);
        if (_state.Value >= _state.Max)
            StartBurnout();
    }

    

    private void StartBurnout() { _state.BurnoutActive = true; /* empower, etc. */ }

    private void InitConfigEntries(ConfigFile config)
    {
        _debug = config.Bind("Unstable Attunment", "Debug", false, "Enable or disable debug logging");
    }
}