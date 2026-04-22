#ifndef FUZZY_MATCH_LOGIC_HPP
#define FUZZY_MATCH_LOGIC_HPP

#include "Variables.hpp"
#include "DatabaseWorker.hpp"
#include "Helpers.hpp"
#include <vector>
#include <string>
#include <algorithm>

namespace SpotifyPlaylistManager {

class FuzzyMatchLogic {
public:
    static double ScoreSong(const Variables::Track& t1, const Variables::Track& t2) {
        if (t1.SongID == t2.SongID) return 1.0;
        if (!t1.PreviewUrl.empty() && t1.PreviewUrl == t2.PreviewUrl) return 1.0;

        double score = 0;
        if (t1.AlbumId == t2.AlbumId) score += 0.1;
        
        if (t1.ArtistIds == t2.ArtistIds) {
            score += 0.25;
        } else {
            auto a1 = Helpers::Split(t1.ArtistIds, Variables::Seperator);
            auto a2 = Helpers::Split(t2.ArtistIds, Variables::Seperator);
            for (const auto& artist : a1) {
                if (std::find(a2.begin(), a2.end(), artist) != a2.end()) {
                    score += 0.1;
                }
            }
        }

        if (t1.DiscNumber == t2.DiscNumber) score += 0.05;
        if (std::abs(t1.DurationMs - t2.DurationMs) < 2000) score += 0.05;
        if (t1.Explicit == t2.Explicit) score += 0.05;
        if (t1.TrackNumber == t2.TrackNumber) score += 0.05;

        std::string n1 = Helpers::ToLower(Helpers::ReplaceAll(t1.Name, " ", ""));
        std::string n2 = Helpers::ToLower(Helpers::ReplaceAll(t2.Name, " ", ""));

        if (n1.find(n2) == 0 || n2.find(n1) == 0) {
            score += 0.5;
        } else if (n1.find(n2) != std::string::npos || n2.find(n1) != std::string::npos) {
            score += 0.3;
        }

        return score;
    }

    static void BasicMatch() {
        auto tracks = DatabaseWorker::GetAllTracks();
        for (size_t i = 0; i < tracks.size(); ++i) {
            for (size_t j = i + 1; j < tracks.size(); ++j) {
                double score = 0;
                if (tracks[i].Name == tracks[j].Name && tracks[i].ArtistIds == tracks[j].ArtistIds) {
                    score = 1.0;
                } else {
                    score = ScoreSong(tracks[i], tracks[j]);
                }

                if (score >= 0.75) {
                    tracks[j].SongID = tracks[i].SongID;
                    DatabaseWorker::SetTrack(tracks[j]);
                } else if (score >= 0.45) {
                    DatabaseWorker::SetMightBeSimilar(tracks[i].SongID, tracks[j].SongID);
                }
            }
        }
    }
};

}

#endif // FUZZY_MATCH_LOGIC_HPP
