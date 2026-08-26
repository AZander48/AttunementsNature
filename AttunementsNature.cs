using BepInEx;

namespace AttunementsNature;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class AttunementsNature : BaseUnityPlugin
{
    private void Awake()
    {
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} has loaded!");

        new AttunementsEffectsManager(Logger, Config);
        new HeroUnitManager(Logger, Config);
        new UnstableAttunmentManager(Logger, Config);
        new AttunmentUnitManager(Logger, Config);
    }
}
