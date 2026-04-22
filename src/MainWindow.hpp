#ifndef MAIN_WINDOW_HPP
#define MAIN_WINDOW_HPP

#include <adwaita.h>

namespace SpotifyPlaylistManager {

class MainWindow {
public:
    static void Activate(GtkApplication* app, gpointer user_data);
};

}

#endif // MAIN_WINDOW_HPP
