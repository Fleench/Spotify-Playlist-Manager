#include <iostream>
#include <string>
#include <nlohmann/json.hpp>
#include <vector>

int main() {
    std::string json = R"({
      "href": "https://api.spotify.com/v1/users/smedleysm/playlists?offset=0&limit=2",
      "items": null,
      "limit": 2,
      "next": "https://api.spotify.com/v1/users/smedleysm/playlists?offset=2&limit=2",
      "offset": 0,
      "previous": null,
      "total": 58
    })";

    try {
        nlohmann::json j = nlohmann::json::parse(json);
        std::vector<int> items = j.value("items", std::vector<int>{});
        std::cout << "Parsed " << items.size() << " total items." << std::endl;
    } catch (const std::exception& e) {
        std::cerr << "Error: " << e.what() << std::endl;
    }
    return 0;
}
