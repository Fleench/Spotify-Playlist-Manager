#ifndef CACHE_WORKER_HPP
#define CACHE_WORKER_HPP

#include "Variables.hpp"
#include <string>
#include <optional>
#include <curl/curl.h>
#include <fstream>
#include <filesystem>

namespace SpotifyPlaylistManager {

class CacheWorker {
public:
    enum class ImageType {
        Artist,
        Album,
        Playlist
    };

    static std::string ImageTypeToString(ImageType type) {
        switch (type) {
            case ImageType::Artist: return "Artist";
            case ImageType::Album: return "Album";
            case ImageType::Playlist: return "Playlist";
            default: return "Unknown";
        }
    }

    static std::optional<std::string> DownloadImage(const std::string& url, ImageType type, const std::string& itemId) {
        if (url.empty()) return std::nullopt;

        std::filesystem::create_directories(Variables::CachePath);

        // For simplicity in this port, we'll assume .jpg or try to guess from URL
        // In a full implementation we'd check Content-Type via curl
        std::string extension = ".jpg"; 
        if (url.find(".png") != std::string::npos) extension = ".png";
        
        std::string fileName = ImageTypeToString(type) + "_" + itemId + extension;
        std::string path = Variables::CachePath + "/" + fileName;

        CURL* curl = curl_easy_init();
        if (curl) {
            FILE* fp = fopen(path.c_str(), "wb");
            curl_easy_setopt(curl, CURLOPT_URL, url.c_str());
            curl_easy_setopt(curl, CURLOPT_WRITEDATA, fp);
            CURLcode res = curl_easy_perform(curl);
            curl_easy_cleanup(curl);
            fclose(fp);
            if (res == CURLE_OK) return path;
        }
        return std::nullopt;
    }

    static std::optional<std::string> GetImagePath(ImageType type, const std::string& itemId) {
        std::string prefix = ImageTypeToString(type) + "_" + itemId;
        if (!std::filesystem::exists(Variables::CachePath)) return std::nullopt;

        for (const auto& entry : std::filesystem::directory_iterator(Variables::CachePath)) {
            if (entry.path().stem().string() == prefix) {
                return entry.path().string();
            }
        }
        return std::nullopt;
    }
};

}

#endif // CACHE_WORKER_HPP
