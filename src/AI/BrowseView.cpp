#include "BrowseView.hpp"
#include "DatabaseWorker.hpp"
#include "Helpers.hpp"
#include <vector>
#include <string>
#include <algorithm>
#include <set>
#include <fstream>

namespace SpotifyPlaylistManager {

static GtkListBox* g_playlist_list = nullptr;
static GtkListBox* g_playlist_track_list = nullptr;

static GtkListBox* g_album_list = nullptr;
static GtkListBox* g_album_track_list = nullptr;

static GtkListBox* g_artist_list = nullptr;
static GtkListBox* g_artist_track_list = nullptr;

static GtkListBox* g_all_songs_list = nullptr;

static std::set<std::string> load_sortable_playlists() {
    std::set<std::string> ids;
    std::ifstream file("sortable_playlists.txt");
    std::string line;
    while (std::getline(file, line)) {
        line = Helpers::Trim(line);
        if (!line.empty()) ids.insert(line);
    }
    return ids;
}

static void save_sortable_playlists(const std::set<std::string>& ids) {
    std::ofstream file("sortable_playlists.txt");
    for (const auto& id : ids) {
        file << id << "\n";
    }
}

static gboolean on_sortable_toggled(GtkSwitch* sw, gboolean state, gpointer user_data) {
    const char* playlist_id = (const char*)user_data;
    auto ids = load_sortable_playlists();
    if (state) {
        ids.insert(playlist_id);
    } else {
        ids.erase(playlist_id);
    }
    save_sortable_playlists(ids);
    return FALSE;
}

static void clear_list_box(GtkListBox* list_box) {
    GtkWidget* child;
    while ((child = gtk_widget_get_first_child(GTK_WIDGET(list_box))) != nullptr) {
        gtk_list_box_remove(list_box, child);
    }
}

static void populate_track_list(GtkListBox* list_box, const std::vector<std::string>& track_ids) {
    clear_list_box(list_box);
    for (const auto& tid : track_ids) {
        if (tid.empty()) continue;
        auto track_opt = DatabaseWorker::GetTrack(tid);
        std::string label_text = track_opt ? track_opt->Name : "Unknown Track (" + tid + ")";
        
        GtkWidget* row_widget = adw_action_row_new();
        gchar* escaped_title = g_markup_escape_text(label_text.c_str(), -1);
        adw_preferences_row_set_title(ADW_PREFERENCES_ROW(row_widget), escaped_title);
        g_free(escaped_title);
        if (track_opt && !track_opt->ArtistIds.empty()) {
            gchar* escaped_subtitle = g_markup_escape_text(track_opt->ArtistIds.c_str(), -1);
            adw_action_row_set_subtitle(ADW_ACTION_ROW(row_widget), escaped_subtitle);
            g_free(escaped_subtitle);
        }
        gtk_list_box_append(list_box, row_widget);
    }
}

static void on_playlist_selected(GtkListBox* list_box, GtkListBoxRow* row, gpointer user_data) {
    if (!row) return;
    const char* playlist_id = (const char*)g_object_get_data(G_OBJECT(row), "id");
    if (!playlist_id) return;

    auto playlist_opt = DatabaseWorker::GetPlaylist(playlist_id);
    if (!playlist_opt) return;

    populate_track_list(g_playlist_track_list, Helpers::Split(playlist_opt->TrackIDs, Variables::Seperator));
}

static void on_album_selected(GtkListBox* list_box, GtkListBoxRow* row, gpointer user_data) {
    if (!row) return;
    const char* album_id = (const char*)g_object_get_data(G_OBJECT(row), "id");
    if (!album_id) return;

    auto album_opt = DatabaseWorker::GetAlbum(album_id);
    if (!album_opt) return;

    populate_track_list(g_album_track_list, Helpers::Split(album_opt->TrackIDs, Variables::Seperator));
}

static void on_artist_selected(GtkListBox* list_box, GtkListBoxRow* row, gpointer user_data) {
    if (!row) return;
    const char* artist_id = (const char*)g_object_get_data(G_OBJECT(row), "id");
    if (!artist_id) return;

    clear_list_box(g_artist_track_list);
    auto all_tracks = DatabaseWorker::GetAllTracks();
    std::string artist_id_str(artist_id);

    for (const auto& track : all_tracks) {
        auto artists = Helpers::Split(track.ArtistIds, Variables::Seperator);
        if (std::find(artists.begin(), artists.end(), artist_id_str) != artists.end()) {
            GtkWidget* row_widget = adw_action_row_new();
            gchar* escaped_title = g_markup_escape_text(track.Name.c_str(), -1);
            adw_preferences_row_set_title(ADW_PREFERENCES_ROW(row_widget), escaped_title);
            g_free(escaped_title);
            gtk_list_box_append(g_artist_track_list, row_widget);
        }
    }
}

void BrowseView::Activate(GtkApplication* app, gpointer user_data) {
    GtkBuilder* builder = gtk_builder_new_from_file("src/AI/browse.ui");
    
    GtkWidget* window = GTK_WIDGET(gtk_builder_get_object(builder, "browse_window"));
    gtk_window_set_application(GTK_WINDOW(window), app);

    // Playlists
    g_playlist_list = GTK_LIST_BOX(gtk_builder_get_object(builder, "playlist_list"));
    g_playlist_track_list = GTK_LIST_BOX(gtk_builder_get_object(builder, "playlist_track_list"));
    auto playlists = DatabaseWorker::GetAllPlaylists();
    
    auto sortable_ids = load_sortable_playlists();

    for (const auto& pl : playlists) {
        GtkWidget* row = adw_action_row_new();
        gchar* escaped = g_markup_escape_text(pl.Name.c_str(), -1);
        adw_preferences_row_set_title(ADW_PREFERENCES_ROW(row), escaped);
        g_free(escaped);
        g_object_set_data_full(G_OBJECT(row), "id", g_strdup(pl.Id.c_str()), g_free);
        
        GtkWidget* sw = gtk_switch_new();
        gtk_switch_set_active(GTK_SWITCH(sw), sortable_ids.count(pl.Id) > 0);
        adw_action_row_add_suffix(ADW_ACTION_ROW(row), sw);
        adw_action_row_set_activatable_widget(ADW_ACTION_ROW(row), NULL);
        
        g_signal_connect(sw, "state-set", G_CALLBACK(on_sortable_toggled), g_strdup(pl.Id.c_str()));

        gtk_list_box_append(g_playlist_list, row);
        
    }
    g_signal_connect(g_playlist_list, "row-selected", G_CALLBACK(on_playlist_selected), NULL);

    // Albums
    g_album_list = GTK_LIST_BOX(gtk_builder_get_object(builder, "album_list"));
    g_album_track_list = GTK_LIST_BOX(gtk_builder_get_object(builder, "album_track_list"));
    auto albums = DatabaseWorker::GetAllAlbums();
    for (const auto& al : albums) {
        GtkWidget* row = adw_action_row_new();
        gchar* escaped = g_markup_escape_text(al.Name.c_str(), -1);
        adw_preferences_row_set_title(ADW_PREFERENCES_ROW(row), escaped);
        g_free(escaped);
        g_object_set_data_full(G_OBJECT(row), "id", g_strdup(al.Id.c_str()), g_free);
        gtk_list_box_append(g_album_list, row);
    }
    g_signal_connect(g_album_list, "row-selected", G_CALLBACK(on_album_selected), NULL);

    // Artists
    g_artist_list = GTK_LIST_BOX(gtk_builder_get_object(builder, "artist_list"));
    g_artist_track_list = GTK_LIST_BOX(gtk_builder_get_object(builder, "artist_track_list"));
    auto artists = DatabaseWorker::GetAllArtists();
    for (const auto& art : artists) {
        GtkWidget* row = adw_action_row_new();
        gchar* escaped = g_markup_escape_text(art.Name.c_str(), -1);
        adw_preferences_row_set_title(ADW_PREFERENCES_ROW(row), escaped);
        g_free(escaped);
        g_object_set_data_full(G_OBJECT(row), "id", g_strdup(art.Id.c_str()), g_free);
        gtk_list_box_append(g_artist_list, row);
    }
    g_signal_connect(g_artist_list, "row-selected", G_CALLBACK(on_artist_selected), NULL);

    // All Songs
    g_all_songs_list = GTK_LIST_BOX(gtk_builder_get_object(builder, "all_songs_list"));
    auto all_tracks = DatabaseWorker::GetAllTracks();
    for (const auto& track : all_tracks) {
        GtkWidget* row = adw_action_row_new();
        gchar* escaped = g_markup_escape_text(track.Name.c_str(), -1);
        adw_preferences_row_set_title(ADW_PREFERENCES_ROW(row), escaped);
        g_free(escaped);
        if (!track.ArtistIds.empty()) {
            gchar* escaped_subtitle2 = g_markup_escape_text(track.ArtistIds.c_str(), -1);
            adw_action_row_set_subtitle(ADW_ACTION_ROW(row), escaped_subtitle2);
            g_free(escaped_subtitle2);
        }
        gtk_list_box_append(g_all_songs_list, row);
    }

    gtk_window_present(GTK_WINDOW(window));
    g_object_unref(builder);
}

} // namespace SpotifyPlaylistManager
