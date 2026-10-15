#pragma once

#include <cerrno>
#include <algorithm>
#include <atomic>
#include <chrono>
#include <cstdio>
#include <cstdlib>
#include <cwchar>
#include <fcntl.h>
#include <filesystem>
#include <functional>
#include <iostream>
#include <optional>
#include <poll.h>
#include <regex>
#include <sstream>
#include <stdexcept>
#include <string>
#include <sys/ioctl.h>
#include <termios.h>
#include <thread>
#include <unistd.h>
#include <vector>

namespace nordiska::synthetic::terminal {

struct Cancelled { bool eof = false; };
inline bool interactive() { return isatty(STDIN_FILENO) && isatty(STDOUT_FILENO); }
enum class Tone { Plain, Heading, Selected, Success, Warning, Error, Muted, Disabled };
inline bool colors_enabled() {
    const auto no_color = std::getenv("NO_COLOR"), term = std::getenv("TERM");
    return interactive() && !(no_color && *no_color) && !(term && std::string(term) == "dumb");
}
inline std::string paint(const std::string& text, Tone tone) {
    if (!colors_enabled() || tone == Tone::Plain || text.empty()) return text;
    const char* code = "";
    switch (tone) {
        case Tone::Heading: code = "\033[1;36m"; break;
        case Tone::Selected: code = "\033[1;36;7m"; break;
        case Tone::Success: code = "\033[32m"; break;
        case Tone::Warning: code = "\033[33m"; break;
        case Tone::Error: code = "\033[1;31m"; break;
        case Tone::Muted: code = "\033[2m"; break;
        case Tone::Disabled: code = "\033[90m"; break;
        case Tone::Plain: break;
    }
    return code + text + "\033[0m";
}
inline std::string line(const std::string& prompt, size_t limit = 60);
inline size_t menu(const std::string& title, const std::vector<std::string>& options, size_t selected);

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
    if (::read(STDIN_FILENO, &result, 1) != 1 || result == '\x04') throw Cancelled{true};
    if (result == '\x03') throw Cancelled{};
    return result;
}

// Distinguish a bare Esc from an arrow/function key and consume its whole
// sequence so leftover bytes cannot leak into a prompt or confirmation.
inline char escape_key() {
    auto prefix = key(100);
    if (prefix == '\0') return '\x1b';
    if (prefix != '[' && prefix != 'O') return '\0';
    for (int i = 0; i < 16; ++i) {
        auto value = key(100);
        if (value == '\0') return '\0';
        if (value >= '@' && value <= '~') return value;
    }
    return '\0';
}

inline std::string line(const std::string& prompt, size_t limit) {
    std::cout << prompt << std::flush;
    std::string value;
    if (!interactive()) {
        if (!std::getline(std::cin, value)) throw Cancelled{true};
        if (value == "esc" || value == "q" || value == "\x1b") throw Cancelled{};
        return value;
    }
    KeyboardMode keyboard;
    std::cout << "\033[?25h" << std::flush;
    while (true) {
        auto ch = key();
        if (ch == '\x1b') {
            if (escape_key() == '\x1b') throw Cancelled{};
            continue;
        }
        if (ch == '\r' || ch == '\n') { std::cout << '\n'; return value; }
        if (ch == '\x7f' || ch == '\x08') {
            if (!value.empty()) { value.pop_back(); std::cout << "\b \b" << std::flush; }
        } else if (ch >= ' ' && ch <= '~' && value.size() < limit) {
            value += ch;
            std::cout << ch << std::flush;
        }
    }
}

class Screen {
    bool enabled = interactive();
public:
    Screen() { if (enabled) std::cout << "\033[?1049h" << std::flush; }
    ~Screen() { if (enabled) std::cout << "\033[?25h\033[?1049l" << std::flush; }
};

