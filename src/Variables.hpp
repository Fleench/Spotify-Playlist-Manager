#ifndef VARIABLES_HPP
#define VARIABLES_HPP

#include <string>
#include <vector>
#include <random>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <algorithm>

namespace SpotifyPlaylistManager {

namespace Variables {
    // App Info
    const std::string AppName = "SpotifyPlaylistManager";
    const std::string AppVersion = "0.0.0";

    // Base Directories
    inline std::string GetAppDataPath() {
        const char* home = getenv("HOME");
        if (home) {
            return std::string(home) + "/.config/" + AppName;
        }
        return "./" + AppName;
    }

    inline std::string AppDataPath = GetAppDataPath();
    inline std::string ConfigPath = AppDataPath + "/config.json";
    inline std::string DatabasePath = AppDataPath + "/data.db";
    inline std::string CachePath = AppDataPath + "/cache";

    const std::string Seperator = ";;";
    const std::string Identifier = "CIID";
    const std::string IdentifierDelimiter = "___";

    inline std::string MakeId(std::string type = "SNG", int length = 30) {
        const std::string allowedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        static std::random_device rd;
        static std::mt19937 gen(rd());
        std::uniform_int_distribution<> dis(0, allowedChars.length() - 1);

        std::string songId = Identifier + IdentifierDelimiter + type + IdentifierDelimiter;
        for (int i = 0; i < length; ++i) {
            songId += allowedChars[dis(gen)];
        }
        return songId;
    }

    inline void Init() {
        std::filesystem::create_directories(AppDataPath);
        std::filesystem::create_directories(CachePath);

        if (!std::filesystem::exists(DatabasePath)) {
            // Create empty file
            std::ofstream(DatabasePath).close();
        }
        std::cout << DatabasePath << std::endl;
    }

    struct PlayList {
        std::string Name;
        std::string ImageURL;
        std::string ImagePath = "";
        std::string Id;
        std::string Description;
        std::string SnapshotID;
        std::string TrackIDs;

        bool MissingInfo() const {
            return Name.empty() || Id.empty() || SnapshotID.empty() || TrackIDs.empty();
        }
    };

    struct Album {
        std::string Name;
        std::string ImageURL;
        std::string ImagePath = "";
        std::string Id;
        std::string ArtistIDs;
        std::string TrackIDs;

        bool MissingInfo() const {
            return Name.empty() || Id.empty() || ArtistIDs.empty() || TrackIDs.empty();
        }
    };

    struct Track {
        std::string Name;
        std::string Id;
        std::string AlbumId;
        std::string ArtistIds;
        int DiscNumber = 0;
        int DurationMs = 0;
        bool Explicit = false;
        std::string PreviewUrl;
        int TrackNumber = 0;
        std::string SongID;

        Track() {
            SongID = MakeId();
        }

        bool MissingInfo() const {
            if (Name.empty() || Id.empty() || AlbumId.empty() || ArtistIds.empty() || SongID.empty()) {
                return true;
            }
            if (DurationMs <= 0 || DiscNumber <= 0 || TrackNumber <= 0) {
                return true;
            }
            return false;
        }
    };

    struct Artist {
        std::string Name;
        std::string ImageURL;
        std::string ImagePath = "";
        std::string Id;
        std::string Genres;

        bool MissingInfo() const {
            return Name.empty() || Id.empty();
        }
    };

    namespace Settings {
        const std::string SW_AccessToken = "SW_AccessToken";
        const std::string SW_RefreshToken = "SW_RefreshToken";
        const std::string SW_ClientToken = "SW_ClientToken";
        const std::string SW_ClientSecret = "SW_ClientSecret";
    }
}

}

#endif // VARIABLES_HPP
