using System;
using System.IO;

namespace JagexAccountSwitcher.Helpers;

public static class CredentialsHelper
{
    public static string? GetDisplayName(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("The specified file does not exist.", filePath);
        
        foreach (var line in File.ReadLines(filePath))
        {
            if (line.StartsWith("JX_DISPLAY_NAME="))
            {
                return line.Substring("JX_DISPLAY_NAME=".Length).Trim();
            }
        }
        return null; // Return null if the key is not found
    }

    /// <summary>
    /// Returns the display name inside the credentials file.
    /// Falls back to the file‑name suffix if the key is missing / empty.
    /// </summary>
    public static string GetDisplayNameOrFallback(string filePath)
    {
        string? name = null;
        // Try the normal way first
        try
        {
            name = GetDisplayName(filePath);
        }
        catch (FileNotFoundException) { /* ignore – we'll fall back */ }
        
        if (!string.IsNullOrWhiteSpace(name))
            return name;
        
        // 🔙 Fallback → use the bit after "credentials.properties."
        var fileName = Path.GetFileName(filePath);                     // credentials.properties.0001
        const string prefix = "credentials.properties.";
        if (fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return fileName.Substring(prefix.Length);                  // 0001
        
        // Last resort – bare file name without extension
        return Path.GetFileNameWithoutExtension(fileName);             // credentials.properties
    }
}