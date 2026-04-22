import sys

with open('src/SpotifyWorker.hpp', 'r') as f:
    content = f.read()

# Fix getSavedAlbums -> getUsersSavedAlbums
content = content.replace('client->album().getSavedAlbums()', 'client->album().getUsersSavedAlbums()')
# Fix getArtists -> getSeveralArtists (or whatever it's called)
content = content.replace('client->artist().getArtists(chunk)', 'client->artist().getSeveralArtists(chunk)')

# Fix vector push_back tuple issues (brace enclosed initialization issues in C++)
content = content.replace('results.push_back({item.album.id, item.album.name, item.album.total_tracks, artistIds});', 'results.emplace_back(item.album.id, item.album.name, item.album.total_tracks, artistIds);')
content = content.replace('results.push_back({artId, art.name, imageUrl, genres});', 'results.emplace_back(artId, art.name, imageUrl, genres);')

with open('src/SpotifyWorker.hpp', 'w') as f:
    f.write(content)
