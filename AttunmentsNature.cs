using BepInEx;

namespace AttunmentsNature;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class AttunmentsNature : BaseUnityPlugin
{
    private void Awake()
    {
        Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} has loaded!");

        new AttunmentsEffectsManager(Logger, Config);
        new HeroUnitManager(Logger, Config);
    }
}
