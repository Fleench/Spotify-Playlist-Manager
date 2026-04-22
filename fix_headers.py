import sys

with open('src/DataCoordinator.hpp', 'r') as f:
    content = f.read()

content = content.replace('#include "DatabaseWorker.hpp"\\n#include "SpotifyWorker.hpp"', '#include "DatabaseWorker.hpp"\n#include "SpotifyWorker.hpp"')

with open('src/DataCoordinator.hpp', 'w') as f:
    f.write(content)
