using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Scotland2_Mono.Loader;

namespace Scotland2_Mono;

/// <summary>
/// Example usage of the NativePluginLoader class for loading native (unmanaged) DLLs.
/// </summary>
public static class NativePluginLoaderExample
{
    // Example delegate for calling native functions
    // This should match the signature of the native function you want to call
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int MyFunctionDelegate(int a, int b);

    /// <summary>
    /// Example of loading native plugins from a directory.
    /// </summary>
    public static void LoadPluginsExample()
    {
        // Create a plugin loader for a specific directory
        string pluginDir = Path.Combine(Environment.CurrentDirectory, "NativePlugins");
        var loader = new NativePluginLoader();

        // Load all DLL files from the directory
        var loadedCount = loader.LoadPlugins(pluginDir);
        Plugin.Log.Info($"Loaded {loadedCount.Count} native plugins");

        // Get summary
        Plugin.Log.Info(loader.GetSummary());

        // Iterate through all loaded plugins
        foreach (var pluginInfo in loader.GetLoadedPlugins())
        {
            Plugin.Log.Info($"  - {pluginInfo.Id} (Handle: 0x{pluginInfo.LibraryHandle:X})");
        }

        // Check for failed plugins
        foreach (var pluginInfo in loader.GetFailedPlugins())
        {
            Plugin.Log.Warn($"  - Failed: {pluginInfo.Id} - {pluginInfo.ErrorMessage}");
        }

        // Get a specific plugin by name
        var specificPlugin = loader.GetPluginByName("MyNativePlugin");
        if (specificPlugin != null && specificPlugin.IsLoaded)
        {
            Plugin.Log.Info($"Found plugin: {specificPlugin}");
            
            // Show dependencies
            if (specificPlugin.Dependencies!.Count > 0)
            {
                Plugin.Log.Info($"  Dependencies:");
                foreach (var dep in specificPlugin.Dependencies)
                {
                    Plugin.Log.Info($"    - {dep}");
                }
            }
            
            // You can get function pointers from the loaded library
            IntPtr functionPtr = NativeLoaderHelper.GetFunctionPointer(specificPlugin.LibraryHandle, "MyExportedFunction");
            if (functionPtr != IntPtr.Zero)
            {
                Plugin.Log.Info($"Found function at: 0x{functionPtr:X}");
                
                // Example: Call the function using delegates
                // var myFunction = Marshal.GetDelegateForFunctionPointer<MyFunctionDelegate>(functionPtr);
                // myFunction();
            }
        }
    }

    /// <summary>
    /// Example of loading plugins from subdirectories as well.
    /// </summary>
    public static void LoadPluginsRecursiveExample()
    {
        string pluginDir = Path.Combine(Environment.CurrentDirectory, "NativePlugins");
        var loader = new NativePluginLoader();

        // Load all DLL files recursively from subdirectories
        var loadedCount = loader.LoadPlugins(pluginDir, "*.dll", SearchOption.AllDirectories);
        Plugin.Log.Info($"Loaded {loadedCount.Count} native plugins from {pluginDir} and subdirectories");
    }

    /// <summary>
    /// Example of loading a single plugin file.
    /// </summary>
    public static void LoadSinglePluginExample()
    {
        string pluginDir = Path.Combine(Environment.CurrentDirectory, "NativePlugins");
        var loader = new NativePluginLoader();

        // Load a single DLL file
        string dllPath = Path.Combine(pluginDir, "MyNativePlugin.dll");
        var binary = new NativeBinary(dllPath);
        var info = loader.LoadPlugin(binary);

        if (info.IsLoaded)
        {
            Plugin.Log.Info($"Successfully loaded native plugin from {dllPath}");
        }
        else
        {
            Plugin.Log.Error($"Failed to load native plugin from {dllPath}");
        }
    }

    /// <summary>
    /// Example of calling a function from a native plugin using delegates.
    /// </summary>
    public static void CallNativeFunctionExample()
    {
        string pluginDir = Path.Combine(Environment.CurrentDirectory, "NativePlugins");
        var loader = new NativePluginLoader();
        loader.LoadPlugins(pluginDir);

        var plugin = loader.GetPluginByName("MyNativePlugin");
        if (plugin != null && plugin.IsLoaded)
        {
            // Get the function pointer
            IntPtr funcPtr = NativeLoaderHelper.GetFunctionPointer(plugin.LibraryHandle, "MyFunction");
            
            if (funcPtr != IntPtr.Zero)
            {
                // Convert the function pointer to a delegate
                var myFunction = Marshal.GetDelegateForFunctionPointer<MyFunctionDelegate>(funcPtr);
                
                // Call the native function
                int result = myFunction(10, 20);
                Plugin.Log.Info($"Native function returned: {result}");
            }
        }
    }

    /// <summary>
    /// Example of loading plugins in dependency order using topological sort.
    /// </summary>
    public static void LoadPluginsInDependencyOrderExample()
    {
        string pluginDir = Path.Combine(Environment.CurrentDirectory, "NativePlugins");
        var loader = new NativePluginLoader();
        
        // Load all plugins first (without dependencies resolved)
        loader.LoadPlugins(pluginDir);
        
        // Get all successfully loaded plugins
        var loadedPlugins = loader.GetLoadedPlugins().ToList();
        
        Plugin.Log.Info($"Loaded {loadedPlugins.Count} plugins, sorting by dependencies...");
        
        // Validate dependencies first
        if (TopologicalPluginSorter.ValidateDependencies(loadedPlugins, out var errors))
        {
            Plugin.Log.Info("All plugin dependencies are valid!");
        }
        else
        {
            Plugin.Log.Warn("Plugin dependency validation failed:");
            foreach (var error in errors)
            {
                Plugin.Log.Warn($"  - {error}");
            }
        }
        
        // Sort plugins in dependency order
        var sortedPlugins = TopologicalPluginSorter.SortPlugins(loadedPlugins);
        
        Plugin.Log.Info("Plugins in dependency order (dependencies first):");
        for (int i = 0; i < sortedPlugins.Count; i++)
        {
            var plugin = sortedPlugins[i];
            Plugin.Log.Info($"  {i + 1}. {plugin.Name}");
            
            if (plugin.Dependencies!.Count > 0)
            {
                var pluginDeps = plugin.Dependencies
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(d => loadedPlugins.Any(p => p.Id.Equals(d, StringComparison.OrdinalIgnoreCase)))
                    .ToArray();
                
                if (pluginDeps.Any())
                {
                    Plugin.Log.Info($"     Depends on: {string.Join(", ", pluginDeps)}");
                }
            }
        }
        
        // Now you can initialize plugins in the sorted order
        // This ensures dependencies are initialized before dependents
        foreach (var plugin in sortedPlugins)
        {
            Plugin.Log.Debug($"Initializing plugin: {Path.GetFileName(plugin.FilePath)}");
            // Call initialization code here
            // plugin.CallSetup(); // or whatever your initialization method is
        }
    }

    /// <summary>
    /// Example of properly unloading all native plugins when done.
    /// </summary>
    public static void UnloadPluginsExample()
    {
        string pluginDir = Path.Combine(Environment.CurrentDirectory, "NativePlugins");
        var loader = new NativePluginLoader();
        
        loader.LoadPlugins(pluginDir);
        
        // Do work with plugins...
        
        // When done, unload all native libraries
        loader.UnloadAll();
        Plugin.Log.Info("All native plugins unloaded");
    }
}

