using System;
using System.Runtime.InteropServices;
using RGiesecke.DllExport;

namespace Scotland2_Mono;

#region Enums

/// <summary>
/// Mod information structure containing ID, version, and version long.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct CModInfo
{
    [MarshalAs(UnmanagedType.LPStr)]
    public string Id;

    [MarshalAs(UnmanagedType.LPStr)]
    public string Version;

    public ulong VersionLong;
}

/// <summary>
/// Enum for matching mods by different criteria.
/// </summary>
public enum CMatchType
{
    MatchType_Strict,
    MatchType_IdOnly,
    MatchType_IdVersion,
    MatchType_IdVersionLong,
    MatchType_ObjectName, // library binary name e.g libsl2.so
}

/// <summary>
/// Enum for the current loading phase.
/// </summary>
public enum CLoadPhase
{
    LoadPhase_None,
    LoadPhase_Libs,
    LoadPhase_EarlyMods,
    LoadPhase_Mods,
}

/// <summary>
/// Enum for load result status.
/// </summary>
public enum CLoadResultEnum
{
    LoadResult_NotFound,
    LoadResult_Failed,
    MatchType_Loaded,
    // LoadResult_AlreadyLoaded,
}

#endregion

#region Structs

/// <summary>
/// Information about a mod loading failure.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct CLoadFailed
{
    [MarshalAs(UnmanagedType.LPStr)]
    public string Failure;

    [MarshalAs(UnmanagedType.LPStr)]
    public string Path;
}

/// <summary>
/// Result of a successfully loaded mod.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct CModResult
{
    public CModInfo Info;

    [MarshalAs(UnmanagedType.LPStr)]
    public string Path;

    public IntPtr Handle;
}

/// <summary>
/// Array of mod results.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct CModResults
{
    public IntPtr Array;
    public ulong Size;
}

/// <summary>
/// Result of a load operation with union-like behavior.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct CLoadResult
{
    public CLoadResultEnum Result;

    // Union field - interpret based on Result
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
    public string UnionData; // This is a simplified representation
}

/// <summary>
/// Array of load results.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct CLoadResults
{
    public IntPtr Array;
    public ulong Size;
}

#endregion

#region Delegate Callbacks

/// <summary>
/// Delegate for setup function that gets called when a mod is loaded.
/// The modloader will call this with the mod info reference.
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void ModSetupDelegate(ref CModInfo modInfo);

/// <summary>
/// Delegate for mod initialization/load function.
/// Called after the mod is loaded into memory.
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void ModLoadDelegate();

/// <summary>
/// Delegate for late load function.
/// Called after all mods have been loaded.
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void ModLateLoadDelegate();

/// <summary>
/// Delegate for mod unload function.
/// Called when a mod is being unloaded.
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void ModUnloadDelegate();

#endregion

#region Exported Callbacks

/// <summary>
/// Exported C# callbacks that the native modloader will invoke.
/// These are delegate instances that can be marshaled to native code as function pointers.
/// </summary>
public static class ModloaderCallbacks
{
    /// <summary>
    /// Called by modloader when setup phase begins.
    /// </summary>
    public static Action OnModloaderSetup = () =>
    {
        Plugin.Log.Info("Modloader setup phase started");
    };

    /// <summary>
    /// Called by modloader when early mods phase begins.
    /// </summary>
    public static Action OnEarlyModsLoaded = () =>
    {
        Plugin.Log.Info("Early mods loaded");
    };

    /// <summary>
    /// Called by modloader when regular mods phase begins.
    /// </summary>
    public static Action OnModsLoaded = () =>
    {
        Plugin.Log.Info("Mods loaded");
    };

    /// <summary>
    /// Called by modloader when late mods phase begins.
    /// </summary>
    public static Action OnLateModsLoaded = () =>
    {
        Plugin.Log.Info("Late mods loaded");
    };

    /// <summary>
    /// Called by modloader to notify about mod load status.
    /// </summary>
    public static Action<CModInfo, CLoadResultEnum> OnModLoaded = (modInfo, loadResult) =>
    {
        Plugin.Log.Info($"Mod '{modInfo.Id}' load result: {loadResult}");
    };

