#include <iostream>
#include <string>

std::string stripQuotes(const std::string& s) {
    if (s.size() >= 2 && ((s.front() == '"' && s.back() == '"') || (s.front() == '\'' && s.back() == '\''))) {
        return s.substr(1, s.size() - 2);
    }
    return s;
}

int main() {
    std::cout << stripQuotes("\"hello\"") << std::endl;
    std::cout << stripQuotes("'world'") << std::endl;
    std::cout << stripQuotes("noquotes") << std::endl;
}
