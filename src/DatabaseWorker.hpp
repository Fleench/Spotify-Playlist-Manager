#ifndef DATABASE_WORKER_HPP
#define DATABASE_WORKER_HPP

#include "Variables.hpp"
#include <string>
#include <vector>
#include <mutex>
#include <optional>

namespace SpotifyPlaylistManager {

class DatabaseWorker {
public:
    static void Init();

    // Settings
    static void SetSetting(const std::string& key, const std::string& value);
    static std::optional<std::string> GetSetting(const std::string& key);
    static void RemoveSetting(const std::string& key);
    static std::vector<std::pair<std::string, std::string>> GetAllSettings();

    // Playlists
    static void SetPlaylist(const Variables::PlayList& playlist);
    static std::optional<Variables::PlayList> GetPlaylist(const std::string& id);
    static void RemovePlaylist(const std::string& id);
    static std::vector<Variables::PlayList> GetAllPlaylists();

    // Albums
    static void SetAlbum(const Variables::Album& album);
    static void EnsureAlbumExists(const std::string& id);
    static std::optional<Variables::Album> GetAlbum(const std::string& id);
    static void RemoveAlbum(const std::string& id);
    static std::vector<Variables::Album> GetAllAlbums();

    // Tracks
    static void SetTrack(const Variables::Track& track);
    static void EnsureTrackExists(const std::string& id);
    static std::optional<Variables::Track> GetTrack(const std::string& id);
    static void RemoveTrack(const std::string& id);
    static std::vector<Variables::Track> GetAllTracks();
    static int GetTrackCountBySongId(const std::string& songId);

    // Artists
    static void SetArtist(const Variables::Artist& artist);
    static void EnsureArtistExists(const std::string& id);
    static std::optional<Variables::Artist> GetArtist(const std::string& id);
    static void RemoveArtist(const std::string& id);
    static std::vector<Variables::Artist> GetAllArtists();

    // Similarity
    static void SetSimilar(const std::string& songId1, const std::string& songId2, const std::string& type);
    static void RemoveSimilar(const std::string& songId1, const std::string& songId2, const std::string& type);
    static std::vector<std::tuple<std::string, std::string, std::string>> GetAllSimilar();

    static void SetMightBeSimilar(const std::string& songId1, const std::string& songId2);
    static void RemoveMightBeSimilar(const std::string& songId1, const std::string& songId2);
    static std::vector<std::pair<std::string, std::string>> GetAllMightBeSimilar();

private:
    static std::mutex dbMutex;
    static std::string GetAlbumTrackIds(void* db, const std::string& albumId);
};

}

#endif // DATABASE_WORKER_HPP
