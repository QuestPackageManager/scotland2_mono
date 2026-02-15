using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Semver;

namespace Scotland2_Mono.Loader;

/// <summary>
/// Contains information about a loaded native plugin (after loading).
/// Created from NativeBinary when successfully loaded into memory.
/// </summary>
public class NativePluginInfo
{
    /// <summary>
    /// The underlying binary metadata.
    /// </summary>
    public NativeBinary Binary { get; }

    /// <summary>
    /// The handle to the loaded native library.
    /// </summary>
    public NativeLibraryHandle LibraryHandle { get; }

    /// <summary>
    /// The name of the plugin, typically set during the setup phase by calling the "setup" function in the native library.
    /// </summary>
    public string Id { get; private set; }

    /// <summary>
    /// The version of the plugin, typically set during the setup phase by calling the "setup" function in the native library.
    /// </summary>
    public ulong VersionLong { get; set; }

    /// <summary>
    ///  The semantic version of the plugin, typically set during the setup phase by calling the "setup" function in the native library.
    /// </summary>
    public Semver.SemVersion Version { get; set; }

    /// <summary>
    /// Error message if loading failed, null otherwise.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Whether the plugin was successfully loaded.
    /// </summary>
    public bool IsLoaded { get; }

    /// <summary>
    /// The time when the plugin was loaded.
    /// </summary>
    public DateTime LoadedAt { get; }

    /// <summary>
    /// Dependencies from the binary metadata.
    /// </summary>
    public IReadOnlyList<string>? Dependencies => Binary.Dependencies;

    // Private constructor
    private NativePluginInfo(NativeBinary binary, NativeLibraryHandle handle, bool isLoaded, string? error)
    {
        Binary = binary;
        LibraryHandle = handle;
        IsLoaded = isLoaded;
        ErrorMessage = error;
        LoadedAt = DateTime.UtcNow;
        Version = new SemVersion(0, 0, 0);
        VersionLong = 0;
        Id = Path.GetFileNameWithoutExtension(binary.FilePath);
    }

    /// <summary>
    /// Creates a successful plugin info from a loaded binary.
    /// </summary>
    public static NativePluginInfo Loaded(NativeBinary binary, NativeLibraryHandle handle)
    {
        return new NativePluginInfo(binary, handle, true, null);
    }

    /// <summary>
    /// Creates a failed plugin info for an unloaded binary.
    /// </summary>
    public static NativePluginInfo Error(NativeBinary binary, string errorMessage)
    {
        return new NativePluginInfo(binary, NativeLibraryHandle.Null, false, errorMessage);
    }

    /// <summary>
    /// Calls the "setup" function in the loaded library if it exists.
    /// </summary>
    public void CallSetup()
    {
        if (!IsLoaded || LibraryHandle == NativeLibraryHandle.Null)
            return;

        Plugin.Log.Info($"Attempting to call setup function in {Id}...");
        try
        {
            var setupFuncPtr = NativeLoaderHelper.GetFunctionPointer(LibraryHandle, "setup");
            if (setupFuncPtr == IntPtr.Zero)
            {
                Plugin.Log.Debug($"No setup function found in {Id}.");
                return;
            }

            Plugin.Log.Info($"Calling setup function in {Id}...");
            var setupDelegate = Marshal.GetDelegateForFunctionPointer<ModSetupDelegate>(setupFuncPtr);
            
            CModInfo info = new();
            setupDelegate(ref info);
            Id = info.Id;
            Version = SemVersion.Parse(info.Version);
            VersionLong = info.VersionLong;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Error calling setup in {Id}: {ex.Message}");
        }
    }


    /// <summary>
    /// Calls the "load" function in the loaded library if it exists.
    /// </summary>
    public void CallLoad()
    {
        if (!IsLoaded || LibraryHandle == NativeLibraryHandle.Null)
            return;

        Plugin.Log.Info($"Attempting to load function in {Id}...");
        try
        {
            var loadFuncPtr = NativeLoaderHelper.GetFunctionPointer(LibraryHandle, "load");
            if (loadFuncPtr == IntPtr.Zero)
            {
                Plugin.Log.Debug($"No load function found in {Id}.");
                return;
            }

            Plugin.Log.Info($"Calling load function in {Id}...");
            var loadDelegate = Marshal.GetDelegateForFunctionPointer<ModLoadDelegate>(loadFuncPtr);
            loadDelegate();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Error calling load in {Id}: {ex.Message}");
        }
    }

    /// <summary>
    /// Calls the "late_load" function in the loaded library if it exists.
    /// </summary>
    public void CallLateLoad()
    {
        if (!IsLoaded || LibraryHandle == NativeLibraryHandle.Null)
            return;

        Plugin.Log.Info($"Calling late_load function in {Id}...");
        try
        {
            var lateLoadFuncPtr = NativeLoaderHelper.GetFunctionPointer(LibraryHandle, "late_load");
            if (lateLoadFuncPtr == IntPtr.Zero)
            {
                Plugin.Log.Debug($"No late_load function found in {Id}.");
                return;
            }

            Plugin.Log.Info($"Calling late_load function in {Id}...");
            var lateLoadDelegate = Marshal.GetDelegateForFunctionPointer<ModLateLoadDelegate>(lateLoadFuncPtr);
            lateLoadDelegate();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Error calling late_load in {Id}: {ex.Message}");
        }
    }

    public override string ToString()
    {
        if (IsLoaded)
        {
            return $"{Id} (Handle: 0x{LibraryHandle:X}) - Loaded at {LoadedAt:yyyy-MM-dd HH:mm:ss} ({Binary.FilePath})";
        }

        return $"{Id} (Failed: {ErrorMessage}) - Attempted at {LoadedAt:yyyy-MM-dd HH:mm:ss}";
    }
}