    /// <summary>
    /// Called by modloader to check if C# is ready for mod loading.
    /// </summary>
    public static Func<bool> IsCsReady = () =>
    {
        Plugin.Log.Debug("Modloader checking if C# is ready");
        return true;
    };

    /// <summary>
    /// Helper to get function pointer from a delegate for native code.
    /// </summary>
    public static IntPtr GetFunctionPointer<T>(T del) where T : class
    {
        if (del == null)
            return IntPtr.Zero;

        return Marshal.GetFunctionPointerForDelegate(del);
    }
}

#endregion

#region Exported Native Functions

/// <summary>
/// Exported functions callable from native code.
/// These are the entry points that native modloader calls.
/// </summary>
public static class ModloaderExportedFunctions
{
    // Keep delegate references alive to prevent garbage collection
    private static SetupCallbackDelegate _setupDelegate = null!;
    private static EarlyModsLoadedCallbackDelegate _earlyModsLoadedDelegate = null!;
    private static ModsLoadedCallbackDelegate _modsLoadedDelegate = null!;
    private static LateModsLoadedCallbackDelegate _lateModsLoadedDelegate = null!;
    private static ModLoadedCallbackDelegate _modLoadedDelegate = null!;
    private static ReadyCheckCallbackDelegate _readyCheckDelegate = null!;

    /// <summary>
    /// Delegate types for native callbacks
    /// </summary>
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetupCallbackDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void EarlyModsLoadedCallbackDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ModsLoadedCallbackDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void LateModsLoadedCallbackDelegate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ModLoadedCallbackDelegate(ref CModInfo modInfo, CLoadResultEnum result);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate bool ReadyCheckCallbackDelegate();

    /// <summary>
    /// Registers all callback delegates and returns their function pointers for native code.
    /// Call this once during plugin initialization.
    /// </summary>
    /// <returns>Struct containing all function pointers</returns>
    public static ModloaderFunctionPointers RegisterCallbacks()
    {
        _setupDelegate = () => ModloaderCallbacks.OnModloaderSetup?.Invoke();
        _earlyModsLoadedDelegate = () => ModloaderCallbacks.OnEarlyModsLoaded?.Invoke();
        _modsLoadedDelegate = () => ModloaderCallbacks.OnModsLoaded?.Invoke();
        _lateModsLoadedDelegate = () => ModloaderCallbacks.OnLateModsLoaded?.Invoke();
        _modLoadedDelegate = (ref CModInfo modInfo, CLoadResultEnum result) => 
            ModloaderCallbacks.OnModLoaded?.Invoke(modInfo, result);
        _readyCheckDelegate = () => ModloaderCallbacks.IsCsReady?.Invoke() ?? false;

        return new ModloaderFunctionPointers
        {
            OnModloaderSetup = Marshal.GetFunctionPointerForDelegate(_setupDelegate),
            OnEarlyModsLoaded = Marshal.GetFunctionPointerForDelegate(_earlyModsLoadedDelegate),
            OnModsLoaded = Marshal.GetFunctionPointerForDelegate(_modsLoadedDelegate),
            OnLateModsLoaded = Marshal.GetFunctionPointerForDelegate(_lateModsLoadedDelegate),
            OnModLoaded = Marshal.GetFunctionPointerForDelegate(_modLoadedDelegate),
            IsCsReady = Marshal.GetFunctionPointerForDelegate(_readyCheckDelegate),
        };
    }

    /// <summary>
    /// Exported function: on_modloader_setup
    /// Called by native modloader when setup phase begins
    /// </summary>
    [DllExport]
    public static void on_modloader_setup()
    {
        try
        {
            ModloaderCallbacks.OnModloaderSetup?.Invoke();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Error in on_modloader_setup: {ex}");
        }
    }

    /// <summary>
    /// Exported function: on_early_mods_loaded
    /// Called by native modloader when early mods phase begins
    /// </summary>
    [DllExport]
    public static void on_early_mods_loaded()
    {
        try
        {
            ModloaderCallbacks.OnEarlyModsLoaded?.Invoke();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Error in on_early_mods_loaded: {ex}");
        }
    }