inline std::pair<int, int> dimensions(int fd = STDOUT_FILENO) {
    winsize size{};
    ioctl(fd, TIOCGWINSZ, &size);
    return {size.ws_col ? std::min(80, int(size.ws_col)) : 80, size.ws_row ? int(size.ws_row) : 24};
}
inline std::string clipped(const std::string& text, int width) {
    if (width <= 0) return "";
    if (width <= 3) return text.substr(0, width);
    return text.size() < size_t(width) ? text : text.substr(0, std::max(0, width - 4)) + "...";
}
inline void clear() { if (interactive()) std::cout << "\033[H\033[2J"; }
inline void separator(int width) {
    std::cout << paint(clipped(" --------------------------------------------------", width), Tone::Muted) << '\n';
}
inline void heading(const std::string& title, int width) {
    std::cout << paint(clipped(" " + title, width), Tone::Heading) << '\n';
    separator(width);
}
inline void footer(const std::string& controls, int width) {
    separator(width);
    std::cout << paint(clipped(" " + controls, width), Tone::Muted) << '\n';
}
inline std::string screen_input(const std::string& title, const std::string& prompt,
                                const std::string& controls, const std::string& notice = "",
                                Tone tone = Tone::Plain) {
    clear();
    const auto width = dimensions().first;
    heading(title, width);
    if (!interactive()) {
        if (!notice.empty()) std::cout << notice << '\n';
        return line(prompt);
    }
    std::cout << '\n';
    footer(controls, width);
    if (!notice.empty()) std::cout << paint(clipped(" " + notice, width), Tone::Error) << '\n';
    std::cout << "\033[3;1H";
    const auto limit = size_t(std::max(1, std::min(60, width - int(prompt.size()) - 2)));
    return line(paint(prompt, tone), limit);
}
struct Option { std::string text; bool gap = false; bool enabled = true; };

inline size_t screen_menu(const std::vector<std::string>& header, const std::vector<Option>& options,
                          size_t selected, const std::string& notice = "", const std::string& escape_action = "Exit") {
    if (options.empty() || std::none_of(options.begin(), options.end(), [](const auto& option) { return option.enabled; }))
        throw std::runtime_error("No selectable menu options");
    selected = std::min(selected, options.size() - 1);
    auto advance = [&](int direction) {
        do { selected = direction > 0 ? (selected + 1) % options.size() : selected == 0 ? options.size() - 1 : selected - 1; }
        while (!options[selected].enabled);
    };
    if (!options[selected].enabled) advance(1);
    if (!interactive()) {
        for (const auto& row : header) std::cout << row << '\n';
        std::vector<std::string> choices;
        std::vector<size_t> indices;
        size_t choice = 0;
        for (size_t i = 0; i < options.size(); ++i) {
            if (options[i].enabled) {
                if (i == selected) choice = choices.size();
                choices.push_back(options[i].text);
                indices.push_back(i);
            } else std::cout << "   " << options[i].text << '\n';
        }
        if (!notice.empty()) std::cout << notice << '\n';
        return indices[menu("Action", choices, choice)];
    }
    KeyboardMode keyboard;
    std::vector<int> rows;
    auto geometry = dimensions();
    auto draw_options = [&]() {
        for (size_t i = 0; i < options.size(); ++i) {
            std::cout << "\033[" << rows[i] << ";1H\033[2K"
                      << paint(clipped(std::string(!options[i].enabled ? " - " : i == selected ? " > " : "   ") + options[i].text, geometry.first),
                               !options[i].enabled ? Tone::Disabled : i == selected ? Tone::Selected : Tone::Plain);
        }
        std::cout << std::flush;
    };
    auto draw = [&]() {
        clear();
        rows.clear();
        int row = 1;
        for (const auto& value : header) {
            const auto text = clipped(value, geometry.first);
            const std::string title = " Synthetic Data Manager [Active DB: ";
            if (text.starts_with(title)) {
                const auto end = text.find_first_of(" ]", title.size());
                const auto active = text.substr(title.size(), end - title.size());
                const auto tone = active == "NORMAL" ? Tone::Success :
                                  active == "SYNTHETIC" ? Tone::Heading : Tone::Warning;
                std::cout << paint(title, Tone::Heading) << paint(active, tone);
                if (end != std::string::npos) std::cout << paint(text.substr(end), Tone::Heading);
            } else {
                std::cout << paint(text, value.starts_with(" ---") ? Tone::Muted : row == 1 ? Tone::Heading : Tone::Plain);
            }
            std::cout << '\n';
            ++row;
        }
        for (const auto& option : options) {
            if (option.gap) { std::cout << '\n'; ++row; }
            rows.push_back(row++);
            std::cout << '\n';
        }
        footer("Up/Down Navigate   Enter Select   Esc " + escape_action, geometry.first);
        if (!notice.empty()) {
            auto tone = notice.starts_with("Error:") ? Tone::Error :
                        notice.starts_with("OK:") ? Tone::Success : Tone::Muted;
            auto remaining = notice;
            const auto width = size_t(std::max(1, geometry.first - 2));
            // The compact menu leaves room for up to three result lines at 80x24.
            const int available = std::max(1, std::min(3, geometry.second - row - 2));
            for (int i = 0; i < available && !remaining.empty(); ++i) {
                if (i + 1 == available) {
                    std::cout << paint(clipped(" " + remaining, geometry.first), tone) << '\n';
                    break;
                }
                auto end = std::min(width, remaining.size());
                if (end < remaining.size()) {
                    const auto space = remaining.rfind(' ', end);
                    if (space != std::string::npos && space > 0) end = space;
                }
                std::cout << paint(" " + remaining.substr(0, end), tone) << '\n';
                remaining.erase(0, end);
                if (remaining.starts_with(' ')) remaining.erase(0, 1);
            }
        }
        draw_options();
    };
    draw();
    while (true) {
        auto ch = key(150);
        auto current = dimensions();
        if (current != geometry) { geometry = current; draw(); }
        if ((ch == '\r' || ch == '\n') && options[selected].enabled) return selected;
        if (ch == '\x1b') {
            auto direction = escape_key();
            if (direction == '\x1b') throw Cancelled{};
            if (direction == 'A') advance(-1);
            else if (direction == 'B') advance(1);
            else continue;
            draw_options();
        }
    }
}

