#ifndef HELPERS_HPP
#define HELPERS_HPP

#include <string>
#include <vector>
#include <sstream>
#include <algorithm>

namespace SpotifyPlaylistManager {

namespace Helpers {

    inline std::vector<std::string> Split(const std::string& s, const std::string& delimiter) {
        std::vector<std::string> tokens;
        size_t last = 0;
        size_t next = 0;
        while ((next = s.find(delimiter, last)) != std::string::npos) {
            tokens.push_back(s.substr(last, next - last));
            last = next + delimiter.length();
        }
        tokens.push_back(s.substr(last));
        return tokens;
    }

    inline std::string Join(const std::vector<std::string>& elements, const std::string& delimiter) {
        std::stringstream ss;
        for (size_t i = 0; i < elements.size(); ++i) {
            if (i != 0) ss << delimiter;
            ss << elements[i];
        }
        return ss.str();
    }

    inline std::string ToLower(std::string s) {
        std::transform(s.begin(), s.end(), s.begin(), [](unsigned char c){ return std::tolower(c); });
        return s;
    }

    inline std::string ReplaceAll(std::string str, const std::string& from, const std::string& to) {
        size_t start_pos = 0;
        while((start_pos = str.find(from, start_pos)) != std::string::npos) {
            str.replace(start_pos, from.length(), to);
            start_pos += to.length();
        }
        return str;
    }

    inline std::string Trim(const std::string& s) {
        auto start = s.find_first_not_of(" \t\n\r");
        if (start == std::string::npos) return "";
        auto end = s.find_last_not_of(" \t\n\r");
        return s.substr(start, end - start + 1);
    }
}

}

#endif // HELPERS_HPP
