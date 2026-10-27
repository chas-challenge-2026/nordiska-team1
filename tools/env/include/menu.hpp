#pragma once

#include "env_file.hpp"
#include <string>

namespace nordiska::env {

enum class Color {
    Reset,
    Bold,
    Cyan,
    Green,
    Yellow,
    Red,
    Gray,
    BoldCyan,
    BoldGreen,
    BoldYellow,
    BoldRed
};

class TerminalMenu {
public:
    explicit TerminalMenu(EnvFile& env);

    // Runs the main interactive menu loop
    int run();

    // Color helpers
    static bool colors_enabled();
    static std::string colorize(const std::string& text, Color color);

private:
    EnvFile& env_;
    std::string status_message_;
    Color status_color_{Color::Green};
    bool modified_{false};

    void draw_header();
    void draw_status();
    void show_menu();

    void action_switch_db(const std::string& db_name);
    void action_custom_db();
    void action_list_vars();
    void action_edit_var();
    void action_add_var();
    void action_delete_var();
    void action_generate_secrets();
    void action_spawn_subshell();
    void action_print_exports();
    void action_save();

    std::string read_input(const std::string& prompt);
    void wait_for_enter();
    void clear_screen();
};

} // namespace nordiska::env
