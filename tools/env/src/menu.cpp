#include "menu.hpp"
#include "secret_generator.hpp"

#include <iostream>
#include <iomanip>
#include <sstream>
#include <cstdlib>
#include <unistd.h>

namespace nordiska::env {

TerminalMenu::TerminalMenu(EnvFile& env) : env_(env) {}

bool TerminalMenu::colors_enabled() {
    const char* no_color = std::getenv("NO_COLOR");
    if (no_color && *no_color) return false;
    const char* term = std::getenv("TERM");
    if (term && std::string(term) == "dumb") return false;
    return isatty(STDOUT_FILENO) && isatty(STDIN_FILENO);
}

std::string TerminalMenu::colorize(const std::string& text, Color color) {
    if (!colors_enabled() || text.empty()) return text;
    const char* code = "";
    switch (color) {
        case Color::Bold:       code = "\033[1m"; break;
        case Color::Cyan:       code = "\033[36m"; break;
        case Color::Green:      code = "\033[32m"; break;
        case Color::Yellow:     code = "\033[33m"; break;
        case Color::Red:        code = "\033[31m"; break;
        case Color::Gray:       code = "\033[90m"; break;
        case Color::BoldCyan:   code = "\033[1;36m"; break;
        case Color::BoldGreen:  code = "\033[1;32m"; break;
        case Color::BoldYellow: code = "\033[1;33m"; break;
        case Color::BoldRed:    code = "\033[1;31m"; break;
        case Color::Reset:      code = "\033[0m"; break;
    }
    return std::string(code) + text + "\033[0m";
}

void TerminalMenu::clear_screen() {
    if (colors_enabled()) {
        std::cout << "\033[H\033[2J" << std::flush;
    } else {
        std::cout << "\n------------------------------------------------------------\n";
    }
}

std::string TerminalMenu::read_input(const std::string& prompt) {
    std::cout << prompt << std::flush;
    std::string line;
    if (!std::getline(std::cin, line)) {
        return "Q"; // Exit on EOF
    }
    // Trim
    auto start = line.find_first_not_of(" \t\r\n");
    if (start == std::string::npos) return "";
    auto end = line.find_last_not_of(" \t\r\n");
    return line.substr(start, end - start + 1);
}

void TerminalMenu::wait_for_enter() {
    std::cout << "\n" << colorize("Press Enter to continue...", Color::Gray);
    std::string dummy;
    std::getline(std::cin, dummy);
}

void TerminalMenu::draw_header() {
    clear_screen();
    std::string border(68, '=');
    std::string line(68, '-');

    std::cout << colorize(border, Color::Cyan) << "\n";
    std::cout << colorize("              NORDISKA ENVIRONMENT MANAGER", Color::BoldCyan) << "\n";
    std::cout << colorize(border, Color::Cyan) << "\n";

    std::string cur_db = env_.current_database();
    std::string shell_db = env_.shell_database();

    std::cout << colorize(" Config File: ", Color::Bold) << env_.env_path().string() << "\n";
    std::cout << colorize(" Target DB (.env):  ", Color::Bold);
    if (cur_db == "nordiska_v2") {
        std::cout << colorize("nordiska_v2 (Normal DB)", Color::BoldGreen);
    } else if (cur_db == "nordiska_synthetic") {
        std::cout << colorize("nordiska_synthetic (Synthetic DB)", Color::BoldYellow);
    } else {
        std::cout << colorize(cur_db, Color::Cyan);
    }
    std::cout << "\n";

    std::cout << colorize(" Shell DB ($ENV):   ", Color::Bold);
    if (shell_db.empty()) {
        std::cout << colorize("(not set in current shell)", Color::Gray);
    } else if (shell_db == "nordiska_v2") {
        std::cout << colorize("nordiska_v2", Color::BoldGreen);
    } else if (shell_db == "nordiska_synthetic") {
        std::cout << colorize("nordiska_synthetic", Color::BoldYellow);
    } else {
        std::cout << colorize(shell_db, Color::Cyan);
    }
    std::cout << "\n";

    auto vol = env_.get("NORDISKA_REPORT_DOCUMENTS_VOLUME");
    if (vol.has_value() && !vol->empty()) {
        std::cout << colorize(" Document Storage:  ", Color::Bold) << *vol << "\n";
    }

    std::cout << colorize(line, Color::Gray) << "\n";
}

void TerminalMenu::draw_status() {
    if (!status_message_.empty()) {
        std::cout << " " << colorize(status_message_, status_color_) << "\n";
        std::string line(68, '-');
        std::cout << colorize(line, Color::Gray) << "\n";
        status_message_.clear();
    }
}

void TerminalMenu::show_menu() {
    draw_header();
    draw_status();

    std::cout << colorize(" Database Presets:", Color::BoldCyan) << "\n";
    std::cout << "   " << colorize("[1]", Color::Bold) << " Switch to NORMAL database      " << colorize("(nordiska_v2)", Color::Green) << "\n";
    std::cout << "   " << colorize("[2]", Color::Bold) << " Switch to SYNTHETIC database   " << colorize("(nordiska_synthetic)", Color::Yellow) << "\n";
    std::cout << "   " << colorize("[3]", Color::Bold) << " Enter custom database name\n";
    std::cout << "\n";

    std::cout << colorize(" Variables & Secrets:", Color::BoldCyan) << "\n";
    std::cout << "   " << colorize("[4]", Color::Bold) << " View all environment variables\n";
    std::cout << "   " << colorize("[5]", Color::Bold) << " Edit a variable value\n";
    std::cout << "   " << colorize("[6]", Color::Bold) << " Add a new variable\n";
    std::cout << "   " << colorize("[7]", Color::Bold) << " Delete a variable\n";
    std::cout << "   " << colorize("[8]", Color::Bold) << " Generate missing secrets (random keys)\n";
    std::cout << "\n";

    std::cout << colorize(" Shell Integration:", Color::BoldCyan) << "\n";
    std::cout << "   " << colorize("[9]", Color::Bold) << " Launch subshell with variables exported\n";
    std::cout << "   " << colorize("[10]", Color::Bold) << " Print shell export commands\n";
    std::cout << "\n";

    std::cout << colorize(" Actions:", Color::BoldCyan) << "\n";
    std::cout << "   " << colorize("[S]", Color::Bold) << " Force re-save to infra/.env\n";
    std::cout << "   " << colorize("[Q]", Color::Bold) << " Quit\n";

    std::string line(68, '-');
    std::cout << colorize(line, Color::Gray) << "\n";
}

void TerminalMenu::action_switch_db(const std::string& db_name) {
    env_.set_database(db_name);
    env_.save();
    status_message_ = "[SUCCESS] Database set to '" + db_name + "' and saved to " + env_.env_path().filename().string();
    status_color_ = (db_name == "nordiska_v2") ? Color::BoldGreen : Color::BoldYellow;
}

void TerminalMenu::action_custom_db() {
    clear_screen();
    std::cout << colorize("=== Custom Database Name ===", Color::BoldCyan) << "\n\n";
    std::cout << "Current database: " << env_.current_database() << "\n\n";
    std::string input = read_input("Enter database name (or empty to cancel): ");
    if (input.empty()) {
        status_message_ = "Cancelled.";
        status_color_ = Color::Gray;
        return;
    }
    action_switch_db(input);
}

void TerminalMenu::action_list_vars() {
    clear_screen();
    std::cout << colorize("=== Environment Variables in " + env_.env_path().filename().string() + " ===", Color::BoldCyan) << "\n\n";

    auto vars = env_.all_vars();
    if (vars.empty()) {
        std::cout << colorize("No variables defined.\n", Color::Yellow);
    } else {
        std::cout << " " << std::left << std::setw(4) << "#"
                  << std::setw(36) << "VARIABLE"
                  << "VALUE\n";
        std::cout << " " << std::string(66, '-') << "\n";

        for (size_t i = 0; i < vars.size(); ++i) {
            const auto& [k, v] = vars[i];
            std::string display_val = v;

            // Partially mask long secrets if desired or display cleanly
            if (display_val.size() > 30 && (k.find("SECRET") != std::string::npos || k.find("KEY") != std::string::npos || k.find("PASSWORD") != std::string::npos)) {
                display_val = display_val.substr(0, 12) + "..." + display_val.substr(display_val.size() - 8);
            }

            std::cout << " " << std::left << std::setw(4) << (std::to_string(i + 1) + ".")
                      << colorize(k, Color::Bold) << std::string(std::max(1, 36 - int(k.size())), ' ')
                      << colorize(display_val, Color::Cyan) << "\n";
        }
    }
    wait_for_enter();
}

void TerminalMenu::action_edit_var() {
    clear_screen();
    std::cout << colorize("=== Edit Variable Value ===", Color::BoldCyan) << "\n\n";

    auto vars = env_.all_vars();
    for (size_t i = 0; i < vars.size(); ++i) {
        std::cout << " [" << std::setw(2) << (i + 1) << "] "
                  << colorize(vars[i].first, Color::Bold)
                  << " = " << colorize(vars[i].second, Color::Cyan) << "\n";
    }
    std::cout << "\n";

    std::string choice = read_input("Select variable number or name to edit (or 'c' to cancel): ");
    if (choice.empty() || choice == "c" || choice == "C") {
        status_message_ = "Cancelled.";
        status_color_ = Color::Gray;
        return;
    }

    std::string key_to_edit;
    try {
        size_t idx = std::stoul(choice);
        if (idx >= 1 && idx <= vars.size()) {
            key_to_edit = vars[idx - 1].first;
        }
    } catch (...) {
        key_to_edit = choice;
    }

    auto current_val = env_.get(key_to_edit);
    if (!current_val.has_value()) {
        status_message_ = "[ERROR] Variable '" + key_to_edit + "' not found.";
        status_color_ = Color::BoldRed;
        return;
    }

    std::cout << "\nEditing: " << colorize(key_to_edit, Color::Bold) << "\n";
    std::cout << "Current value: " << colorize(*current_val, Color::Cyan) << "\n";
    std::string new_val = read_input("Enter new value (leave empty to keep current): ");
    if (new_val.empty()) {
        status_message_ = "No change made to " + key_to_edit;
        status_color_ = Color::Gray;
        return;
    }

    if (key_to_edit == "NORDISKA_DATABASE") {
        env_.set_database(new_val);
    } else {
        env_.set(key_to_edit, new_val);
    }
    env_.save();

    status_message_ = "[SUCCESS] Updated " + key_to_edit + " and saved to " + env_.env_path().filename().string();
    status_color_ = Color::BoldGreen;
}

void TerminalMenu::action_add_var() {
    clear_screen();
    std::cout << colorize("=== Add New Variable ===", Color::BoldCyan) << "\n\n";

    std::string key = read_input("Enter variable name (e.g. MY_CUSTOM_VAR): ");
    if (key.empty()) {
        status_message_ = "Cancelled.";
        status_color_ = Color::Gray;
        return;
    }

    std::string val = read_input("Enter value for " + key + ": ");
    env_.set(key, val);
    env_.save();

    status_message_ = "[SUCCESS] Added " + key + " and saved to " + env_.env_path().filename().string();
    status_color_ = Color::BoldGreen;
}

void TerminalMenu::action_delete_var() {
    clear_screen();
    std::cout << colorize("=== Delete Variable ===", Color::BoldCyan) << "\n\n";

    auto vars = env_.all_vars();
    for (size_t i = 0; i < vars.size(); ++i) {
        std::cout << " [" << std::setw(2) << (i + 1) << "] "
                  << colorize(vars[i].first, Color::Bold) << "\n";
    }
    std::cout << "\n";

    std::string choice = read_input("Select variable number or name to delete (or 'c' to cancel): ");
    if (choice.empty() || choice == "c" || choice == "C") {
        status_message_ = "Cancelled.";
        status_color_ = Color::Gray;
        return;
    }

    std::string key_to_del;
    try {
        size_t idx = std::stoul(choice);
        if (idx >= 1 && idx <= vars.size()) {
            key_to_del = vars[idx - 1].first;
        }
    } catch (...) {
        key_to_del = choice;
    }

    if (!env_.has(key_to_del)) {
        status_message_ = "[ERROR] Variable '" + key_to_del + "' not found.";
        status_color_ = Color::BoldRed;
        return;
    }

    std::string confirm = read_input("Are you sure you want to delete '" + key_to_del + "'? [y/N]: ");
    if (confirm == "y" || confirm == "Y") {
        env_.remove(key_to_del);
        env_.save();
        status_message_ = "[SUCCESS] Removed '" + key_to_del + "' and saved to " + env_.env_path().filename().string();
        status_color_ = Color::BoldGreen;
    } else {
        status_message_ = "Cancelled.";
        status_color_ = Color::Gray;
    }
}

void TerminalMenu::action_generate_secrets() {
    clear_screen();
    std::cout << colorize("=== Generate Random Secrets ===", Color::BoldCyan) << "\n\n";
    std::cout << "Options:\n";
    std::cout << " [1] Only fill empty or placeholder secrets (Safe)\n";
    std::cout << " [2] Force regenerate ALL application secrets (Resets passwords/keys)\n";
    std::cout << " [C] Cancel\n\n";

    std::string choice = read_input("Select option [1/2/C]: ");
    if (choice == "1") {
        size_t n = SecretGenerator::fill_secrets(env_, false);
        env_.save();
        status_message_ = "[SUCCESS] Generated " + std::to_string(n) + " missing secret(s).";
        status_color_ = Color::BoldGreen;
    } else if (choice == "2") {
        std::string confirm = read_input("Warning: Existing secrets will be overwritten. Proceed? [y/N]: ");
        if (confirm == "y" || confirm == "Y") {
            size_t n = SecretGenerator::fill_secrets(env_, true);
            env_.save();
            status_message_ = "[SUCCESS] Regenerated " + std::to_string(n) + " secret(s).";
            status_color_ = Color::BoldGreen;
        } else {
            status_message_ = "Cancelled.";
            status_color_ = Color::Gray;
        }
    } else {
        status_message_ = "Cancelled.";
        status_color_ = Color::Gray;
    }
}

void TerminalMenu::action_spawn_subshell() {
    clear_screen();
    std::cout << colorize("=== Spawning Subshell with Exported Environment ===", Color::BoldCyan) << "\n\n";
    std::cout << "All variables from " << env_.env_path().filename().string() << " are loaded into this shell.\n";
    std::cout << "Type 'exit' when done to return to this menu.\n\n";

    // Set all env vars in current process environment before calling subshell
    for (const auto& [k, v] : env_.all_vars()) {
        setenv(k.c_str(), v.c_str(), 1);
    }

    // Set custom prompt prefix
    setenv("PS1", "(\\033[1;36mnordiska-env\\033[0m) \\u@\\h:\\w\\$ ", 1);

    int res = std::system("bash --noprofile --norc");
    (void)res;

    status_message_ = "Returned from subshell.";
    status_color_ = Color::Gray;
}

void TerminalMenu::action_print_exports() {
    clear_screen();
    std::cout << colorize("=== Shell Export Commands ===", Color::BoldCyan) << "\n\n";
    std::cout << colorize("# Copy and paste or use: eval $(tools/env/nordiska-env --export)", Color::Gray) << "\n\n";

    std::cout << env_.generate_export_commands() << "\n";
    std::cout << colorize("Also written to: " + env_.export_path().string(), Color::Cyan) << "\n";

    wait_for_enter();
}

void TerminalMenu::action_save() {
    if (env_.save()) {
        status_message_ = "[SUCCESS] Saved to " + env_.env_path().string() + " and written to " + env_.export_path().string();
        status_color_ = Color::BoldGreen;
    } else {
        status_message_ = "[ERROR] Failed to save to " + env_.env_path().string();
        status_color_ = Color::BoldRed;
    }
}

int TerminalMenu::run() {
    while (true) {
        show_menu();
        std::string choice = read_input("Select an option [1-10, S, Q]: ");
        if (choice.empty()) continue;

        if (choice == "1") {
            action_switch_db("nordiska_v2");
        } else if (choice == "2") {
            action_switch_db("nordiska_synthetic");
        } else if (choice == "3") {
            action_custom_db();
        } else if (choice == "4") {
            action_list_vars();
        } else if (choice == "5") {
            action_edit_var();
        } else if (choice == "6") {
            action_add_var();
        } else if (choice == "7") {
            action_delete_var();
        } else if (choice == "8") {
            action_generate_secrets();
        } else if (choice == "9") {
            action_spawn_subshell();
        } else if (choice == "10") {
            action_print_exports();
        } else if (choice == "s" || choice == "S") {
            action_save();
        } else if (choice == "q" || choice == "Q" || choice == "exit" || choice == "\x1b") {
            clear_screen();
            std::cout << colorize("Exiting Nordiska Environment Manager.\n", Color::BoldCyan);
            break;
        } else {
            status_message_ = "Invalid choice: " + choice;
            status_color_ = Color::BoldRed;
        }
    }
    return 0;
}

} // namespace nordiska::env
