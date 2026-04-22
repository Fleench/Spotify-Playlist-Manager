/* File: manual-test.cs
 * Author: Glenn Sutherland, ChatGPT Codex
 * Description: Manual testing for the Spotify Playlist Manager. This allows
 * for the modules to tested for before they are stringed together.
 */
using Spotify_Playlist_Manager.Models.txt;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Spotify_Playlist_Manager.Models;
using System.Collections.Concurrent;
using System.Threading;

/// <summary>
/// Stand-alone console entry point intended for smoke-testing the workers
/// without launching the Avalonia front end. Keep the file excluded from
/// release builds; it serves only as a diagnostic harness.
/// </summary>
public class TempProgram
{
    static async Task Main(string[] args)
    {
        Variables.Init();
        DatabaseWorker.Init();
        // --- Example Usage ---

        string myFile = "settings.txt";
        //old code
        /*
        FileHelper.ModifySpecificLine(myFile,4,"null"); //client token
        FileHelper.ModifySpecificLine(myFile,1,"null"); //client secret
        FileHelper.ModifySpecificLine(myFile,2,"null"); //token
        FileHelper.ModifySpecificLine(myFile,3,"null"); //refresh token
        */

        string clientID = FileHelper.ReadSpecificLine(myFile, 4);
        if (clientID == "" || clientID == null)
        {
            Console.WriteLine("Could not read settings.txt file for ClientID");
            clientID = Console.ReadLine();
        }

        string clientSecret = FileHelper.ReadSpecificLine(myFile, 1);
        ;

        if (clientSecret == "" || clientSecret == null)
        {
            Console.WriteLine("Could not read settings.txt file for ClientSecret");
            clientSecret = Console.ReadLine();

        }

        string token = "";
        string refreshToken = "";
        if (FileHelper.ReadSpecificLine(myFile, 2) != "null")
        {
            token = FileHelper.ReadSpecificLine(myFile, 2);
        }

        if (FileHelper.ReadSpecificLine(myFile, 3) != "null")
        {
            refreshToken = FileHelper.ReadSpecificLine(myFile, 3);
        }

        Console.WriteLine($"ClientID: {clientID}, ClientSecret: {clientSecret}");
        SpotifyWorker.Init(clientID, clientSecret, token, refreshToken);
        SpotifyWorker_Old.Init(clientID, clientSecret, token, refreshToken);
        var (at, rt) = await SpotifyWorker_Old.AuthenticateAsync();
        FileHelper.ModifySpecificLine(myFile, 4, clientID);
        FileHelper.ModifySpecificLine(myFile, 1, clientSecret);
        FileHelper.ModifySpecificLine(myFile, 2, at);
        FileHelper.ModifySpecificLine(myFile, 3, rt);
        await DataCoordinator.SetSettingAsync(Variables.Settings.SW_ClientToken, clientID);
        await DataCoordinator.SetSettingAsync(Variables.Settings.SW_RefreshToken, refreshToken);
        await DataCoordinator.SetSettingAsync(Variables.Settings.SW_AccessToken, at);
        await DataCoordinator.SetSettingAsync(Variables.Settings.SW_ClientSecret, clientSecret);

        Console.WriteLine("YO WE DONE with AUTHENTICATED!");
        string data = "data.txt";
        //Get the first playlist, its first song, that songs album and artist, and save the IDs.
        //uncomment this code to get new ids
        /*var ids = Getabitofdata().Result;
        Console.WriteLine(ids);
        string st = ids.playlistID + Variables.Seperator + ids.trackID + Variables.Seperator + ids.albumID +
                    Variables.Seperator + ids.artistID;
        Console.WriteLine(st);
        IEnumerable<string> list = new[] { st };
        FileHelper.CreateOrOverwriteFile(data, list);
        FileHelper.ModifySpecificLine(data, 1, st);*/
        Console.WriteLine("Sync Started"); // Provide a simple progress heartbeat.
        var timerTask = Task.Run(async () =>
        {
            int seconds = 0;
            while (true)
            {
                seconds++;
                TimeSpan span = TimeSpan.FromSeconds(seconds);
                string disp = $"";
                Console.Write($"\r {span}");
                await Task.Delay(1000);
            }
        });
        //await DataCoordinator.Sync();
        // 1. Get the initial list of all tracks.
        DataCoordinator.UpdateArtistIdSeparatorsAsync();
        var allTracks = DataCoordinator.GetAllTracks();

        // 2. Process the tracks using LINQ
        // Build a list of tracks that share the same SongID so we can inspect
        // duplicates created by older synchronization runs.
        var tracksWithDuplicateSongIds = allTracks
            // Group the items based on the non-unique identifier: SongId
            .GroupBy(item => item.SongID)

            // Filter the groups: keep only the ones that have 2 or more tracks associated with that SongId
            .Where(group => group.Count() >= 2)

            // <--- THIS IS THE LINE YOU CHANGE --->
            // Use SelectMany to flatten the remaining groups back into a single list of tracks.
            // It takes all the individual Track items within the qualifying groups.
            .SelectMany(group => group)

            // Convert the final result back to a List<Variables.Track>
            .ToList();
        foreach (var track in tracksWithDuplicateSongIds)
        {
            var artists = track.ArtistIds.Split(Variables.Seperator);
            string artist_string = "";
            foreach (var artist in artists)
            {
                var artist_item = DataCoordinator.GetArtist(artist);
                if (artist_item != null)
                {
                    artist_string += artist_item.Name + ",";
                }
            }

            Console.WriteLine($"{track.Name} - {artist_string}");
        }

}

    public static async Task<(string playlistID, string trackID, string albumID, string artistID)> Getabitofdata()
    {
        string playlistID = "";
        string trackID = "";
        string albumID = "";
        string artistID = "";
        await foreach (var playlist in SpotifyWorker_Old.GetUserPlaylistsAsync())
        {
            playlistID = playlist.Id;
            break;
        }
        trackID = SpotifyWorker_Old.GetPlaylistDataAsync(playlistID).Result.TrackIDs.Split(Variables.Seperator)[0];
        var data = SpotifyWorker_Old.GetSongDataAsync(trackID);
        albumID = data.Result.albumID;
        artistID = data.Result.artistIDs.Split(Variables.Seperator)[0];
        return  (playlistID, trackID, albumID, artistID);
    }
}