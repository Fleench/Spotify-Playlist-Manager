#ifndef DATA_COORDINATOR_HPP
#define DATA_COORDINATOR_HPP

#include "Variables.hpp"
#include "DatabaseWorker.hpp"
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
};

}

#endif // DATA_COORDINATOR_HPP