inline void page(const std::string& title, const std::string& content) {
    if (!interactive()) {
        std::cout << title << '\n' << content << '\n';
        try { line("Enter or Esc to return: "); } catch (const Cancelled& cancel) { if (cancel.eof) throw; }
        return;
    }
    const auto plain = std::regex_replace(content, std::regex("\x1b\\[[0-?]*[ -/]*[@-~]"), "");
    auto geometry = dimensions();
    std::vector<std::string> rows;
    auto wrap = [&]() {
        rows.clear();
        std::istringstream input(plain);
        std::string value;
        while (std::getline(input, value)) {
            const auto width = std::max(1, geometry.first - 2);
            std::string printable;
            int columns = 0;
            std::mbstate_t state{};
            for (size_t offset = 0; offset < value.size();) {
                wchar_t character;
                auto length = std::mbrtowc(&character, value.data() + offset, value.size() - offset, &state);
                if (length == size_t(-1) || length == size_t(-2) || length == 0) {
                    state = {};
                    ++offset;
                    continue;
                }
                const auto cells = wcwidth(character);
                if (cells >= 0 && cells <= width) {
                    if (columns + cells > width) { rows.push_back(printable); printable.clear(); columns = 0; }
                    printable.append(value, offset, length);
                    columns += cells;
                }
                offset += length;
            }
            rows.push_back(printable);
        }
    };
    wrap();
    size_t top = 0;
    bool dirty = true;
    KeyboardMode keyboard;
    while (true) {
        auto height = size_t(std::max(1, geometry.second - 5));
        if (dirty) {
            clear();
            heading(title, geometry.first);
            for (size_t i = top; i < rows.size() && i < top + height; ++i) std::cout << ' ' << rows[i] << '\n';
            footer(rows.size() > height ? "Up/Down Scroll   Esc Back" : "Esc Back", geometry.first);
            std::cout << std::flush;
            dirty = false;
        }
        auto ch = key(150);
        if (ch == '\r' || ch == '\n') return;
        auto current = dimensions();
        if (current != geometry) { geometry = current; wrap(); top = 0; dirty = true; }
        if (ch == '\x1b') {
            auto direction = escape_key();
            if (direction == '\x1b') return;
            if (direction == 'A' && top > 0) { --top; dirty = true; }
            if (direction == 'B' && top + height < rows.size()) { ++top; dirty = true; }
        }
    }
}

