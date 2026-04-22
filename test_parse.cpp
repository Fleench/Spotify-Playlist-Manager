#include <iostream>
#include <string>
#include "spotify/spotify.hpp"
#include <nlohmann/json.hpp>

int main() {
    std::string json = R"({
      "href": "https://api.spotify.com/v1/users/smedleysm/playlists?offset=0&limit=2",
      "items": [],
      "limit": 2,
      "next": "https://api.spotify.com/v1/users/smedleysm/playlists?offset=2&limit=2",
      "offset": 0,
      "previous": null,
      "total": 58
    })";

    try {
        nlohmann::json j = nlohmann::json::parse(json);
        // Using generic parser method instead of Parse::playlist as it's private or not directly what we need
        Spotify::PagedPlaylistObject p; // Assuming there is a way to construct it
        // Or actually the API throws. The error says type must be array, but is null
        std::cout << "Test compiled" << std::endl;
    } catch (...) {}
    return 0;
}
