using System;
using System.Runtime.InteropServices;
using DllExporterNet4;
using Scotland2_Mono.Loader;
using System.Collections.Generic;
using System.Linq;


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

/// <summary>
/// Modloader C API exports - exposes modloader functionality to unmanaged code.
/// These functions are called from native plugins and other unmanaged code.
/// </summary>
public static class ModloaderExports
{
    #region Module State
    // Global state for the modloader
    private static List<NativePluginInfo> _loadedModules = new();
    private static NativePluginLoader? _pluginLoader;
    private static string _modloaderPath = string.Empty;
    private static string _rootLoadPath = string.Empty;
    private static string _filesDir = string.Empty;
    private static string _externalDir = string.Empty;
    private static string _applicationId = string.Empty;
    private static string _sourcePath = string.Empty;
    private static string _libil2cppPath = string.Empty;
    private static bool _loadFailed = false;
    public static IntPtr ModloaderJvm = IntPtr.Zero;
    public static IntPtr ModloaderLibil2cppHandle = IntPtr.Zero;
    public static IntPtr ModloaderUnityHandle = IntPtr.Zero;
    public static bool LibsOpened = false;
    public static bool EarlyModsOpened = false;
    public static bool LateModsOpened = false;
    public static CLoadPhase CurrentLoadPhase = CLoadPhase.LoadPhase_None;
    #endregion
    #region Initialization
    /// <summary>
    /// Initializes the modloader with paths and loaders.
    /// Call this during plugin initialization.
    /// </summary>
    public static void Initialize(
        NativePluginLoader pluginLoader,
        string modloaderPath,
        string rootLoadPath,
        string filesDir,
        string externalDir,
        string applicationId,
        string sourcePath,
        string libil2cppPath)
    {
        _pluginLoader = pluginLoader;
        _modloaderPath = modloaderPath;
        _rootLoadPath = rootLoadPath;
        _filesDir = filesDir;
        _externalDir = externalDir;
        _applicationId = applicationId;
        _sourcePath = sourcePath;
        _libil2cppPath = libil2cppPath;
    }
    #endregion
    #region Query Functions - Exported
    /// <summary>
    /// Returns true if the modloader failed to copy over the libs/mods to load, false otherwise.
    /// </summary>
    [DllExport()]
    public static bool modloader_get_failed()
    {
        return _loadFailed;
    }
    /// <summary>
    /// Returns the path of the modloader.
    /// Example output: /data/user/0/com.beatgames.beatsaber/files/libsl2.so
    /// </summary>
    [DllExport()]
    public static string modloader_get_path()
    {
        return _modloaderPath;
    }
    /// <summary>
    /// Returns the root load path for searches.
    /// Example output: /sdcard/ModData/com.beatgames.beatsaber/Modloader
    /// </summary>
    [DllExport()]
    public static string modloader_get_root_load_path()
    {
        return _rootLoadPath;
    }
    /// <summary>
    /// Returns the path to the files directory
    /// Example output: /data/user/0/com.beatgames.beatsaber/files
    /// </summary>
    [DllExport()]
    public static string modloader_get_files_dir()
    {
        return _filesDir;
    }
    /// <summary>
    /// Returns the path to the external folder
    /// Example output: /storage/emulated/0/Android/data/com.beatgames.beatsaber/files
    /// </summary>
    [DllExport()]
    public static string modloader_get_external_dir()
    {
        return _externalDir;
    }
    /// <summary>
    /// Returns the application ID
    /// Example output: com.beatgames.beatsaber
    /// </summary>
    [DllExport()]
    public static string modloader_get_application_id()
    {
        return _applicationId;
    }
    /// <summary>
    /// Returns the path where the modloader was found from
    /// Example output: /sdcard/ModData/com.beatgames.beatsaber/Modloader/libsl2.so
    /// </summary>
    [DllExport()]
    public static string modloader_get_source_path()
    {
        return _sourcePath;
    }
    /// <summary>
    /// Returns the path where libil2cpp.so is located and dlopened from
    /// </summary>
    [DllExport()]
    public static string modloader_get_libil2cpp_path()
    {
        return _libil2cppPath;
    }
    #endregion
    #region Mod Management Functions - Exported
    /// <summary>
    /// Finds the mod result for the id
    /// </summary>
    [DllExport()]
    public static CModResult modloader_get_mod(ref CModInfo info, CMatchType matchType)
    {
        var result = new CModResult();
        if (_pluginLoader == null)
            return result;
        CModInfo temp = info;
        var found = _loadedModules.FirstOrDefault(m => MatchesCriteria(m, temp, matchType));
        if (found != null)
        {
            result.Info = info;
            result.Path = found.Binary.FilePath;
            result.Handle = (IntPtr)found.LibraryHandle;
        }
        return result;
    }
    /// <summary>
    /// Triggers an unload of the specified mod.
    /// </summary>
    [DllExport()]
    public static bool modloader_force_unload(CModInfo info, CMatchType matchType)
    {
        CModInfo temp = info;
        var module = _loadedModules.FirstOrDefault(m => MatchesCriteria(m, temp, matchType));
        if (module == null)
            return true; // Already not loaded
        try
        {
            if (module.LibraryHandle != NativeLibraryHandle.Null)
            {
                NativeLoaderHelper.UnloadNativeLibrary(module.LibraryHandle);
            }
            _loadedModules.Remove(module);
            return true;
        }
        catch
        {
            return false;
        }
    }
    /// <summary>
    /// Returns an allocated array of CModResults for all successfully loaded objects.
    /// </summary>
    [DllExport()]
    public static CModResults modloader_get_loaded()
    {
        var results = new CModResults();
        var loadedModules = _loadedModules.Where(m => m.IsLoaded).ToArray();
        if (loadedModules.Length == 0)
            return results;
        var cmodResults = new CModResult[loadedModules.Length];
        for (int i = 0; i < loadedModules.Length; i++)
        {
            cmodResults[i] = new CModResult
            {
                Info = new CModInfo { Id = loadedModules[i].Id, Version = loadedModules[i].Version.ToString(), VersionLong = loadedModules[i].VersionLong },
                Path = loadedModules[i].Binary.FilePath,
                Handle = (IntPtr)loadedModules[i].LibraryHandle
            };
        }
        int size = Marshal.SizeOf(typeof(CModResult));
        IntPtr arrayPtr = Marshal.AllocCoTaskMem(size * cmodResults.Length);
        for (int i = 0; i < cmodResults.Length; i++)
        {
            Marshal.StructureToPtr(cmodResults[i], IntPtr.Add(arrayPtr, i * size), false);
        }
        results.Array = arrayPtr;
        results.Size = (ulong)cmodResults.Length;
        return results;
    }
    /// <summary>
    /// Returns an allocated array of CModResults for all successfully loaded and failed objects.
    /// </summary>
    [DllExport()]
    public static CLoadResults modloader_get_all()
    {
        var results = new CLoadResults();
        if (_loadedModules.Count == 0)
            return results;
        var cloadResults = new CLoadResult[_loadedModules.Count];
        for (int i = 0; i < _loadedModules.Count; i++)
        {
            var module = _loadedModules[i];
            cloadResults[i] = new CLoadResult
            {
                Result = module.IsLoaded ? CLoadResultEnum.MatchType_Loaded : CLoadResultEnum.LoadResult_Failed,
                UnionData = module.IsLoaded ? module.Binary.FilePath : (module.ErrorMessage ?? string.Empty)
            };
        }
        int size = Marshal.SizeOf(typeof(CLoadResult));
        IntPtr arrayPtr = Marshal.AllocCoTaskMem(size * cloadResults.Length);
        for (int i = 0; i < cloadResults.Length; i++)
        {
            Marshal.StructureToPtr(cloadResults[i], IntPtr.Add(arrayPtr, i * size), false);
        }
        results.Array = arrayPtr;
        results.Size = (ulong)cloadResults.Length;
        return results;
    }
    /// <summary>
    /// Frees a CModResults object allocated by the modloader
    /// </summary>
    [DllExport()]
    public static void modloader_free_results(ref CModResults results)
    {
        if (results.Array != IntPtr.Zero)
        {
            Marshal.FreeCoTaskMem(results.Array);
            results.Array = IntPtr.Zero;
            results.Size = 0;
        }
    }
    /// <summary>
    /// Requires a mod to be loaded. If not already loaded, attempts to load it.
    /// </summary>
    [DllExport()]
    public static CLoadResultEnum modloader_require_mod(ref CModInfo info, CMatchType matchType)
    {
        CModInfo temp = info;
        var existing = _loadedModules.FirstOrDefault(m => MatchesCriteria(m, temp, matchType));
        if (existing != null && existing.IsLoaded)
            return CLoadResultEnum.MatchType_Loaded;
        // Attempt to load the mod
        // TODO: Implement actual loading logic
        return CLoadResultEnum.LoadResult_NotFound;
    }
    #endregion
    #region Library Path Management - Exported
    /// <summary>
    /// Adds the path to the LD_LIBRARY_PATH of the modloader/mods namespace
    /// </summary>
    [DllExport()]
    public static bool modloader_add_ld_library_path(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
        try
        {
            string? currentPath = Environment.GetEnvironmentVariable("LD_LIBRARY_PATH");
            string newPath = string.IsNullOrEmpty(currentPath)
                ? path
                : currentPath + System.IO.Path.PathSeparator + path;
            Environment.SetEnvironmentVariable("LD_LIBRARY_PATH", newPath);
            return true;
        }
        catch
        {
            return false;
        }
    }
    #endregion
    #region Exported Variables
    /// <summary>
    /// The captured pointer to the JavaVM
    /// </summary>
    [DllExport()]
    public static IntPtr GetModloaderJvm() => ModloaderJvm;
    /// <summary>
    /// The captured dlopen-d libil2cpp.so handle
    /// </summary>
    [DllExport()]
    public static IntPtr GetModloaderLibil2cppHandle() => ModloaderLibil2cppHandle;
    /// <summary>
    /// The captured dlopen-d libunity.so handle
    /// </summary>
    [DllExport()]
    public static IntPtr GetModloaderUnityHandle() => ModloaderUnityHandle;
    /// <summary>
    /// True if libs have been dlopened
    /// </summary>
    [DllExport()]
    public static bool GetLibsOpened() => LibsOpened;
    /// <summary>
    /// True if early mods have been opened
    /// </summary>
    [DllExport()]
    public static bool GetEarlyModsOpened() => EarlyModsOpened;
    /// <summary>
    /// True if late mods have been opened
    /// </summary>
    [DllExport()]
    public static bool GetLateModsOpened() => LateModsOpened;
    /// <summary>
    /// Current loading phase being invoked.
    /// </summary>
    [DllExport()]
    public static CLoadPhase GetCurrentLoadPhase() => CurrentLoadPhase;
    #endregion
    #region Helper Methods
    /// <summary>
    /// Registers a loaded module in the modloader.
    /// Call this when a plugin is successfully loaded.
    /// </summary>
    public static void RegisterModule(NativePluginInfo pluginInfo)
    {
        _loadedModules.Add(pluginInfo);
    }
    /// <summary>
    /// Registers a failed module in the modloader.
    /// </summary>
    public static void RegisterFailedModule(NativePluginInfo pluginInfo)
    {
        _loadedModules.Add(pluginInfo);
    }
    /// <summary>
    /// Checks if a module matches the search criteria.
    /// </summary>
    private static bool MatchesCriteria(NativePluginInfo module, CModInfo searchInfo, CMatchType matchType)
    {
        return matchType switch
        {
            CMatchType.MatchType_Strict => module.Id == searchInfo.Id && module.Version.ToString() == searchInfo.Version && module.VersionLong == searchInfo.VersionLong,
            CMatchType.MatchType_IdOnly => module.Id == searchInfo.Id,
            CMatchType.MatchType_IdVersion => module.Id == searchInfo.Id && module.Version.ToString() == searchInfo.Version,
            CMatchType.MatchType_IdVersionLong => module.Id == searchInfo.Id && module.VersionLong == searchInfo.VersionLong,
            CMatchType.MatchType_ObjectName => module.Binary.FilePath.EndsWith(searchInfo.Id),
            _ => false
        };
    }
    #endregion
}


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