struct OperationFailure : std::runtime_error {
    std::string output;
    OperationFailure(const std::string& message, std::string log) : std::runtime_error(message), output(std::move(log)) {}
};

// Keep backend/child-process output off the interactive screen while the
// spinner writes through the original FD.
template <typename Operation>
std::string loading(const std::string& label, Operation operation,
                    const std::function<std::vector<std::string>()>& details = {}) {
    clear();
    if (interactive()) {
        const auto width = dimensions().first;
        heading("Synthetic Data Manager", width);
        std::cout << '\n';
        if (details) for (int i = 0; i < 6; ++i) std::cout << '\n';
        footer("Please wait...", width);
    }
    std::cout.flush();
    std::cerr.flush();
    auto pattern = (std::filesystem::temp_directory_path() / "nordiska-tui.XXXXXX").string();
    int log = mkstemp(pattern.data());
    if (log < 0) throw std::runtime_error("Cannot capture operation output");
    unlink(pattern.c_str());
    int display = dup(STDOUT_FILENO), errors = dup(STDERR_FILENO);
    const bool tty = interactive();
    const bool color = colors_enabled();
    std::optional<KeyboardMode> keyboard;
    if (tty) keyboard.emplace();
    if (display < 0 || errors < 0 || dup2(log, STDOUT_FILENO) < 0 || dup2(log, STDERR_FILENO) < 0) {
        if (display >= 0) { dup2(display, STDOUT_FILENO); close(display); }
        if (errors >= 0) { dup2(errors, STDERR_FILENO); close(errors); }
        close(log);
        throw std::runtime_error("Cannot capture operation output");
    }
    std::atomic<bool> finished{false};
    std::jthread spinner([&]() {
        size_t frame = 0;
        do {
            std::string value = tty ? "\033[3;1H\033[2K" + std::string(color ? "\033[36m" : "") +
                clipped(" " + label, dimensions(display).first - 3) + "  " + "|/-\\"[frame++ % 4] + (color ? "\033[0m" : "") : label + "...\n";
            if (tty && details) {
                const auto rows = details();
                for (size_t i = 0; i < 6; ++i)
                    value += "\033[" + std::to_string(i + 4) + ";1H\033[2K" +
                        (i < rows.size() ? clipped(" " + rows[i], dimensions(display).first) : "");
            }
            ::write(display, value.data(), value.size());
            if (!tty) break;
            std::this_thread::sleep_for(std::chrono::milliseconds(100));
        } while (!finished);
    });
    std::string failure;
    try { operation(); } catch (const std::exception& error) { failure = error.what(); }
    finished = true;
    spinner.join();
    std::cout.flush();
    std::cerr.flush();
    dup2(display, STDOUT_FILENO);
    dup2(errors, STDERR_FILENO);
    close(display);
    close(errors);
    lseek(log, 0, SEEK_SET);
    std::string output;
    char buffer[8192];
    ssize_t count;
    while ((count = ::read(log, buffer, sizeof(buffer))) > 0) output.append(buffer, count);
    close(log);
    if (tty) { tcflush(STDIN_FILENO, TCIFLUSH); std::cout << "\r\033[2K"; }
    else std::cout << '\n';
    if (!failure.empty()) throw OperationFailure(failure, std::move(output));
    return output;
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
        auto direction = escape_key();
        if (direction == '\x1b') throw Cancelled{};
        if (direction == 'A') selected = selected == 0 ? options.size() - 1 : selected - 1;
        else if (direction == 'B') selected = (selected + 1) % options.size();
        else continue;
        std::cout << "\033[" << options.size() << "A";
        draw();
    }
}

inline bool confirm(const std::string& title, const std::string& prompt) {
    auto answer = screen_input(title, prompt + " [y/N]: ",
                               "Y/N Choose   Enter Confirm   Esc Cancel", "", Tone::Warning);
    return answer == "y" || answer == "Y";
}

} // namespace nordiska::synthetic::terminal
