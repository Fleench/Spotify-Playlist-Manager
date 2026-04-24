#include "MainWindow.hpp"
#include "DataCoordinator.hpp"
#include "Logger.hpp"
#include <iostream>
#include <thread>

namespace SpotifyPlaylistManager {

static GtkLabel* g_log_label = nullptr;

struct LogTaskData {
    std::string text;
};

static void on_sync_clicked(GtkButton* button, gpointer user_data) {
    gtk_widget_set_sensitive(GTK_WIDGET(button), FALSE);

    Logger::Log("Starting Full Sync...", LogType::Info);

    // Run sync in a background thread
    std::thread([](GtkButton* btn) {
        DataCoordinator::Sync();
        
        Logger::Log("Sync Complete", LogType::Success);

        // Schedule UI update back on the main thread
        g_idle_add([](gpointer data) -> gboolean {
            GtkButton* b = GTK_BUTTON(data);
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
    g_log_label = GTK_LABEL(gtk_builder_get_object(builder, "log_label"));
    
    g_signal_connect(sync_button, "clicked", G_CALLBACK(on_sync_clicked), NULL);

    Logger::SetCallback([](std::string message, LogType type) {
        // Console output using Terminal format
        std::cout << LogFormatter::StandardFormat(message, type, "Terminal") << std::endl;

        // Safely update GTK Label using SyncView format
        if (g_log_label) {
            LogTaskData* taskData = new LogTaskData{LogFormatter::StandardFormat(message, type, "SyncView")};
            g_idle_add([](gpointer d) -> gboolean {
                LogTaskData* data = static_cast<LogTaskData*>(d);
                gtk_label_set_text(g_log_label, data->text.c_str());
                delete data;
                return G_SOURCE_REMOVE;
            }, taskData);
        }
    });

    gtk_window_present(GTK_WINDOW(window));
    g_object_unref(builder);
}

} // namespace SpotifyPlaylistManager
