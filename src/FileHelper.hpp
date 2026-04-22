#ifndef FILE_HELPER_HPP
#define FILE_HELPER_HPP

#include <string>
#include <vector>
#include <fstream>
#include <filesystem>
#include <iostream>

namespace SpotifyPlaylistManager {

namespace FileHelper {

    inline void AppendLine(const std::string& path, const std::string& line) {
        std::ofstream outfile;
        outfile.open(path, std::ios_base::app);
        if (outfile.is_open()) {
            outfile << line << std::endl;
        }
    }

    inline std::string ReadSpecificLine(const std::string& path, int lineNumber) {
        std::ifstream infile(path);
        if (!infile.is_open()) return "";

        std::string line;
        int currentLine = 0;
        while (std::getline(infile, line)) {
            currentLine++;
            if (currentLine == lineNumber) {
                return line;
            }
        }
        return "";
    }

    inline bool ModifySpecificLine(const std::string& path, int lineNumber, const std::string& newText) {
        std::ifstream infile(path);
        if (!infile.is_open()) return false;

        std::vector<std::string> lines;
        std::string line;
        while (std::getline(infile, line)) {
            lines.push_back(line);
        }
        infile.close();

        if (lineNumber < 1 || lineNumber > (int)lines.size()) return false;

        lines[lineNumber - 1] = newText;

        std::ofstream outfile(path);
        if (!outfile.is_open()) return false;

        for (const auto& l : lines) {
            outfile << l << std::endl;
        }
        return true;
    }

    inline void CreateOrOverwriteFile(const std::string& path, const std::vector<std::string>& lines) {
        std::ofstream outfile(path);
        if (outfile.is_open()) {
            for (const auto& l : lines) {
                outfile << l << std::endl;
            }
        }
    }

    inline std::vector<std::string> ReadAllLines(const std::string& path) {
        std::vector<std::string> lines;
        std::ifstream infile(path);
        if (!infile.is_open()) return lines;

        std::string line;
        while (std::getline(infile, line)) {
            lines.push_back(line);
        }
        return lines;
    }
}

}

#endif // FILE_HELPER_HPP
