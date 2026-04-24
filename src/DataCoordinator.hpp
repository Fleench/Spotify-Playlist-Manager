#ifndef DATA_COORDINATOR_HPP
#define DATA_COORDINATOR_HPP

#include "Variables.hpp"
#include "DatabaseWorker.hpp"
#include "Logger.hpp"
#include "SpotifyWorker.hpp"
#include "CacheWorker.hpp"
#include "Helpers.hpp"
#include <string>
#include <vector>
#include <optional>

namespace SpotifyPlaylistManager {

class DataCoordinator {
public:
    // Settings
    static void SetSetting(const std::string& key, const std::string& value) { DatabaseWorker::SetSetting(key, value); }
    static void RemoveSetting(const std::string& key) { DatabaseWorker::RemoveSetting(key); }
    static std::optional<std::string> GetSetting(const std::string& key) { return DatabaseWorker::GetSetting(key); }
    static std::vector<std::pair<std::string, std::string>> GetAllSettings() { return DatabaseWorker::GetAllSettings(); }

    // Playlists
    static void SetPlaylist(Variables::PlayList playlist) {
        auto imagePath = CacheWorker::GetImagePath(CacheWorker::ImageType::Playlist, playlist.Id);
        if (!imagePath && !playlist.ImageURL.empty()) {
            imagePath = CacheWorker::DownloadImage(playlist.ImageURL, CacheWorker::ImageType::Playlist, playlist.Id);
        }
        playlist.ImagePath = imagePath.value_or("");
        DatabaseWorker::SetPlaylist(playlist);
    }
    static void RemovePlaylist(const std::string& id) { DatabaseWorker::RemovePlaylist(id); }
    static std::optional<Variables::PlayList> GetPlaylist(const std::string& id) { return DatabaseWorker::GetPlaylist(id); }
    static std::vector<Variables::PlayList> GetAllPlaylists() { return DatabaseWorker::GetAllPlaylists(); }

    // Albums
    static void SetAlbum(Variables::Album album) {
        auto imagePath = CacheWorker::GetImagePath(CacheWorker::ImageType::Album, album.Id);
        if (!imagePath && !album.ImageURL.empty()) {
            imagePath = CacheWorker::DownloadImage(album.ImageURL, CacheWorker::ImageType::Album, album.Id);
        }
        album.ImagePath = imagePath.value_or("");
        DatabaseWorker::SetAlbum(album);
    }
    static void RemoveAlbum(const std::string& id) { DatabaseWorker::RemoveAlbum(id); }
    static std::optional<Variables::Album> GetAlbum(const std::string& id) { return DatabaseWorker::GetAlbum(id); }
    static std::vector<Variables::Album> GetAllAlbums() { return DatabaseWorker::GetAllAlbums(); }

    // Tracks
    static void SetTrack(Variables::Track track) {
        if (track.SongID.empty()) {
            track.SongID = Variables::MakeId();
        }
        DatabaseWorker::SetTrack(track);
    }
    static void RemoveTrack(const std::string& id) { DatabaseWorker::RemoveTrack(id); }
    static std::optional<Variables::Track> GetTrack(const std::string& id) { return DatabaseWorker::GetTrack(id); }
    static std::vector<Variables::Track> GetAllTracks() { return DatabaseWorker::GetAllTracks(); }
    static int GetTrackCountBySongId(const std::string& songId) { return DatabaseWorker::GetTrackCountBySongId(songId); }

    // Artists
    static void SetArtist(Variables::Artist artist) {
        auto imagePath = CacheWorker::GetImagePath(CacheWorker::ImageType::Artist, artist.Id);
        if (!imagePath && !artist.ImageURL.empty()) {
            imagePath = CacheWorker::DownloadImage(artist.ImageURL, CacheWorker::ImageType::Artist, artist.Id);
        }
        artist.ImagePath = imagePath.value_or("");
        DatabaseWorker::SetArtist(artist);
    }
    static void RemoveArtist(const std::string& id) { DatabaseWorker::RemoveArtist(id); }
    static std::optional<Variables::Artist> GetArtist(const std::string& id) { return DatabaseWorker::GetArtist(id); }
    static std::vector<Variables::Artist> GetAllArtists() { return DatabaseWorker::GetAllArtists(); }

