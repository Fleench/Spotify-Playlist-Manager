#ifndef LOGGER_HPP
#define LOGGER_HPP

#include <string>
#include <functional>
#include <mutex>
#include <fstream>
#include <chrono>
#include <ctime>
#include <iomanip>
#include <sstream>
#include <iostream>

namespace SpotifyPlaylistManager {

// 1. Data Types
enum class LogType {
    Info,
    Warning,
    Error,
    Success
};

// 2. The Template Book (The Formatter)
class LogFormatter {
public:
    static std::string StandardFormat(const std::string& message, LogType type, const std::string& context_string) {
        if (context_string == "Terminal") {
            std::string color;
            std::string symbol;
            std::string reset = "\033[0m";

            switch (type) {
                case LogType::Info:
                    color = "\033[36m"; // Cyan
                    symbol = "[i]";
                    break;
                case LogType::Warning:
                    color = "\033[33m"; // Yellow
                    symbol = "[!]";
                    break;
                case LogType::Error:
                    color = "\033[31m"; // Red
                    symbol = "[✖]";
                    break;
                case LogType::Success:
                    color = "\033[32m"; // Green
                    symbol = "[✔]";
                    break;
            }

            // Generate a timestamp for the terminal
            auto now = std::chrono::system_clock::now();
            std::time_t now_time = std::chrono::system_clock::to_time_t(now);
            std::stringstream ss;
            ss << std::put_time(std::localtime(&now_time), "%H:%M:%S");

            return color + "[" + ss.str() + "] " + symbol + " " + message + reset;
        } 
        else if (context_string == "SyncView") {
            // Clean version without timestamps or symbols
            return message;
        }

        // Default fallback
        return message;
    }
};

// 3. The Scribe (Logger Class)
class Logger {
private:
    static inline std::mutex logMutex;
    static inline std::function<void(std::string, LogType)> uiCallback = nullptr;
    static inline const std::string logFilePath = "Carillon.log";

public:
    // The "Sending Stone" connection point
    static void SetCallback(std::function<void(std::string, LogType)> callback) {
        std::lock_guard<std::mutex> lock(logMutex);
        uiCallback = callback;
    }

    static void Log(const std::string& message, LogType type = LogType::Info) {
        std::lock_guard<std::mutex> lock(logMutex);

        // A. Always write to file (Carillon.log)
        std::ofstream file(logFilePath, std::ios::app);
        if (file.is_open()) {
            auto now = std::chrono::system_clock::now();
            std::time_t now_time = std::chrono::system_clock::to_time_t(now);
            std::stringstream ss;
            ss << std::put_time(std::localtime(&now_time), "%Y-%m-%d %H:%M:%S");

            std::string typeStr;
            switch (type) {
                case LogType::Info: typeStr = "INFO"; break;
                case LogType::Warning: typeStr = "WARNING"; break;
                case LogType::Error: typeStr = "ERROR"; break;
                case LogType::Success: typeStr = "SUCCESS"; break;
            }

            file << "[" << ss.str() << "] [" << typeStr << "] " << message << std::endl;
        }

        // B. Trigger the "Distributor" callback with raw data
        if (uiCallback) {
            uiCallback(message, type);
        }
    }
};

} // namespace SpotifyPlaylistManager

#endif // LOGGER_HPP
