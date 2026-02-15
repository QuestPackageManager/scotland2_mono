using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Scotland2_Mono.Loader;

/// <summary>
/// Loads native (unmanaged) DLL plugins from a specified directory and stores their information.
/// </summary>
public class NativePluginLoader
{
    private readonly List<NativePluginInfo> _pluginInfos = new();

    /// <summary>
    /// Gets a read-only collection of all loaded plugin information.
    /// </summary>
    public IReadOnlyList<NativePluginInfo> PluginInfos => _pluginInfos.AsReadOnly();

    /// <summary>
    /// Gets the number of successfully loaded plugins.
    /// </summary>
    public int LoadedCount => _pluginInfos.Count(p => p.IsLoaded);

    /// <summary>
    /// Gets the number of plugins that failed to load.
    /// </summary>
    public int FailedCount => _pluginInfos.Count(p => !p.IsLoaded);

    /// <summary>
    /// Gets the total number of plugins attempted to load.
    /// </summary>
    public int TotalCount => _pluginInfos.Count;

    /// <summary>
    /// Initializes a new instance of the NativePluginLoader.
    /// </summary>
    public NativePluginLoader()
    {
    }

    /// <summary>
    /// Loads all DLL files from the plugin directory.
    /// </summary>
    /// <param name="searchPattern">The search pattern for DLL files (default: "*.dll").</param>
    /// <param name="searchOption">Whether to search subdirectories (default: TopDirectoryOnly).</param>
    /// <param name="pluginDirectory">The directory containing the DLL files to load.</param>
    /// 
    /// <returns>The number of successfully loaded plugins.</returns>
    public IList<NativePluginInfo> LoadPlugins(string pluginDirectory, string searchPattern = "*.dll",
        SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        if (!Directory.Exists(pluginDirectory))
        {
            Plugin.Log.Warn($"Plugin directory does not exist: {pluginDirectory}");
            return [];
        }

        _pluginInfos.Clear();

        string[] dllFiles;
        try
        {
            dllFiles = Directory.GetFiles(pluginDirectory, searchPattern, searchOption);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Failed to enumerate DLL files in {pluginDirectory}: {ex.Message}");
            return [];
        }

        Plugin.Log.Info($"Found {dllFiles.Length} DLL file(s) in {pluginDirectory}");

        var binaries = dllFiles.Select(path => new NativeBinary(path)).ToList();
        var sortedBinaries = TopologicalPluginSorter.SortPlugins(binaries);

        // Load plugins in sorted order
        Plugin.Log.Info($"Sorted {sortedBinaries.Count} binaries");
        foreach (var binary in sortedBinaries)
        {
            Plugin.Log.Debug($"Plugin: {binary.Name}, Dependencies: {string.Join(", ", binary.Dependencies ?? [])}");
        }

        var justLoaded = sortedBinaries.Select(LoadPlugin).ToList();
        _pluginInfos.AddRange(justLoaded);


        Plugin.Log.Info($"Loaded {LoadedCount}/{TotalCount} plugins successfully");
        return justLoaded;
    }

    /// <summary>
    /// Loads a single native DLL file as a plugin.
    /// </summary>
    /// <param name="binary">The path to the DLL file.</param>
    /// <returns>True if the plugin was loaded successfully, false otherwise.</returns>
    public NativePluginInfo LoadPlugin(NativeBinary binary)
    {
        var dllPath = binary.FilePath;
        if (!File.Exists(dllPath))
        {
            var errorInfo = NativePluginInfo.Error(binary, "File not found");
            Plugin.Log.Warn($"DLL file not found: {dllPath}");
            return errorInfo;
        }

        try
        {
            Plugin.Log.Debug($"Loading native plugin from: {dllPath}");

            // Load the native DLL
            var handle = NativeLoaderHelper.LoadNativeLibrary(dllPath);

            if (handle.IsNull)
            {
                var error = NativeLoaderHelper.GetLastError();
                var errorInfo = NativePluginInfo.Error(binary, $"Failed to load native library: {error}");
                Plugin.Log.Error($"Failed to load {Path.GetFileName(dllPath)}: {error}");
                return errorInfo;
            }

            var pluginInfo = NativePluginInfo.Loaded(binary, handle);

            Plugin.Log.Info($"Successfully loaded native plugin: {pluginInfo.Id} (handle: 0x{handle:X})");
            return pluginInfo;
        }
        catch (Exception ex)
        {
            var errorInfo = NativePluginInfo.Error(binary, $"Unexpected error: {ex.Message}");
            Plugin.Log.Error($"Failed to load {Path.GetFileName(dllPath)}: {ex.GetType().Name} - {ex.Message}");
            return errorInfo;
        }
    }


    /// <summary>
    /// Gets plugin info by library name.
    /// </summary>
    /// <param name="name">The name of the library.</param>
    /// <returns>The plugin info if found, null otherwise.</returns>
    public NativePluginInfo? GetPluginByName(string name)
    {
        return _pluginInfos.FirstOrDefault(p =>
            p.Id.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets all successfully loaded plugin infos.
    /// </summary>
    /// <returns>A collection of loaded plugin infos.</returns>
    public IEnumerable<NativePluginInfo> GetLoadedPlugins()
    {
        return _pluginInfos.Where(p => p.IsLoaded);
    }

    /// <summary>
    /// Gets all failed plugin infos.
    /// </summary>
    /// <returns>A collection of failed plugin infos.</returns>
    public IEnumerable<NativePluginInfo> GetFailedPlugins()
    {
        return _pluginInfos.Where(p => !p.IsLoaded);
    }

    /// <summary>
    /// Clears all loaded plugin information without unloading the libraries.
    /// </summary>
    public void Clear()
    {
        _pluginInfos.Clear();
    }

    /// <summary>
    /// Unloads all loaded native libraries and clears the plugin information.
    /// </summary>
    public void UnloadAll()
    {
        foreach (var plugin in _pluginInfos.Where(p => p.IsLoaded))
        {
            if (plugin.LibraryHandle != IntPtr.Zero)
            {
                bool success = NativeLoaderHelper.UnloadNativeLibrary(plugin.LibraryHandle);
                Plugin.Log.Debug($"Unloaded {plugin.Id}: {(success ? "Success" : "Failed")}");
            }
        }

        _pluginInfos.Clear();
    }

    /// <summary>
    /// Gets a summary of the plugin loading results.
    /// </summary>
    /// <returns>A summary string.</returns>
    public string GetSummary()
    {
        return $"Plugins: {LoadedCount} loaded, {FailedCount} failed, {TotalCount} total";
    }
}