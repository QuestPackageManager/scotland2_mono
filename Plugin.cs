using System.IO;
using IPA;
using IPA.Config.Stores;
using IPA.Loader;
using IPA.Utilities;
using JetBrains.Annotations;
using Scotland2_Mono.Loader;
using IpaLogger = IPA.Logging.Logger;
using IpaConfig = IPA.Config.Config;

namespace Scotland2_Mono;

[Plugin(RuntimeOptions.DynamicInit), NoEnableDisable]
internal class Plugin
{
    internal static IpaLogger Log { get; private set; } = null!;

    private readonly NativePluginLoader _nativeLoader = new();
    private readonly NativePluginLoader _pluginLoader = new();
    
    private readonly string _pluginDirectory;
    private readonly string _libraryDirectory;
    
    
    // Methods with [Init] are called when the plugin is first loaded by IPA.
    // All the parameters are provided by IPA and are optional.
    // The constructor is called before any method with [Init]. Only use [Init] with one constructor.
    [Init]
    public Plugin(IpaLogger ipaLogger, IpaConfig ipaConfig, PluginMetadata pluginMetadata)
    {
        Log = ipaLogger;
        
        // load in UnityDir/Native/Plugins
        _libraryDirectory = (Path.Combine(UnityGame.InstallPath, "Native", "Libs"));
        _pluginDirectory = (Path.Combine(UnityGame.InstallPath, "Native", "Plugins"));


        // Creates an instance of PluginConfig used by IPA to load and store config values
        var pluginConfig = ipaConfig.Generated<PluginConfig>();

        Log.Info($"{pluginMetadata.Name} {pluginMetadata.HVersion} loading libraries {_libraryDirectory}");
        _nativeLoader.LoadPlugins(_libraryDirectory);
    }
    
    [Init]
    public void OnApplicationStart()
    {
        Log.Info($"Loading plugins {_pluginDirectory}");
        _pluginLoader.LoadPlugins(_pluginDirectory);
        CallSetup();
    }

    [OnEnable]
    [UsedImplicitly]
    public void OnEnable()
    {
        Log.Info("Plugin enabled, loading plugins...");

        Log.Info("Plugin loaded");

        // Call load and late load for all plugins after the main plugin is enabled,
        // so that they can safely call IPA APIs and interact with the game.
        CallLoad();
        CallLateLoad();
        
    }

    private void CallSetup()
    {
        foreach (var plugin in _pluginLoader.PluginInfos)
        {
            plugin.CallSetup();
        }
    }
    
    private void CallLoad()
    {
        foreach (var plugin in _pluginLoader.PluginInfos)
        {
            plugin.CallLoad();
        }
    }
    
    private void CallLateLoad()
    {
        foreach (var plugin in _pluginLoader.PluginInfos)
        {
            plugin.CallLateLoad();
        }
    }
    

}