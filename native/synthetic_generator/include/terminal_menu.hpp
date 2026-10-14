#pragma once

#include <cerrno>
#include <iostream>
#include <poll.h>
#include <stdexcept>
#include <string>
#include <termios.h>
#include <unistd.h>
#include <vector>

namespace nordiska::synthetic::terminal {

struct Cancelled {};

inline std::string line(const std::string& prompt) {
    std::cout << prompt << std::flush;
    std::string value;
    if (!std::getline(std::cin, value)) throw Cancelled{};
    return value;
}

// Restore the terminal and cursor on every return, including exceptions and Ctrl+C.
class KeyboardMode {
    termios previous{};
public:
    KeyboardMode() {
        if (tcgetattr(STDIN_FILENO, &previous) != 0) throw std::runtime_error("Cannot read terminal settings");
        auto mode = previous;
        mode.c_lflag &= ~(ICANON | ECHO | ISIG);
        mode.c_cc[VMIN] = 1;
        mode.c_cc[VTIME] = 0;
        if (tcsetattr(STDIN_FILENO, TCSANOW, &mode) != 0) throw std::runtime_error("Cannot set terminal settings");
        std::cout << "\033[?25l" << std::flush;
    }
    ~KeyboardMode() {
        tcsetattr(STDIN_FILENO, TCSANOW, &previous);
        std::cout << "\033[?25h" << std::flush;
    }
    KeyboardMode(const KeyboardMode&) = delete;
    KeyboardMode& operator=(const KeyboardMode&) = delete;
};

inline char key(int timeout = -1) {
    pollfd input{STDIN_FILENO, POLLIN, 0};
    int ready;
    do { ready = poll(&input, 1, timeout); } while (ready < 0 && errno == EINTR);
    if (ready == 0) return '\0';
    if (ready < 0) throw std::runtime_error("Cannot read terminal input");
    char result;
    if (::read(STDIN_FILENO, &result, 1) != 1 || result == '\x03' || result == '\x04') throw Cancelled{};
    return result;
}

inline size_t menu(const std::string& title, const std::vector<std::string>& options, size_t selected = 0) {
    if (!isatty(STDIN_FILENO) || !isatty(STDOUT_FILENO)) {
        std::cout << '\n' << title << '\n';
        for (size_t i = 0; i < options.size(); ++i) std::cout << "  " << i + 1 << ") " << options[i] << '\n';
        while (true) {
            auto value = line("Choose [" + std::to_string(selected + 1) + "]: ");
            if (value.empty()) return selected;
            try {
                size_t end;
                auto choice = std::stoul(value, &end);
                if (end == value.size() && choice > 0 && choice <= options.size()) return choice - 1;
            } catch (const std::exception&) {}
            std::cout << "Enter a listed number.\n";
        }
    }
    std::cout << '\n' << title << " (up/down arrows, Enter; Esc cancels)\n";
    auto draw = [&]() {
        for (size_t i = 0; i < options.size(); ++i)
            std::cout << "\r\033[2K" << (i == selected ? "  > " : "    ") << options[i] << '\n';
        std::cout << std::flush;
    };
    KeyboardMode keyboard;
    draw();
    while (true) {
        auto ch = key();
        if (ch == '\r' || ch == '\n') return selected;
        if (ch != '\x1b') continue;
        auto prefix = key(100);
        if (prefix == '\0') throw Cancelled{};
        if (prefix != '[' && prefix != 'O') continue;
        auto direction = key(100);
        if (direction == 'A') selected = selected == 0 ? options.size() - 1 : selected - 1;
        else if (direction == 'B') selected = (selected + 1) % options.size();
        else continue;
        std::cout << "\033[" << options.size() << "A";
        draw();
    }
}

inline bool confirm(const std::string& prompt) {
    auto answer = line(prompt + " [y/N]: ");
    return answer == "y" || answer == "Y";
}

} // namespace nordiska::synthetic::terminal
