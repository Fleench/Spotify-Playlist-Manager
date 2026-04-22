import sys

# Fix main.cpp includes
with open('src/main.cpp', 'r') as f:
    content = f.read()

content = content.replace('#include "DataCoordinator.hpp"\\n#include "SpotifyWorker.hpp"', '#include "SpotifyWorker.hpp"\\n#include "DataCoordinator.hpp"')

with open('src/main.cpp', 'w') as f:
    f.write(content)


# Fix SpotifyWorker.hpp
with open('src/SpotifyWorker.hpp', 'r') as f:
    content = f.read()

content = content.replace('client->artist().getSeveralArtists(chunk)', 'client->artist().getMultipleArtists(chunk)')

with open('src/SpotifyWorker.hpp', 'w') as f:
    f.write(content)
