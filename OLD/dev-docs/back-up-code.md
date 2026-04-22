```C#
// HOW MANY SONGS I GOT
        /*
        string localID = "";
        HashSet<string> trackIDs = new(); // ensures uniqueness
        int trackcounter = 0;

        await foreach (var item in SpotifyWorker.GetUserPlaylistsAsync())
        {
            var ids = SpotifyWorker.GetPlaylistDataAsync(item.Id).Result.TrackIDs.Split(";;");
            foreach (var id in ids)
            {
                if (trackIDs.Add(id)) // only true if new ID added
                {
                    trackcounter++;
                    Console.Write($"\r Tracks Found: {trackcounter}");
                }
            }
        }

        await foreach (var item in SpotifyWorker.GetUserAlbumsAsync())
        {
            var ids = SpotifyWorker.GetAlbumDataAsync(item.Id).Result.TrackIDs.Split(";;");
            foreach (var id in ids)
            {
                if (trackIDs.Add(id))
                {
                    trackcounter++;
                    Console.Write($"\r Tracks Found: {trackcounter}");
                }
            }
        }

        await foreach (var item in SpotifyWorker.GetLikedSongsAsync())
        {
            if (trackIDs.Add(item.Id))
            {
                trackcounter++;
                Console.Write($"\r Tracks Found: {trackcounter}");
            }
        }

        trackIDs.Remove("");
        Console.WriteLine("");
        HashSet<string> albumIDs = new();
        int albumcounter = 0;
        foreach (var track in trackIDs)
        {
            if (albumIDs.Add(SpotifyWorker.GetSongDataAsync(track).Result.albumID))
            {
                albumcounter++;
                Console.Write($"\r Albums Found: {albumcounter}");
            }
        }
        HashSet<string> artistIDs = new();
        Console.WriteLine("");
        int artistcounter = 0;
        foreach (var track in trackIDs)
        {
            string[] data = SpotifyWorker.GetSongDataAsync(track).Result.artistIDs.Split(";;");
            foreach (var artist in data)
            {
                if (albumIDs.Add(artist))
                {
                    artistcounter++;
                    Console.Write($"\r Artists Found: {artistcounter}");
                }
            }
        }
        Console.WriteLine($"{trackcounter} tracks found from {albumcounter} albums by {artistcounter} artists");
        */
        // ---- CONFIG ----
        /*
        int maxConcurrentCalls = 6; // Hard limit (change if needed)
        var semaphore = new SemaphoreSlim(maxConcurrentCalls);
        const int maxRetries = 5;   // Retry limit for transient errors
        const int requestPauseMs = 100; // 0.1s pause after every request to reduce rate limiting

        async Task<(bool Success, T Result)> TryExecuteWithRetriesAsync<T>(Func<Task<T>> operation, string context)
        {
            int retryCount = 0;

            while (true)
            {
                try
                {
                    var result = await operation();
                    await Task.Delay(requestPauseMs);
                    return (true, result);
                }
                catch (SpotifyAPI.Web.APITooManyRequestsException ex)
                {
                    int waitSeconds = Math.Max((int)Math.Ceiling(ex.RetryAfter.TotalSeconds), 1);
                    Console.WriteLine($"\n[429] Rate limited while {context}. Waiting {waitSeconds}s...");
                    await Task.Delay(waitSeconds * 1000);
                }
                catch (Exception ex)
                {
                    retryCount++;
                    Console.WriteLine($"\n[ERROR] {context}: {ex.Message}");

                    if (retryCount > maxRetries)
                    {
                        Console.WriteLine($"[WARN] Skipping {context} after {maxRetries} retries.");
                        return (false, default!);
                    }

                    int waitSeconds = retryCount * 2; // exponential backoff
                    Console.WriteLine($"[Retry] {context}. Waiting {waitSeconds}s before retry {retryCount}/{maxRetries}...");
                    await Task.Delay(waitSeconds * 1000);
                }

                await Task.Delay(requestPauseMs);
            }
        }

        // ---- STEP 1: COLLECT ALL TRACK IDS ----
        var trackIDs = new HashSet<string>();
        int trackcounter = 0;

        Console.WriteLine("Collecting all track IDs...\n");
        
        await foreach (var item in SpotifyWorker.GetUserPlaylistsAsync())
        {
            var (success, playlistData) = await TryExecuteWithRetriesAsync(
                () => SpotifyWorker.GetPlaylistDataAsync(item.Id),
                $"fetching playlist {item.Id}");

            if (!success || string.IsNullOrWhiteSpace(playlistData.TrackIDs))
            {
                continue;
            }

            foreach (var id in playlistData.TrackIDs.Split(";;", StringSplitOptions.RemoveEmptyEntries))
            {
                if (trackIDs.Add(id))
                {
                    trackcounter++;
                }
            }

            Console.Write($"\rTracks Found: {trackcounter}");
        }

        await foreach (var item in SpotifyWorker.GetUserAlbumsAsync())
        {
            var (success, albumData) = await TryExecuteWithRetriesAsync(
                () => SpotifyWorker.GetAlbumDataAsync(item.Id),
                $"fetching album {item.Id}");

            if (!success || string.IsNullOrWhiteSpace(albumData.TrackIDs))
            {
                continue;
            }

            foreach (var id in albumData.TrackIDs.Split(";;", StringSplitOptions.RemoveEmptyEntries))
            {
                if (trackIDs.Add(id))
                {
                    trackcounter++;
                }
            }

            Console.Write($"\rTracks Found: {trackcounter}");
        }

        await foreach (var item in SpotifyWorker.GetLikedSongsAsync())
        {
            if (trackIDs.Add(item.Id))
            {
                trackcounter++;
            }

            Console.Write($"\rTracks Found: {trackcounter}");
        }

        Console.WriteLine($"\n\nTotal unique tracks collected: {trackIDs.Count}\n");

        // ---- STEP 2: FETCH SONG METADATA CONCURRENTLY ----
        var songData = new ConcurrentDictionary<string, (string AlbumID, string[] ArtistIDs)>();
        int completed = 0;

        Console.WriteLine($"Fetching song metadata (max {maxConcurrentCalls} concurrent calls)...\n");

        var tasks = trackIDs.Select(async id =>
        {
            await semaphore.WaitAsync();
            try
            {
                var (success, data) = await TryExecuteWithRetriesAsync(
                    () => SpotifyWorker.GetSongDataAsync(id),
                    $"fetching song {id}");

                if (!success)
                {
                    return;
                }

                songData[id] = (data.albumID, data.artistIDs.Split(";;", StringSplitOptions.RemoveEmptyEntries));
                int done = Interlocked.Increment(ref completed);

                if (done % 25 == 0)
                    Console.Write($"\rSongs Processed: {done}/{trackIDs.Count}");
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        Console.WriteLine("\nSong data fetch complete.\n");

        // ---- STEP 3: COUNT ALBUMS & ARTISTS ----
        var albumIDs = new HashSet<string>();
        var artistIDs = new HashSet<string>();

        foreach (var kv in songData.Values)
        {
            albumIDs.Add(kv.AlbumID);
            foreach (var a in kv.ArtistIDs)
                artistIDs.Add(a);
        }

        Console.WriteLine("------------------------------------");
        Console.WriteLine($"Tracks : {trackIDs.Count}");
        Console.WriteLine($"Albums : {albumIDs.Count}");
        Console.WriteLine($"Artists: {artistIDs.Count}");
        Console.WriteLine("------------------------------------\n");
        */
```