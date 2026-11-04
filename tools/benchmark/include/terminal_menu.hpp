#pragma once

#include <algorithm>
#include <chrono>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <cwchar>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <poll.h>
#include <sstream>
#include <stdexcept>
#include <string>
#include <sys/ioctl.h>
#include <termios.h>
#include <unistd.h>
#include <vector>

namespace nordiska::benchmark::terminal {

struct Cancelled { bool eof = false; };
inline bool interactive() { return isatty(STDIN_FILENO) && isatty(STDOUT_FILENO); }

enum class Tone { Plain, Heading, Selected, Success, Warning, Error, Muted, Value };

inline bool colors_enabled() {
    const auto no_color = std::getenv("NO_COLOR"), term = std::getenv("TERM");
    return interactive() && !(no_color && *no_color) && !(term && std::string(term) == "dumb");
}

inline std::string paint(const std::string& text, Tone tone) {
    if (!colors_enabled() || tone == Tone::Plain || text.empty()) return text;
    const char* code = "";
    switch (tone) {
        case Tone::Heading:  code = "\033[1;36m"; break;
        case Tone::Selected: code = "\033[1;30;46m"; break; // Bold black on cyan
        case Tone::Success:  code = "\033[1;32m"; break;
        case Tone::Warning:  code = "\033[1;33m"; break;
        case Tone::Error:    code = "\033[1;31m"; break;
        case Tone::Muted:    code = "\033[90m"; break;
        case Tone::Value:    code = "\033[1;37m"; break; // Bold white
        case Tone::Plain:    break;
    }
    return std::string(code) + text + "\033[0m";
}

class KeyboardMode {
    termios previous_{};
public:
    KeyboardMode() {
        if (!isatty(STDIN_FILENO)) throw Cancelled{true};
        if (tcgetattr(STDIN_FILENO, &previous_) != 0) throw std::runtime_error("Cannot read terminal settings");
        auto mode = previous_;
        mode.c_lflag &= ~(ICANON | ECHO | ISIG);
        mode.c_cc[VMIN] = 1;
        mode.c_cc[VTIME] = 0;
        if (tcsetattr(STDIN_FILENO, TCSANOW, &mode) != 0) throw std::runtime_error("Cannot set terminal settings");
        std::cout << "\033[?25l" << std::flush; // Hide cursor
    }
    ~KeyboardMode() {
        if (isatty(STDIN_FILENO)) {
            tcsetattr(STDIN_FILENO, TCSANOW, &previous_);
            std::cout << "\033[?25h" << std::flush; // Show cursor
        }
    }
    KeyboardMode(const KeyboardMode&) = delete;
    KeyboardMode& operator=(const KeyboardMode&) = delete;
};

inline char key(int timeout_ms = -1) {
    pollfd input{STDIN_FILENO, POLLIN, 0};
    int ready;
    do { ready = poll(&input, 1, timeout_ms); } while (ready < 0 && errno == EINTR);
    if (ready == 0) return '\0';
    if (ready < 0) throw std::runtime_error("Cannot read terminal input");
    char result;
    if (::read(STDIN_FILENO, &result, 1) != 1 || result == '\x04') throw Cancelled{true};
    if (result == '\x03') throw Cancelled{};
    return result;
}

inline char escape_key() {
    auto prefix = key(80);
    if (prefix == '\0') return '\x1b';
    if (prefix != '[' && prefix != 'O') return '\0';
    for (int i = 0; i < 16; ++i) {
        auto val = key(80);
        if (val == '\0') return '\0';
        if (val >= '@' && val <= '~') return val;
    }
    return '\0';
}

inline std::pair<int, int> dimensions() {
    winsize ws{};
    if (ioctl(STDOUT_FILENO, TIOCGWINSZ, &ws) == 0 && ws.ws_col > 0 && ws.ws_row > 0)
        return {ws.ws_col, ws.ws_row};
    return {80, 24};
}

inline void clear_screen() {
    std::cout << "\033[2J\033[H" << std::flush;
}

inline std::string prompt_line(const std::string& prompt, const std::string& default_val = "") {
    std::cout << "\033[?25h"; // Show cursor
    std::cout << prompt;
    if (!default_val.empty()) std::cout << "[" << default_val << "]: ";
    else std::cout << ": ";
    std::cout << std::flush;

    std::string val;
    std::getline(std::cin, val);
    std::cout << "\033[?25l"; // Hide cursor
    if (val.empty()) return default_val;
    return val;
}

} // namespace nordiska::benchmark::terminal

