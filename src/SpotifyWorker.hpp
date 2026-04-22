#ifndef SPOTIFY_WORKER_HPP
#define SPOTIFY_WORKER_HPP

#include <spotify/spotify.hpp>
#include <string>
#include <vector>
#include <tuple>
#include <optional>
#include <iostream>
#include <chrono>

namespace SpotifyPlaylistManager {

class SpotifyWorker {
private:
    static inline std::unique_ptr<Spotify::Auth> auth;
    static inline std::unique_ptr<Spotify::Client> client;
    static inline std::string clientId;
    static inline std::string clientSecret;
    static inline std::string accessToken;
    static inline std::string refreshToken;

public:
    static void Init(const std::string& ck, const std::string& cs, const std::string& at = "", const std::string& rt = "") {
        clientId = ck;
        clientSecret = cs;
        accessToken = at;
        refreshToken = rt;
        
        auth = std::make_unique<Spotify::Auth>(Spotify::Auth({ck, cs}));
        if (!rt.empty() && rt != "your_refresh_token_here_optional") {
            try {
                if (auth->begin(rt)) {
                    client = std::make_unique<Spotify::Client>(*auth);
                } else {
                    std::cerr << "Refresh token invalid. Proceeding to browser auth." << std::endl;
                }
            } catch (...) {
            }
        }
    }

    static std::tuple<std::string, std::string> Authenticate() {
        if (!auth) return {"", ""};
        if (client) {
            accessToken = auth->getAccessToken();
            try {
                refreshToken = auth->getRefreshToken();
            } catch (...) {}
            return {accessToken, refreshToken};
        }
        
        auto url = auth->createAuthoriseURL("http://127.0.0.1:5543/callback", { 
            Spotify::Scope::UserReadPlaybackState, 
            Spotify::Scope::UserModifyPlaybackState,
            Spotify::Scope::PlaylistReadPrivate,
            Spotify::Scope::PlaylistModifyPrivate,
            Spotify::Scope::PlaylistModifyPublic,
            Spotify::Scope::UserLibraryRead
        });
        
        Spotify::AuthServer::openBrowser(url);
        std::string code = Spotify::AuthServer::waitForCode("127.0.0.1", 5543);
        auth->exchangeCode(code);
        
        client = std::make_unique<Spotify::Client>(*auth);
        accessToken = auth->getAccessToken();
        try {
            refreshToken = auth->getRefreshToken();
        } catch (...) {}
        return {accessToken, refreshToken};
    }

    
    static std::vector<std::tuple<std::string, std::string, int>> GetUserPlaylists() {
        if (!client) return {};
        std::vector<std::tuple<std::string, std::string, int>> results;
        try {
            auto page = client->playlist().getCurrentUsersPlaylists(50);
            for (const auto& pl : page.items) {
                results.push_back({pl.id, pl.name, pl.tracks.total});
            }
        } catch (const Spotify::APIException& e) {
            std::cerr << "GetUserPlaylists Spotify::APIException: " << e.what() << " - Reason: " << e.reason() << std::endl;
        } catch (const std::exception& e) {
            std::cerr << "GetUserPlaylists std::exception: " << e.what() << std::endl;
        } catch (...) {
            std::cerr << "GetUserPlaylists unknown exception" << std::endl;
        }
        return results;
    }

    static std::tuple<std::string, std::string> GetCurrentTokens() {
        return {accessToken, refreshToken};
    }

    static std::tuple<std::string, std::string, std::string, std::string, std::string, std::string> GetPlaylistData(const std::string& id) {
        if (!client) return {"", "", "", "", "", ""};
        try {
            auto pl = client->playlist().getPlaylist(id);
            std::string imageUrl = pl.images.empty() ? "" : pl.images[0].url;
            
            std::string trackIds = "";
            for (const auto& item : pl.tracks.items) {
                if (auto trackPtr = std::get_if<std::shared_ptr<Spotify::TrackObject>>(&item.track)) {
                    if (*trackPtr) trackIds += (*trackPtr)->id + ";;";
                }
            }
            if (!trackIds.empty()) trackIds.pop_back();
            if (!trackIds.empty()) trackIds.pop_back();

            return {pl.name, imageUrl, pl.id, pl.description.value_or(""), pl.snapshot_id, trackIds};
        } catch (const Spotify::APIException& e) {
            std::cerr << "GetPlaylistData API Exception: " << e.what() << " - Reason: " << e.reason() << std::endl;
            return {"", "", "", "", "", ""};
        } catch (const std::exception& e) {
            std::cerr << "GetPlaylistData std::exception: " << e.what() << std::endl;
            return {"", "", "", "", "", ""};
        } catch (...) {
            std::cerr << "GetPlaylistData unknown exception" << std::endl;
            return {"", "", "", "", "", ""};
        }
    }

