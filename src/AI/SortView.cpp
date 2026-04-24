#include "SortView.hpp"
#include "DatabaseWorker.hpp"
#include "SpotifyWorker.hpp"
#include "Helpers.hpp"
#include <vector>
#include <string>
#include <algorithm>
#include <set>
#include <fstream>
#include <random>
#include <map>

namespace SpotifyPlaylistManager {

struct SortViewState {
    GtkApplication* app;
    GtkWidget* window;
    AdwStatusPage* song_status;
    GtkImage* song_image;
    GtkListBox* playlist_list;
    std::string current_track_id;
    std::set<std::string> sortable_playlist_ids;
    std::map<std::string, GtkCheckButton*> playlist_checks;
};

static SortViewState g_state;

static std::set<std::string> load_sortable_ids() {
    std::set<std::string> ids;
    std::ifstream file("sortable_playlists.txt");
    std::string line;
    while (std::getline(file, line)) {
        line = Helpers::Trim(line);
        if (!line.empty()) ids.insert(line);
    }
    return ids;
}

static void pick_next_song() {
    auto all_tracks = DatabaseWorker::GetAllTracks();
    auto sortable_ids = load_sortable_ids();
    
    // Get all tracks already in those playlists
    std::set<std::string> tracks_to_skip;
    for (const auto& pid : sortable_ids) {
        auto pl = DatabaseWorker::GetPlaylist(pid);
        if (pl) {
            auto ids = Helpers::Split(pl->TrackIDs, Variables::Seperator);
            for (const auto& tid : ids) tracks_to_skip.insert(tid);
        }
    }

    std::vector<Variables::Track> eligible_tracks;
    for (const auto& t : all_tracks) {
        if (tracks_to_skip.find(t.Id) == tracks_to_skip.end()) {
            eligible_tracks.push_back(t);
        }
    }

    if (eligible_tracks.empty()) {
        adw_status_page_set_title(g_state.song_status, "No more songs to sort!");
        adw_status_page_set_description(g_state.song_status, "All songs are already in the selected playlists.");
        return;
    }

    std::random_device rd;
    std::mt19937 g(rd());
    std::uniform_int_distribution<> dis(0, (int)eligible_tracks.size() - 1);
    auto track = eligible_tracks[dis(g)];
    g_state.current_track_id = track.Id;

    adw_status_page_set_title(g_state.song_status, track.Name.c_str());
    
    // Get album for image
    auto album = DatabaseWorker::GetAlbum(track.AlbumId);
    if (album && !album->ImageURL.empty()) {
        std::string local_path = "/tmp/spotify_song_thumb.jpg";
        std::string cmd = "curl -s -o " + local_path + " " + album->ImageURL;
        system(cmd.c_str());
        gtk_image_set_from_file(g_state.song_image, local_path.c_str());
    } else {
        gtk_image_set_from_icon_name(g_state.song_image, "audio-x-generic-symbolic");
    }

    // Artist name
    std::string artists = "";
    auto artist_ids = Helpers::Split(track.ArtistIds, Variables::Seperator);
    for (const auto& aid : artist_ids) {
        auto art = DatabaseWorker::GetArtist(aid);
        if (art) artists += art->Name + ", ";
    }
    if (!artists.empty()) {
        artists.pop_back();
        artists.pop_back();
    }
    adw_status_page_set_description(g_state.song_status, artists.c_str());

    // Reset checks
    for (auto const& [id, check] : g_state.playlist_checks) {
        gtk_check_button_set_active(check, FALSE);
    }

    // Try play
    if (!SpotifyWorker::PlayTrack(track.Id)) {
        // Maybe show an alert or update button
    }
}

static void on_next_clicked(GtkButton* btn, gpointer user_data) {
    // Add to selected playlists
    std::vector<std::string> tids = { g_state.current_track_id };
    for (auto const& [id, check] : g_state.playlist_checks) {
        if (gtk_check_button_get_active(check)) {
            SpotifyWorker::AddTracksToPlaylist(id, tids);
            
            // Also update local DB
            auto pl = DatabaseWorker::GetPlaylist(id);
            if (pl) {
                if (!pl->TrackIDs.empty()) pl->TrackIDs += Variables::Seperator;
                pl->TrackIDs += g_state.current_track_id;
                DatabaseWorker::SetPlaylist(*pl);
            }
        }
    }
    pick_next_song();
}

static void on_play_clicked(GtkButton* btn, gpointer user_data) {
    if (!SpotifyWorker::PlayTrack(g_state.current_track_id)) {
        AdwDialog* dialog = adw_alert_dialog_new("No Session Found", "Please start Spotify on a device and try again.");
        adw_alert_dialog_add_response(ADW_ALERT_DIALOG(dialog), "ok", "OK");
        adw_alert_dialog_choose(ADW_ALERT_DIALOG(dialog), g_state.window, NULL, NULL, NULL);
    }
}

void SortView::Activate(GtkApplication* app, gpointer user_data) {
    g_state.app = app;
    GtkBuilder* builder = gtk_builder_new_from_file("src/AI/sort.ui");
    g_state.window = GTK_WIDGET(gtk_builder_get_object(builder, "sort_window"));
    gtk_window_set_application(GTK_WINDOW(g_state.window), app);

    g_state.song_status = ADW_STATUS_PAGE(gtk_builder_get_object(builder, "song_status"));
    g_state.song_image = GTK_IMAGE(gtk_builder_get_object(builder, "song_image"));
    g_state.playlist_list = GTK_LIST_BOX(gtk_builder_get_object(builder, "sortable_playlist_list"));

    auto sortable_ids = load_sortable_ids();
    for (const auto& id : sortable_ids) {
        auto pl = DatabaseWorker::GetPlaylist(id);
        if (!pl) continue;

        GtkWidget* row = adw_action_row_new();
        gchar* escaped = g_markup_escape_text(pl->Name.c_str(), -1);
        adw_preferences_row_set_title(ADW_PREFERENCES_ROW(row), escaped);
        g_free(escaped);
        
        GtkWidget* check = gtk_check_button_new();
        adw_action_row_add_prefix(ADW_ACTION_ROW(row), check);
        g_state.playlist_checks[id] = GTK_CHECK_BUTTON(check);

        gtk_list_box_append(g_state.playlist_list, row);
    }

    GtkWidget* next_btn = GTK_WIDGET(gtk_builder_get_object(builder, "next_button"));
    g_signal_connect(next_btn, "clicked", G_CALLBACK(on_next_clicked), NULL);

    GtkWidget* play_btn = GTK_WIDGET(gtk_builder_get_object(builder, "play_button"));
    g_signal_connect(play_btn, "clicked", G_CALLBACK(on_play_clicked), NULL);

    gtk_window_present(GTK_WINDOW(g_state.window));
    g_object_unref(builder);

    pick_next_song();
}

} // namespace SpotifyPlaylistManager
