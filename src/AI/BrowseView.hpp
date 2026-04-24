#ifndef BROWSE_VIEW_HPP
#define BROWSE_VIEW_HPP

#include <adwaita.h>

namespace SpotifyPlaylistManager {

class BrowseView {
public:
    static void Activate(GtkApplication* app, gpointer user_data);
};

}

#endif // BROWSE_VIEW_HPP