    static std::tuple<std::string, std::string, std::string, std::string, std::string> GetAlbumData(const std::string& id) {
        if (!client) return {"", "", "", "", ""};
        try {
            auto al = client->album().getAlbum(id);
            std::string imageUrl = al.images.empty() ? "" : al.images[0].url;
            
            std::string trackIds = "";
            for (const auto& item : al.tracks.items) {
                trackIds += item.id + ";;";
            }
            
            std::string artistIds = "";
            for (const auto& art : al.artists) {
                artistIds += art.id + ";;";
            }

            return {al.name, imageUrl, al.id, trackIds, artistIds};
        } catch (const Spotify::APIException& e) {
            std::cerr << "GetAlbumData API Exception: " << e.what() << " - Reason: " << e.reason() << std::endl;
            return {"", "", "", "", ""};
        } catch (const std::exception& e) {
            std::cerr << "GetAlbumData std::exception: " << e.what() << std::endl;
            return {"", "", "", "", ""};
        } catch (...) {
            std::cerr << "GetAlbumData unknown exception" << std::endl;
            return {"", "", "", "", ""};
        }
    }

    static std::tuple<std::string, std::string, std::string, std::string, int, int, bool, std::string, int> GetSongData(const std::string& id) {
        if (!client) return {"", "", "", "", 0, 0, false, "", 0};
        try {
            auto tr = client->track().getTrack(id);
            
            std::string artistIds = "";
            for (const auto& art : tr.artists) {
                artistIds += art.id + ";;";
            }

            return {tr.name, tr.id, tr.album.id, artistIds, tr.disc_number, tr.duration_ms, tr.is_explicit, tr.preview_url.value_or(""), tr.track_number};
        } catch (const Spotify::APIException& e) {
            std::cerr << "GetSongData API Exception: " << e.what() << " - Reason: " << e.reason() << std::endl;
            return {"", "", "", "", 0, 0, false, "", 0};
        } catch (const std::exception& e) {
            std::cerr << "GetSongData std::exception: " << e.what() << std::endl;
            return {"", "", "", "", 0, 0, false, "", 0};
        } catch (...) {
            std::cerr << "GetSongData unknown exception" << std::endl;
            return {"", "", "", "", 0, 0, false, "", 0};
        }
    }

    static std::tuple<std::string, std::string, std::string, std::string> GetArtistData(const std::string& id) {
        if (!client) return {"", "", "", ""};
        try {
            auto art = client->artist().getArtist(id);
            std::string imageUrl = art.images.empty() ? "" : art.images[0].url;
            
            std::string genres = "";
            for (const auto& g : art.genres) {
                genres += g + ";;";
            }

            std::string artId = art.uri;
            auto pos = artId.find_last_of(':');
            if (pos != std::string::npos) {
                artId = artId.substr(pos + 1);
            }

            return {artId, art.name, imageUrl, genres};
        } catch (const Spotify::APIException& e) {
            std::cerr << "GetArtistData API Exception: " << e.what() << " - Reason: " << e.reason() << std::endl;
            return {"", "", "", ""};
        } catch (const std::exception& e) {
            std::cerr << "GetArtistData std::exception: " << e.what() << std::endl;
            return {"", "", "", ""};
        } catch (...) {
            std::cerr << "GetArtistData unknown exception" << std::endl;
            return {"", "", "", ""};
        }
    }

    
    static std::vector<std::tuple<std::string, std::string, std::string>> GetLikedSongs() {
        if (!client) return {};
        std::vector<std::tuple<std::string, std::string, std::string>> results;
        try {
            auto page = client->track().getUserSavedTracks();
            for (const auto& item : page.items) {
                std::string artistIds = "";
                for (const auto& art : item.track.artists) {
                    artistIds += art.id + ";;";
                }
                results.push_back({item.track.id, item.track.name, artistIds});
            }
        } catch (...) {}
        return results;
    }

