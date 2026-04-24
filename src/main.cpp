#include <adwaita.h>
#include <iostream>
#include <fstream>
#include <map>
#include <string>

#include "Variables.hpp"
#include "DatabaseWorker.hpp"
#include "SpotifyWorker.hpp"
#include "DataCoordinator.hpp"
#include "Helpers.hpp"
#include "MainWindow.hpp"
#include "AI/BrowseView.hpp"
#include "AI/SortView.hpp"

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
    if (!file.is_open()) return env;

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
    const std::string requiredSpotifyScopeVersion = "3";

    Variables::Init();
    DatabaseWorker::Init();

    auto env = loadEnv(".env");
    std::string clientId = env["SPOTIFY_CLIENT_ID"];
    std::string clientSecret = env["SPOTIFY_CLIENT_SECRET"];
    std::string accessToken = env["SPOTIFY_ACCESS_TOKEN"];
    std::string refreshToken = env["SPOTIFY_REFRESH_TOKEN"];

    if (clientId.empty() || clientSecret.empty()) {
        std::cerr << "Error: SPOTIFY_CLIENT_ID and SPOTIFY_CLIENT_SECRET must be set in .env" << std::endl;
        return 1;
    }

    if (accessToken.empty() || accessToken == "your_access_token_here_optional") {
        auto dbAt = DataCoordinator::GetSetting(Variables::Settings::SW_AccessToken);
        if (dbAt) accessToken = dbAt.value();
    }
    
    if (refreshToken.empty() || refreshToken == "your_refresh_token_here_optional") {
        auto dbRt = DataCoordinator::GetSetting(Variables::Settings::SW_RefreshToken);
        if (dbRt) refreshToken = dbRt.value();
    }

    auto storedScopeVersion = DataCoordinator::GetSetting(Variables::Settings::SW_AuthScopeVersion);
    if (!storedScopeVersion || storedScopeVersion.value() != requiredSpotifyScopeVersion) {
        if (!refreshToken.empty() && refreshToken != "your_refresh_token_here_optional") {
            std::cout << "Spotify scope permissions changed. Forcing re-authentication to grant new scopes." << std::endl;
        }
        accessToken.clear();
        refreshToken.clear();
        DataCoordinator::RemoveSetting(Variables::Settings::SW_AccessToken);
        DataCoordinator::RemoveSetting(Variables::Settings::SW_RefreshToken);
    }

    SpotifyWorker::Init(clientId, clientSecret, accessToken, refreshToken);
    
    std::cout << "Authenticating..." << std::endl;
    try {
        auto [newAt, newRt] = SpotifyWorker::Authenticate();
        std::cout << "Authenticated successfully." << std::endl;
        
        DataCoordinator::SetSetting(Variables::Settings::SW_ClientToken, clientId);
        DataCoordinator::SetSetting(Variables::Settings::SW_ClientSecret, clientSecret);
        if (!newAt.empty()) DataCoordinator::SetSetting(Variables::Settings::SW_AccessToken, newAt);
        if (!newRt.empty()) DataCoordinator::SetSetting(Variables::Settings::SW_RefreshToken, newRt);
        DataCoordinator::SetSetting(Variables::Settings::SW_AuthScopeVersion, requiredSpotifyScopeVersion);
    } catch (const std::exception& e) {
        std::cerr << "Auth failed: " << e.what() << std::endl;
        return 1;
    }

    bool browse_mode = false;
    bool sort_mode = false;
    bool sync_mode = false;
    for (int i = 1; i < argc; ++i) {
        std::string arg = argv[i];
        if (arg == "browse") {
            browse_mode = true;
        } else if (arg == "sort") {
            sort_mode = true;
        } else if (arg == "sync") {
            sync_mode = true;
        }
    }

    if (sync_mode) {
        std::cout << "Starting CLI Sync..." << std::endl;
        DataCoordinator::Sync();
        std::cout << "CLI Sync complete." << std::endl;
        return 0;
    }

    // Launch GTK/Adwaita Application
    AdwApplication* app = adw_application_new("com.example.SpotifyPlaylistManager", G_APPLICATION_HANDLES_COMMAND_LINE);
    
    // We need to handle the command-line signal to prevent GApplication from complaining about unknown options
    g_signal_connect(app, "command-line", G_CALLBACK(+[](GApplication* application, GApplicationCommandLine* cmdline, gpointer user_data) -> int {
        g_application_activate(application);
        return 0;
    }), NULL);

    if (browse_mode) {
        g_signal_connect(app, "activate", G_CALLBACK(BrowseView::Activate), NULL);
    } else if (sort_mode) {
        g_signal_connect(app, "activate", G_CALLBACK(SortView::Activate), NULL);
    } else {
        g_signal_connect(app, "activate", G_CALLBACK(MainWindow::Activate), NULL);
    }

    int status = g_application_run(G_APPLICATION(app), argc, argv);
    g_object_unref(app);

    return status;
}
