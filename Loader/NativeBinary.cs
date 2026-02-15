using System;
using System.Collections.Generic;
using System.IO;

namespace Scotland2_Mono.Loader;

/// <summary>
/// Represents metadata about a native binary before it's loaded.
/// Handles dependency scanning and stores information about the binary file itself.
/// </summary>
public class NativeBinary
{
    /// <summary>
    /// The file path to the binary.
    /// </summary>
    public string FilePath { get; }
    
    public string Name => Path.GetFileName(FilePath);

    /// <summary>
    /// List of dependencies (imported DLLs/shared libraries) required by this binary.
    /// </summary>
    public IReadOnlyList<string>? Dependencies { get; private set; }


    public NativeBinary(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Binary file not found: {filePath}");

        FilePath = Path.GetFullPath(filePath);
        Dependencies = NativeLoaderHelper.GetDependencies(FilePath);
    }

    public override string ToString()
    {
        return $"{Name} - {Dependencies?.Count ?? 0} dependencies";
    }
}