    /// <summary>
    /// Exported function: on_mods_loaded
    /// Called by native modloader when regular mods phase begins
    /// </summary>
    [DllExport]
    public static void on_mods_loaded()
    {
        try
        {
            ModloaderCallbacks.OnModsLoaded?.Invoke();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Error in on_mods_loaded: {ex}");
        }
    }

    /// <summary>
    /// Exported function: on_late_mods_loaded
    /// Called by native modloader when late mods phase begins
    /// </summary>
    [DllExport]
    public static void on_late_mods_loaded()
    {
        try
        {
            ModloaderCallbacks.OnLateModsLoaded?.Invoke();
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Error in on_late_mods_loaded: {ex}");
        }
    }

    /// <summary>
    /// Exported function: on_mod_loaded
    /// Called by native modloader when a mod is loaded
    /// </summary>
    [DllExport]
    public static void on_mod_loaded(ref CModInfo modInfo, CLoadResultEnum result)
    {
        try
        {
            ModloaderCallbacks.OnModLoaded?.Invoke(modInfo, result);
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Error in on_mod_loaded: {ex}");
        }
    }

    /// <summary>
    /// Exported function: is_cs_ready
    /// Called by native modloader to check if C# is ready
    /// </summary>
    [DllExport]
    public static bool is_cs_ready()
    {
        try
        {
            return ModloaderCallbacks.IsCsReady?.Invoke() ?? false;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"Error in is_cs_ready: {ex}");
            return false;
        }
    }
}

#endregion

#region Function Pointer Structure

/// <summary>
/// Structure containing all function pointers for native code to call.
/// Use RegisterCallbacks() to populate this.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct ModloaderFunctionPointers
{
    /// <summary>
    /// Pointer to on_modloader_setup() function
    /// </summary>
    public IntPtr OnModloaderSetup;

    /// <summary>
    /// Pointer to on_early_mods_loaded() function
    /// </summary>
    public IntPtr OnEarlyModsLoaded;

    /// <summary>
    /// Pointer to on_mods_loaded() function
    /// </summary>
    public IntPtr OnModsLoaded;

    /// <summary>
    /// Pointer to on_late_mods_loaded() function
    /// </summary>
    public IntPtr OnLateModsLoaded;

    /// <summary>
    /// Pointer to on_mod_loaded() function
    /// </summary>
    public IntPtr OnModLoaded;

    /// <summary>
    /// Pointer to is_cs_ready() function
    /// </summary>
    public IntPtr IsCsReady;
}

#endregion

#region Helper Extensions

/// <summary>
/// Extension methods for working with modloader types.
/// </summary>
public static class ModloaderExtensions
{
    /// <summary>
    /// Marshals a CModResults struct to a managed array.
    /// </summary>
    public static CModResult[] ToManagedArray(this CModResults results)
    {
        if (results.Size == 0 || results.Array == IntPtr.Zero)
            return Array.Empty<CModResult>();

        var managed = new CModResult[results.Size];
        int size = Marshal.SizeOf(typeof(CModResult));

        for (ulong i = 0; i < results.Size; i++)
        {
            IntPtr ptr = new IntPtr(results.Array.ToInt64() + (long)i * size);
            managed[i] = Marshal.PtrToStructure<CModResult>(ptr);
        }

        return managed;
    }

    /// <summary>
    /// Marshals a CLoadResults struct to a managed array.
    /// </summary>
    public static CLoadResult[] ToManagedArray(this CLoadResults results)
    {
        if (results.Size == 0 || results.Array == IntPtr.Zero)
            return Array.Empty<CLoadResult>();

        var managed = new CLoadResult[results.Size];
        int size = Marshal.SizeOf(typeof(CLoadResult));

        for (ulong i = 0; i < results.Size; i++)
        {
            IntPtr ptr = new IntPtr(results.Array.ToInt64() + (long)i * size);
            managed[i] = Marshal.PtrToStructure<CLoadResult>(ptr);
        }

        return managed;
    }

    /// <summary>
    /// Creates a CModInfo from an ID and version.
    /// </summary>
    public static CModInfo CreateModInfo(string id, string version, ulong versionLong = 0)
    {
        return new CModInfo
        {
            Id = id,
            Version = version,
            VersionLong = versionLong
        };
    }
}

#endregion

