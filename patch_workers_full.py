import sys

# 1. Update SpotifyWorker.hpp
with open('src/SpotifyWorker.hpp', 'r') as f:
    sw = f.read()

# Add missing backend methods to SpotifyWorker
new_methods = """
    static std::vector<std::tuple<std::string, std::string, std::string>> GetLikedSongs() {
        if (!client) return {};
        std::vector<std::tuple<std::string, std::string, std::string>> results;
        try {
            auto page = client->track().getUserSavedTracks();
            for (const auto& item : page.items) {
                std::string artistIds = "";
                for (const auto& art : item.track.artists) {
                    artistIds += art.id + ";;";
                }
                results.push_back({item.track.id, item.track.name, artistIds});
            }
        } catch (...) {}
        return results;
    }

    static std::vector<std::tuple<std::string, std::string, int, std::string>> GetUserAlbums() {
        if (!client) return {};
        std::vector<std::tuple<std::string, std::string, int, std::string>> results;
        try {
            auto page = client->album().getSavedAlbums();
            for (const auto& item : page.items) {
                std::string artistIds = "";
                for (const auto& art : item.album.artists) {
                    artistIds += art.id + ";;";
                }
                results.push_back({item.album.id, item.album.name, item.album.total_tracks, artistIds});
            }
        } catch (...) {}
        return results;
    }

    static std::vector<std::tuple<std::string, std::string, std::string, std::string, std::string, std::string>> GetPlaylistDataBatch(const std::vector<std::string>& ids) {
        std::vector<std::tuple<std::string, std::string, std::string, std::string, std::string, std::string>> results;
        for (const auto& id : ids) {
            auto data = GetPlaylistData(id);
            if (!std::get<0>(data).empty()) {
                results.push_back(data);
            }
        }
        return results;
    }

    static std::vector<std::tuple<std::string, std::string, std::string, std::string, std::string>> GetAlbumDataBatch(const std::vector<std::string>& ids) {
        if (!client) return {};
        std::vector<std::tuple<std::string, std::string, std::string, std::string, std::string>> results;
        for (const auto& id : ids) {
            auto data = GetAlbumData(id);
            if (!std::get<0>(data).empty()) {
                results.push_back(data);
            }
        }
        return results;
    }

    static std::vector<std::tuple<std::string, std::string, std::string, std::string, int, int, bool, std::string, int>> GetSongDataBatch(const std::vector<std::string>& ids) {
        if (!client) return {};
        std::vector<std::tuple<std::string, std::string, std::string, std::string, int, int, bool, std::string, int>> results;
        
        // Spotify API supports batching up to 50 tracks
        for (size_t i = 0; i < ids.size(); i += 50) {
            std::vector<std::string> chunk;
            for (size_t j = i; j < i + 50 && j < ids.size(); j++) {
                chunk.push_back(ids[j]);
            }
            try {
                auto trackList = client->track().getTracks(chunk);
                for (const auto& tr : trackList.tracks) {
                    std::string artistIds = "";
                    for (const auto& art : tr.artists) {
                        artistIds += art.id + ";;";
                    }
                    results.push_back({tr.name, tr.id, tr.album.id, artistIds, tr.disc_number, tr.duration_ms, tr.is_explicit, tr.preview_url.value_or(""), tr.track_number});
                }
            } catch (...) {}
        }
        return results;
    }

    static std::vector<std::tuple<std::string, std::string, std::string, std::string>> GetArtistDataBatch(const std::vector<std::string>& ids) {
        if (!client) return {};
        std::vector<std::tuple<std::string, std::string, std::string, std::string>> results;
        
        // Spotify API supports batching up to 50 artists
        for (size_t i = 0; i < ids.size(); i += 50) {
            std::vector<std::string> chunk;
            for (size_t j = i; j < i + 50 && j < ids.size(); j++) {
                chunk.push_back(ids[j]);
            }
            try {
                auto artistList = client->artist().getArtists(chunk);
                for (const auto& art : artistList.artists) {
                    std::string imageUrl = art.images.empty() ? "" : art.images[0].url;
                    std::string genres = "";
                    for (const auto& g : art.genres) {
                        genres += g + ";;";
                    }
                    
                    std::string artId = art.uri;
                    auto pos = artId.find_last_of(':');
                    if (pos != std::string::npos) {
                        artId = artId.substr(pos + 1);
                    }

                    results.push_back({artId, art.name, imageUrl, genres});
                }
            } catch (...) {}
        }
        return results;
    }
"""