    // Similar
    static void SetSimilar(const std::string& songId1, const std::string& songId2, const std::string& type) { DatabaseWorker::SetSimilar(songId1, songId2, type); }
    static void RemoveSimilar(const std::string& songId1, const std::string& songId2, const std::string& type) { DatabaseWorker::RemoveSimilar(songId1, songId2, type); }
    static std::vector<std::tuple<std::string, std::string, std::string>> GetAllSimilar() { return DatabaseWorker::GetAllSimilar(); }

    // MightBeSimilar
    static void SetMightBeSimilar(const std::string& songId1, const std::string& songId2) { DatabaseWorker::SetMightBeSimilar(songId1, songId2); }
    static void RemoveMightBeSimilar(const std::string& songId1, const std::string& songId2) { DatabaseWorker::RemoveMightBeSimilar(songId1, songId2); }
    static std::vector<std::pair<std::string, std::string>> GetAllMightBeSimilar() { return DatabaseWorker::GetAllMightBeSimilar(); }

    // --- Synchronization Logic ---
    static void SlowSync() {
        Sync();
    }

    static void Sync() {
        SyncPlaylists();
        SyncAlbums();
        SyncLikedSongs();
        SyncFollowedArtists();
        SyncTrackMetadata();
        SyncArtistMetadata();
    }

    static int UpdateArtistIdSeparators() {
        int updates = 0;
        for (auto& track : DatabaseWorker::GetAllTracks()) {
            std::string current = track.ArtistIds;
            std::string normalized = NormalizeArtistIdString(current);
            if (current != normalized) {
                track.ArtistIds = normalized;
                DatabaseWorker::SetTrack(track);
                updates++;
            }
        }
        for (auto& album : DatabaseWorker::GetAllAlbums()) {
            std::string current = album.ArtistIDs;
            std::string normalized = NormalizeArtistIdString(current);
            if (current != normalized) {
                album.ArtistIDs = normalized;
                DatabaseWorker::SetAlbum(album);
                updates++;
            }
        }
        return updates;
    }

private:
    static std::string NormalizeArtistIdString(const std::string& ids) {
        if (ids.empty()) return "";
        auto parts = Helpers::Split(ids, Variables::Seperator);
        std::vector<std::string> valid;
        for (auto& p : parts) {
            std::string trimmed = Helpers::Trim(p);
            if (!trimmed.empty()) valid.push_back(trimmed);
        }
        if (valid.empty()) return "";
        return Helpers::Join(valid, Variables::Seperator);
    }

    static void SyncPlaylists() {
        Logger::Log("Fetching Playlists...", LogType::Info);
        auto playlists = SpotifyWorker::GetUserPlaylists();
        for (auto& summary : playlists) {
            std::string pId = std::get<0>(summary);
            auto data = SpotifyWorker::GetPlaylistData(pId);
            if (std::get<0>(data).empty()) continue; // Skip if failed

            Variables::PlayList pl;
            pl.Id = pId;
            pl.Name = std::get<0>(data);
            pl.ImageURL = std::get<1>(data);
            pl.Description = std::get<3>(data);
            pl.SnapshotID = std::get<4>(data);
            pl.TrackIDs = std::get<5>(data);

            auto existing = DatabaseWorker::GetPlaylist(pl.Id);
            if (!existing || existing->SnapshotID != pl.SnapshotID) {
                SetPlaylist(pl);
                auto tIds = Helpers::Split(pl.TrackIDs, ";;");
                for (auto& tid : tIds) {
                    DatabaseWorker::EnsureTrackExists(tid);
                }
            }
        }
        std::cout << "Got Playlists\n";
    }

    static void SyncAlbums() {
        Logger::Log("Fetching Albums...", LogType::Info);
        auto albums = SpotifyWorker::GetUserAlbums();
        for (auto& summary : albums) {
            std::string aId = std::get<0>(summary);
            auto data = SpotifyWorker::GetAlbumData(aId);
            if (std::get<0>(data).empty()) continue;

            Variables::Album alb;
            alb.Id = aId;
            alb.Name = std::get<0>(data).empty() ? std::get<1>(summary) : std::get<0>(data);
            alb.ImageURL = std::get<1>(data);
            alb.TrackIDs = std::get<3>(data);
            alb.ArtistIDs = std::get<4>(data);

            SetAlbum(alb);

            auto tIds = Helpers::Split(alb.TrackIDs, ";;");
            for (auto& tid : tIds) {
                DatabaseWorker::EnsureTrackExists(tid);
            }
            
            auto artIds = Helpers::Split(alb.ArtistIDs, ";;");
            for (auto& aid : artIds) {
                if (!aid.empty()) {
                    auto existingArt = DatabaseWorker::GetArtist(aid);
                    if (!existingArt) {
                        Variables::Artist a;
                        a.Id = aid;
                        SetArtist(a);
                    }
                }
            }
        }
        std::cout << "Got Albums\n";
    }

