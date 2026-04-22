/* File: text.cs
 * Author: Gemmeni Pro 2.5, ChatGPT Codex
 * Description: This is an AI genereted debug module to quickly save data for testing.
 * It is not designed to be used in production. Nor should it be used as a permanent solution.
 */
using System;
using System.IO;        // Required for File operations
using System.Linq;      // Required for .ToList()
using System.Collections.Generic; // Required for List<T>

/// <summary>
/// A static utility class for performing common line-based file operations.
/// </summary>
namespace Spotify_Playlist_Manager.Models.txt
{
public static class FileHelper
{
    /// <summary>
    /// Appends a single line of text to the end of a file.
    /// A new line character is automatically added.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <param name="line">The line of text to add.</param>
    public static void AppendLine(string path, string line)
    {
        // Use AppendAllText to add to the file.
        // We add Environment.NewLine to ensure it works on all operating systems.
        File.AppendAllText(path, line + Environment.NewLine);
    }

    /// <summary>
    /// Reads a single line from a file.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <param name="lineNumber">The 1-based line number to read (e.g., 1, 2, 3).</param>
    /// <returns>The content of the line, or null if the file/line doesn't exist.</returns>
    public static string ReadSpecificLine(string path, int lineNumber)
    {
        if (!File.Exists(path))
        {
            // You might want to throw an exception here instead
            return null;
        }

        // Read all lines into an array
        string[] lines = File.ReadAllLines(path);

        // Convert 1-based line number to 0-based array index
        int lineIndex = lineNumber - 1;

        if (lineIndex >= 0 && lineIndex < lines.Length)
        {
            // Valid line number
            return lines[lineIndex];
        }
        
        // Line number is out of range
        return null;
    }

    /// <summary>
    /// Modifies a specific line in a file.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <param name="lineNumber">The 1-based line number to modify.</param>
    /// <param name="newText">The new text for the line.</param>
    /// <returns>True if modification was successful, false otherwise.</returns>
    public static bool ModifySpecificLine(string path, int lineNumber, string newText)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        // Read all lines into a List.
        List<string> lines = File.ReadAllLines(path).ToList();

        // Convert 1-based line number to 0-based list index
        int lineIndex = lineNumber - 1;

        if (lineIndex >= 0 && lineIndex < lines.Count)
        {
            // Modify the line in the list
            lines[lineIndex] = newText;

            // Write the entire modified list back to the file
            File.WriteAllLines(path, lines);
            return true;
        }
        
        // Line number was out of range
        return false;
    }

    /// <summary>
    /// Creates a new file (or overwrites an existing one) with the given lines.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <param name="lines">The content to write to the file.</param>
    public static void CreateOrOverwriteFile(string path, IEnumerable<string> lines)
    {
        File.WriteAllLines(path, lines);
    }

    /// <summary>
    /// Reads all lines from a file and returns them as a string array.
    /// Returns an empty array if the file doesn't exist.
    /// </summary>
    /// <param name="path">The path to the file.</param>
    /// <returns>A string array of all lines.</returns>
    public static string[] ReadAllLines(string path)
    {
        if (File.Exists(path))
        {
            return File.ReadAllLines(path);
        }
        
        return new string[0]; // Return an empty array
    }
}    
}
