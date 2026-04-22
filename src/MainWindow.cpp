#include "MainWindow.hpp"
#include "DataCoordinator.hpp"
#include <iostream>
#include <thread>

namespace SpotifyPlaylistManager {

static void on_sync_clicked(GtkButton* button, gpointer user_data) {
    gtk_widget_set_sensitive(GTK_WIDGET(button), FALSE);
    gtk_button_set_label(button, "Syncing... (Check Console)");

    // Run sync in a background thread
    std::thread([](GtkButton* btn) {
        std::cout << "\n--- Starting Full Sync ---" << std::endl;
        DataCoordinator::Sync();
        std::cout << "--- Sync Complete ---\n" << std::endl;

        // Schedule UI update back on the main thread
        g_idle_add([](gpointer data) -> gboolean {
            GtkButton* b = GTK_BUTTON(data);
            gtk_button_set_label(b, "Sync Complete");
            gtk_widget_set_sensitive(GTK_WIDGET(b), TRUE);
            return G_SOURCE_REMOVE;
        }, btn);
    }, button).detach();
}

void MainWindow::Activate(GtkApplication* app, gpointer user_data) {
    GtkBuilder* builder = gtk_builder_new_from_file("src/window.ui");
    
    GtkWidget* window = GTK_WIDGET(gtk_builder_get_object(builder, "main_window"));
    gtk_window_set_application(GTK_WINDOW(window), app);

    GtkWidget* sync_button = GTK_WIDGET(gtk_builder_get_object(builder, "sync_button"));
    g_signal_connect(sync_button, "clicked", G_CALLBACK(on_sync_clicked), NULL);

    gtk_window_present(GTK_WINDOW(window));
    g_object_unref(builder);
}

} // namespace SpotifyPlaylistManager