if "GetLikedSongs" not in sw:
    sw = sw.replace("static void AddTracksToPlaylist", new_methods + "\n    static void AddTracksToPlaylist")
    with open('src/SpotifyWorker.hpp', 'w') as f:
        f.write(sw)


# 2. Update DataCoordinator.hpp
with open('src/DataCoordinator.hpp', 'r') as f:
    dc = f.read()

sync_methods = """
    // --- Synchronization Logic ---
    static void SlowSync() {
        Sync();
    }

    static void Sync() {
        SyncPlaylists();
        SyncAlbums();
        SyncLikedSongs();
        SyncTrackMetadata();
        SyncArtistMetadata();
    }

    static int UpdateArtistIdSeparators() {
        int updates = 0;
        for (auto& track : DatabaseWorker::GetAllTracks()) {
            std::string current = track.ArtistIds;
            std::string normalized = NormalizeArtistIdString(current);
            if (current != normalized) {
                track.ArtistIds = normalized;
                DatabaseWorker::SetTrack(track);
                updates++;
            }
        }
        for (auto& album : DatabaseWorker::GetAllAlbums()) {
            std::string current = album.ArtistIDs;
            std::string normalized = NormalizeArtistIdString(current);
            if (current != normalized) {
                album.ArtistIDs = normalized;
                DatabaseWorker::SetAlbum(album);
                updates++;
            }
        }
        return updates;
    }

private:
    static std::string NormalizeArtistIdString(const std::string& ids) {
        if (ids.empty()) return "";
        auto parts = Helpers::Split(ids, Variables::Seperator);
        std::vector<std::string> valid;
        for (auto& p : parts) {
            std::string trimmed = Helpers::Trim(p);
            if (!trimmed.empty()) valid.push_back(trimmed);
        }
        if (valid.empty()) return "";
        return Helpers::Join(valid, Variables::Seperator);
    }

    static void SyncPlaylists() {
        auto playlists = SpotifyWorker::GetUserPlaylists();
        for (auto& summary : playlists) {
            std::string pId = std::get<0>(summary);
            auto data = SpotifyWorker::GetPlaylistData(pId);
            if (std::get<0>(data).empty()) continue; // Skip if failed

            Variables::PlayList pl;
            pl.Id = pId;
            pl.Name = std::get<0>(data);
            pl.ImageURL = std::get<1>(data);
            pl.Description = std::get<3>(data);
            pl.SnapshotID = std::get<4>(data);
            pl.TrackIDs = std::get<5>(data);

            auto existing = DatabaseWorker::GetPlaylist(pl.Id);
            if (!existing || existing->SnapshotID != pl.SnapshotID) {
                SetPlaylist(pl);
                auto tIds = Helpers::Split(pl.TrackIDs, ";;");
                for (auto& tid : tIds) {
                    if (!tid.empty()) {
                        Variables::Track t;
                        t.Id = tid;
                        SetTrack(t);
                    }
                }
            }
        }
        std::cout << "Got Playlists\\n";
    }

    static void SyncAlbums() {
        auto albums = SpotifyWorker::GetUserAlbums();
        for (auto& summary : albums) {
            std::string aId = std::get<0>(summary);
            auto data = SpotifyWorker::GetAlbumData(aId);
            if (std::get<0>(data).empty()) continue;

            Variables::Album alb;
            alb.Id = aId;
            alb.Name = std::get<0>(data);
            alb.ImageURL = std::get<1>(data);
            alb.TrackIDs = std::get<3>(data);
            alb.ArtistIDs = std::get<4>(data);

            SetAlbum(alb);

            auto tIds = Helpers::Split(alb.TrackIDs, ";;");
            for (auto& tid : tIds) {
                if (!tid.empty()) {
                    Variables::Track t;
                    t.Id = tid;
                    SetTrack(t);
                }
            }
            
            auto artIds = Helpers::Split(alb.ArtistIDs, ";;");
            for (auto& aid : artIds) {
                if (!aid.empty()) {
                    Variables::Artist a;
                    a.Id = aid;
                    SetArtist(a);
                }
            }
        }
        std::cout << "Got Albums\\n";
    }

    static void SyncLikedSongs() {
        auto liked = SpotifyWorker::GetLikedSongs();
        Variables::PlayList likedPl;
        likedPl.Id = "liked_songs";
        likedPl.Name = "Liked Songs";
        likedPl.SnapshotID = "local";
        
        std::string tIdsStr = "";
        for (auto& song : liked) {
            std::string tid = std::get<0>(song);
            tIdsStr += tid + ";;";
            Variables::Track t;
            t.Id = tid;
            SetTrack(t);
        }
        likedPl.TrackIDs = tIdsStr;
        SetPlaylist(likedPl);
        std::cout << "Got Liked Songs\\n";
    }

    static void SyncTrackMetadata() {
        auto tracks = DatabaseWorker::GetAllTracks();
        std::vector<std::string> idsToFetch;
        for (auto& t : tracks) {
            if (t.MissingInfo()) {
                idsToFetch.push_back(t.Id);
            }
        }

        if (!idsToFetch.empty()) {
            auto results = SpotifyWorker::GetSongDataBatch(idsToFetch);
            for (auto& r : results) {
                Variables::Track t;
                t.Name = std::get<0>(r);
                t.Id = std::get<1>(r);
                t.AlbumId = std::get<2>(r);
                t.ArtistIds = std::get<3>(r);
                t.DiscNumber = std::get<4>(r);
                t.DurationMs = std::get<5>(r);
                t.Explicit = std::get<6>(r);
                t.PreviewUrl = std::get<7>(r);
                t.TrackNumber = std::get<8>(r);

                auto existing = DatabaseWorker::GetTrack(t.Id);
                if (existing) t.SongID = existing->SongID;

                SetTrack(t);

                if (!t.AlbumId.empty()) {
                    Variables::Album a;
                    a.Id = t.AlbumId;
                    SetAlbum(a);
                }

                auto artIds = Helpers::Split(t.ArtistIds, ";;");
                for (auto& aid : artIds) {
                    if (!aid.empty()) {
                        Variables::Artist art;
                        art.Id = aid;
                        SetArtist(art);
                    }
                }
            }
        }
        std::cout << "Got Track Metadata\\n";
    }

    static void SyncArtistMetadata() {
        auto artists = DatabaseWorker::GetAllArtists();
        std::vector<std::string> idsToFetch;
        for (auto& a : artists) {
            if (a.MissingInfo()) {
                idsToFetch.push_back(a.Id);
            }
        }

        if (!idsToFetch.empty()) {
            auto results = SpotifyWorker::GetArtistDataBatch(idsToFetch);
            for (auto& r : results) {
                Variables::Artist art;
                art.Id = std::get<0>(r);
                art.Name = std::get<1>(r);
                art.ImageURL = std::get<2>(r);
                art.Genres = std::get<3>(r);

                SetArtist(art);
            }
        }
        std::cout << "Got Artist Metadata\\n";
    }
"""

if "SyncPlaylists" not in dc:
    dc = dc.replace("    static std::vector<std::pair<std::string, std::string>> GetAllMightBeSimilar() { return DatabaseWorker::GetAllMightBeSimilar(); }", "    static std::vector<std::pair<std::string, std::string>> GetAllMightBeSimilar() { return DatabaseWorker::GetAllMightBeSimilar(); }\n" + sync_methods)
    with open('src/DataCoordinator.hpp', 'w') as f:
        f.write(dc)