    static std::vector<std::tuple<std::string, std::string, int, std::string>> GetUserAlbums() {
        if (!client) return {};
        std::vector<std::tuple<std::string, std::string, int, std::string>> results;
        try {
            auto page = client->album().getUsersSavedAlbums();
            for (const auto& item : page.items) {
                std::string artistIds = "";
                for (const auto& art : item.album.artists) {
                    artistIds += art.id + ";;";
                }
                results.emplace_back(item.album.id, item.album.name, item.album.total_tracks, artistIds);
            }
        } catch (...) {}
        return results;
    }

    static std::vector<std::tuple<std::string, std::string, std::string, std::string, std::string, std::string>> GetPlaylistDataBatch(const std::vector<std::string>& ids) {
        std::vector<std::tuple<std::string, std::string, std::string, std::string, std::string, std::string>> results;
        for (const auto& id : ids) {
            auto data = GetPlaylistData(id);
            if (!std::get<0>(data).empty()) {
                results.push_back(data);
            }
        }
        return results;
    }

    static std::vector<std::tuple<std::string, std::string, std::string, std::string, std::string>> GetAlbumDataBatch(const std::vector<std::string>& ids) {
        if (!client) return {};
        std::vector<std::tuple<std::string, std::string, std::string, std::string, std::string>> results;
        for (const auto& id : ids) {
            auto data = GetAlbumData(id);
            if (!std::get<0>(data).empty()) {
                results.push_back(data);
            }
        }
        return results;
    }

    static std::vector<std::tuple<std::string, std::string, std::string, std::string, int, int, bool, std::string, int>> GetSongDataBatch(const std::vector<std::string>& ids) {
        if (!client) return {};
        std::vector<std::tuple<std::string, std::string, std::string, std::string, int, int, bool, std::string, int>> results;
        
        // Spotify API supports batching up to 50 tracks
        for (size_t i = 0; i < ids.size(); i += 50) {
            std::vector<std::string> chunk;
            for (size_t j = i; j < i + 50 && j < ids.size(); j++) {
                chunk.push_back(ids[j]);
            }
            try {
                auto trackList = client->track().getTracks(chunk);
                for (const auto& tr : trackList.tracks) {
                    std::string artistIds = "";
                    for (const auto& art : tr.artists) {
                        artistIds += art.id + ";;";
                    }
                    results.push_back({tr.name, tr.id, tr.album.id, artistIds, tr.disc_number, tr.duration_ms, tr.is_explicit, tr.preview_url.value_or(""), tr.track_number});
                }
            } catch (...) {}
        }
        return results;
    }

    static std::vector<std::tuple<std::string, std::string, std::string, std::string>> GetArtistDataBatch(const std::vector<std::string>& ids) {
        if (!client) return {};
        std::vector<std::tuple<std::string, std::string, std::string, std::string>> results;
        
        // Spotify API supports batching up to 50 artists
        for (size_t i = 0; i < ids.size(); i += 50) {
            std::vector<std::string> chunk;
            for (size_t j = i; j < i + 50 && j < ids.size(); j++) {
                chunk.push_back(ids[j]);
            }
            try {
                auto artistList = client->artist().getMultipleArtists(chunk);
                for (const auto& art : artistList.artists) {
                    std::string imageUrl = art.images.empty() ? "" : art.images[0].url;
                    std::string genres = "";
                    for (const auto& g : art.genres) {
                        genres += g + ";;";
                    }
                    
                    std::string artId = art.uri;
                    auto pos = artId.find_last_of(':');
                    if (pos != std::string::npos) {
                        artId = artId.substr(pos + 1);
                    }

                    results.emplace_back(artId, art.name, imageUrl, genres);
                }
            } catch (...) {}
        }
        return results;
    }

    static void AddTracksToPlaylist(const std::string& playlistId, const std::vector<std::string>& trackIds) {
        if (!client) return;
        try {
            client->playlist().addItemsToPlaylist(playlistId, trackIds);
        } catch (...) {}
    }
};

}

#endif // SPOTIFY_WORKER_HPP