    static void SyncLikedSongs() {
        Logger::Log("Fetching Liked Songs...", LogType::Info);
        auto liked = SpotifyWorker::GetLikedSongs();
        Variables::PlayList likedPl;
        likedPl.Id = "liked_songs";
        likedPl.Name = "Liked Songs";
        likedPl.SnapshotID = "local";
        
        std::string tIdsStr = "";
        for (auto& song : liked) {
            std::string tid = std::get<0>(song);
            tIdsStr += tid + ";;";
            DatabaseWorker::EnsureTrackExists(tid);
        }
        likedPl.TrackIDs = tIdsStr;
        SetPlaylist(likedPl);
        std::cout << "Got Liked Songs\n";
    }

    static void SyncFollowedArtists() {
        Logger::Log("Fetching Followed Artists...", LogType::Info);
        auto artists = SpotifyWorker::GetFollowedArtists();
        for (auto& r : artists) {
            Variables::Artist art;
            art.Id = std::get<0>(r);
            art.Name = std::get<1>(r).empty() ? "Unknown Artist" : std::get<1>(r);
            art.ImageURL = std::get<2>(r);
            art.Genres = std::get<3>(r);
            SetArtist(art);
        }
        std::cout << "Got Followed Artists\n";
    }

    static void SyncTrackMetadata() {
        Logger::Log("Fetching Track Metadata...", LogType::Info);
        auto tracks = DatabaseWorker::GetAllTracks();
        std::vector<std::string> idsToFetch;
        for (auto& t : tracks) {
            if (t.MissingInfo()) {
                idsToFetch.push_back(t.Id);
            }
        }

        if (!idsToFetch.empty()) {
            auto results = SpotifyWorker::GetSongDataBatch(idsToFetch);
            for (auto& r : results) {
                Variables::Track t;
                t.Name = std::get<0>(r);
                t.Id = std::get<1>(r);
                t.AlbumId = std::get<2>(r);
                t.ArtistIds = std::get<3>(r);
                t.DiscNumber = std::get<4>(r);
                t.DurationMs = std::get<5>(r);
                t.Explicit = std::get<6>(r);
                t.PreviewUrl = std::get<7>(r);
                t.TrackNumber = std::get<8>(r);

                auto existing = DatabaseWorker::GetTrack(t.Id);
                if (existing) {
                    t.SongID = existing->SongID;
                    if (t.Name.empty()) t.Name = existing->Name;
                }

                SetTrack(t);

                if (!t.AlbumId.empty()) {
                    Variables::Album a;
                    a.Id = t.AlbumId;
                    SetAlbum(a);
                }

                auto artIds = Helpers::Split(t.ArtistIds, ";;");
                for (auto& aid : artIds) {
                    if (!aid.empty()) {
                        Variables::Artist art;
                        art.Id = aid;
                        SetArtist(art);
                    }
                }
            }
        }
        std::cout << "Got Track Metadata\n";
    }

    static void SyncArtistMetadata() {
        Logger::Log("Fetching Artist Metadata...", LogType::Info);
        auto artists = DatabaseWorker::GetAllArtists();
        std::vector<std::string> idsToFetch;
        for (auto& a : artists) {
            if (a.MissingInfo()) {
                idsToFetch.push_back(a.Id);
            }
        }

        std::cout << "DEBUG: " << idsToFetch.size() << " artists to fetch\n";

        if (!idsToFetch.empty()) {
            auto results = SpotifyWorker::GetArtistDataBatch(idsToFetch);
            std::cout << "DEBUG: " << results.size() << " artists fetched\n";
            for (auto& r : results) {
                Variables::Artist art;
                art.Id = std::get<0>(r);
                art.Name = std::get<1>(r).empty() ? "Unknown Artist" : std::get<1>(r);
                art.ImageURL = std::get<2>(r);
                art.Genres = std::get<3>(r);

                std::cout << "DEBUG: Saving Artist: " << art.Name << " (ID: " << art.Id << ")\n";
                SetArtist(art);
            }
        }
        Logger::Log("Got Artist Metadata", LogType::Success);
    }

};

}

#endif // DATA_COORDINATOR_HPP
