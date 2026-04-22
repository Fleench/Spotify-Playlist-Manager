    /* File: CacheWorker.cs
     * Author: Glenn Sutherland, ChatGPT Codex
     * Description: A basic manager for the local cache of images of items from spotify.
     */
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Threading.Tasks;
    namespace Spotify_Playlist_Manager.Models
    {
        /// <summary>
        /// Centralized helper responsible for downloading, storing, and looking
        /// up cached artwork for playlists, albums, and artists. The worker hides
        /// file-system exceptions so callers can treat missing artwork as an
        /// expected condition rather than an error state.
        /// </summary>
        public static class CacheWorker
        {
            public static string Cachepath = Variables.CachePath;

            /// <summary>
            /// Downloads an image from Spotify and stores it in the cache folder
            /// using a deterministic naming convention (<c>{type}_{id}.{ext}</c>).
            /// </summary>
            /// <param name="url">Remote image URL provided by Spotify.</param>
            /// <param name="type">The logical item type the image belongs to.</param>
            /// <param name="itemId">Unique identifier for the owning item.</param>
            /// <returns>The local file path when successful; otherwise <c>null</c>.</returns>
            public static async Task<string?> DownloadImageAsync(string url, ImageType type, string itemId)
            {
                if (string.IsNullOrWhiteSpace(url))
                {
                    return null;
                }

                try
                {
                    Directory.CreateDirectory(Cachepath);
                }
                catch (Exception directoryException) when (directoryException is IOException or UnauthorizedAccessException)
                {
                    Console.Error.WriteLine($"Failed to create cache directory '{Cachepath}': {directoryException}");
                    return null;
                }

                try
                {
                    using HttpClient client = new();

                    using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();

                    string contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                    string extension = GetFileExtensionFromMimeType(contentType);
                    string fileName = type.ToString() + "_" + itemId + extension;
                    string path = Path.Combine(Cachepath, fileName);

                    try
                    {
                        await using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
                        await response.Content.CopyToAsync(fileStream).ConfigureAwait(false);
                        return path;
                    }
                    catch (Exception fileException) when (fileException is IOException or UnauthorizedAccessException)
                    {
                        Console.Error.WriteLine($"Failed to write cached image '{path}': {fileException}");
                        return null;
                    }
                }
                catch (HttpRequestException httpException)
                {
                    Console.Error.WriteLine($"Failed to download image from '{url}': {httpException}");
                    return null;
                }
                catch (TaskCanceledException canceledException)
                {
                    Console.Error.WriteLine($"Image download timed out for '{url}': {canceledException}");
                    return null;
                }
            }
            /// <summary>
            /// Derives a stable file extension from the MIME type header provided
            /// by Spotify. Falls back to <c>.bin</c> for unknown types so the cache
            /// still works even when Spotify adds new content types.
            /// </summary>
            private static string GetFileExtensionFromMimeType(string mimeType)
            {
                // Convert to lowercase for case-insensitive comparison
                mimeType = mimeType.ToLowerInvariant();

                // Handle the common image types for Spotify
                if (mimeType.Contains("image/jpeg"))
                {
                    return ".jpg";
                }
                if (mimeType.Contains("image/png"))
                {
                    return ".png";
                }
                if (mimeType.Contains("image/gif"))
                {
                    return ".gif";
                }

                // Fallback: If the type is unknown, use a generic binary extension or skip
                return ".bin"; 
            }
            /// <summary>
            /// Logical categories of images that can be cached.
            /// </summary>
            public enum ImageType
            {
                Artist,
                Album,
                Playlist,
            }

            /// <summary>
            /// Attempts to find a cached image based on the deterministic naming
            /// scheme used by <see cref="DownloadImageAsync"/>.
            /// </summary>
            /// <param name="type">Item category to search for.</param>
            /// <param name="itemId">Unique identifier associated with the image.</param>
            /// <returns>The cached image path if found; otherwise <c>null</c>.</returns>
            public static string? GetImagePath(ImageType type, string itemId)
            {
                try
                {
                    string baseFileName = type.ToString() + "_" + itemId;
                    string searchPattern = baseFileName + ".*"; // e.g., "Album_123.*"

                    string[] files = Directory.GetFiles(
                        Cachepath,
                        searchPattern,
                        SearchOption.TopDirectoryOnly
                    );

                    if (files.Length > 0)
                    {
                        return files[0];
                    }
                }
                catch (Exception ioException) when (ioException is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
                {
                    Console.Error.WriteLine($"Failed to read cache directory '{Cachepath}': {ioException}");
                }

                return null;
            }
        }
    }

