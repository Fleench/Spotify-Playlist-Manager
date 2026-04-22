#include <iostream>
#include <fstream>
#include <map>
#include <string>
#include "Variables.hpp"
#include "DatabaseWorker.hpp"
#include "DataCoordinator.hpp"
#include "SpotifyWorker.hpp"
#include "Helpers.hpp"

using namespace SpotifyPlaylistManager;

std::string stripQuotes(const std::string& s) {
    if (s.size() >= 2 && ((s.front() == '"' && s.back() == '"') || (s.front() == '\'' && s.back() == '\''))) {
        return s.substr(1, s.size() - 2);
    }
    return s;
}

std::map<std::string, std::string> loadEnv(const std::string& filepath) {
    std::map<std::string, std::string> env;
    std::ifstream file(filepath);
    if (!file.is_open()) {
        std::cerr << "Could not open " << filepath << ". Please ensure it exists." << std::endl;
        return env;
    }

    std::string line;
    while (std::getline(file, line)) {
        line = Helpers::Trim(line);
        if (line.empty() || line[0] == '#') continue;
        auto delimiterPos = line.find("=");
        if (delimiterPos != std::string::npos) {
            auto key = Helpers::Trim(line.substr(0, delimiterPos));
            auto value = Helpers::Trim(line.substr(delimiterPos + 1));
            env[key] = stripQuotes(value);
        }
    }
    return env;
}

int main(int argc, char** argv) {
    Variables::Init();
    
    // 1. Load keys from .env
    auto env = loadEnv(".env");
    
    std::string clientId = env["SPOTIFY_CLIENT_ID"];
    std::string clientSecret = env["SPOTIFY_CLIENT_SECRET"];
    std::string accessToken = env["SPOTIFY_ACCESS_TOKEN"];
    std::string refreshToken = env["SPOTIFY_REFRESH_TOKEN"];

    if (clientId.empty() || clientSecret.empty()) {
        std::cerr << "Error: SPOTIFY_CLIENT_ID and SPOTIFY_CLIENT_SECRET must be set in .env" << std::endl;
        return 1;
    }

    // Initialize Database
    DatabaseWorker::Init();

    // Try loading tokens from database if they aren't fully provided by .env
    if (accessToken.empty() || accessToken == "your_access_token_here_optional") {
        auto dbAt = DataCoordinator::GetSetting(Variables::Settings::SW_AccessToken);
        if (dbAt) accessToken = dbAt.value();
    }
    
    if (refreshToken.empty() || refreshToken == "your_refresh_token_here_optional") {
        auto dbRt = DataCoordinator::GetSetting(Variables::Settings::SW_RefreshToken);
        if (dbRt) refreshToken = dbRt.value();
    }

    // 2. Init and Authenticate
    std::cout << "Loaded Client ID: " << clientId.substr(0, 4) << "..." << std::endl;
    std::cout << "Loaded Client Secret: " << clientSecret.substr(0, 4) << "..." << std::endl;
    std::cout << "Initializing Spotify Worker..." << std::endl;
    SpotifyWorker::Init(clientId, clientSecret, accessToken, refreshToken);
    
    std::cout << "Authenticating..." << std::endl;
    try {
        auto [newAt, newRt] = SpotifyWorker::Authenticate();
        std::cout << "Authenticated successfully." << std::endl;
        
        // Save back to DB
        DataCoordinator::SetSetting(Variables::Settings::SW_ClientToken, clientId);
        DataCoordinator::SetSetting(Variables::Settings::SW_ClientSecret, clientSecret);
        if (!newAt.empty()) DataCoordinator::SetSetting(Variables::Settings::SW_AccessToken, newAt);
        if (!newRt.empty()) DataCoordinator::SetSetting(Variables::Settings::SW_RefreshToken, newRt);
        
    } catch (const std::exception& e) {
        std::cerr << "Authentication failed with exception: " << e.what() << std::endl;
        std::cerr << "Please ensure your Client ID and Client Secret are correct." << std::endl;
        return 1;
    } catch (...) {
        std::cerr << "Authentication failed with an unknown error." << std::endl;
        return 1;
    }

    // 3. Get first playlist
    std::cout << "Fetching user playlists..." << std::endl;
    auto playlists = SpotifyWorker::GetUserPlaylists();
    
    if (playlists.empty()) {
        std::cout << "No playlists found for this user." << std::endl;
        return 0;
    }

    auto firstPlaylistId = std::get<0>(playlists[0]);
    auto firstPlaylistName = std::get<1>(playlists[0]);
    auto firstPlaylistCount = std::get<2>(playlists[0]);
    
    std::cout << "Found First Playlist: " << firstPlaylistName 
              << " (" << firstPlaylistCount << " tracks)" << std::endl;

    // 4. Fetch playlist data (Track IDs)
    std::cout << "Fetching playlist track IDs..." << std::endl;
    auto [plName, plImg, plId, plDesc, plSnap, plTrackIds] = SpotifyWorker::GetPlaylistData(firstPlaylistId);

    if (plTrackIds.empty()) {
        std::cout << "Playlist is empty or track IDs could not be fetched." << std::endl;
        return 0;
    }

    // 5. List songs
    auto trackIds = Helpers::Split(plTrackIds, ";;");
    std::cout << "\nSongs in Playlist:\n";
    std::cout << "-------------------\n";
    for (size_t i = 0; i < trackIds.size(); ++i) {
        if (trackIds[i].empty()) continue;
        
        auto [sName, sId, sAlbumId, sArtistIds, sDisc, sDur, sExp, sPreview, sNum] = SpotifyWorker::GetSongData(trackIds[i]);
        
        if (sName.empty()) {
            std::cout << (i+1) << ". [Failed to load track data for ID: " << trackIds[i] << "]\n";
        } else {
            std::cout << (i+1) << ". " << sName << "\n";
        }
    }
    
    std::cout << "-------------------\n";
    std::cout << "Test completed successfully." << std::endl;

    return 0;
}
