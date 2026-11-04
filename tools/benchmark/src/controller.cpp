#include "controller.hpp"
#include "benchmark.hpp"
#include "database.hpp"
#include "terminal_menu.hpp"

#include <algorithm>
#include <fstream>
#include <iomanip>
#include <iostream>
#include <sstream>
#include <string>
#include <vector>

namespace fs = std::filesystem;

namespace nordiska::benchmark {

namespace {

using namespace terminal;

enum class MenuItemIndex {
    Run = 0,
    TargetDatabase,
    Concurrency,
    SignerConcurrency,
    SigningMode,
    TsaLatency,
    ApplyChanges,
    ViewLatest,
    Exit,
    Count
};

struct Settings {
    std::string database = "nordiska_synthetic";
    std::string concurrency = "32";
    std::string signer_concurrency = "128";
    std::string signing_enabled = "1";
    std::string signing_mode = "timestamp";
    std::string tsa_min = "60";
    std::string tsa_max = "100";

    bool operator==(const Settings& o) const {
        return database == o.database &&
               concurrency == o.concurrency &&
               signer_concurrency == o.signer_concurrency &&
               signing_enabled == o.signing_enabled &&
               signing_mode == o.signing_mode &&
               tsa_min == o.tsa_min &&
               tsa_max == o.tsa_max;
    }
    bool operator!=(const Settings& o) const { return !(*this == o); }
};

struct MenuState {
    Settings active;
    Settings pending;
    int64_t customers = 0;
    int64_t documents = 0;
    bool worker_running = false;
    std::string status_msg;

    bool has_changes() const {
        return active != pending;
    }
};

MenuState load_state(DatabaseClient& db) {
    MenuState s;
    s.active.database = db.get_env_setting("NORDISKA_DATABASE", "nordiska_synthetic");
    s.active.concurrency = db.get_env_setting("REPORTING_WORKER_CONCURRENCY", "32");
    s.active.signer_concurrency = db.get_env_setting("PDF_SIGNER_WORKER_COUNT", "128");
    s.active.signing_enabled = db.get_env_setting("NORDISKA_PDF_ENABLE_SIGNING", "1");
    s.active.signing_mode = db.get_env_setting("PDF_SIGNER_MODE", "timestamp");
    s.active.tsa_min = db.get_env_setting("PDF_SIGNER_TSA_LATENCY_MIN_MS", "60");
    s.active.tsa_max = db.get_env_setting("PDF_SIGNER_TSA_LATENCY_MAX_MS", "100");
    s.pending = s.active;

    try {
        auto counts = db.get_counts();
        s.customers = counts.customers;
        s.documents = counts.documents;
    } catch (...) {}

    auto env = db.get_worker_environment();
    s.worker_running = env.found;
    return s;
}

void apply_settings(DatabaseClient& db, MenuState& s) {
    clear_screen();
    std::cout << paint("[*] Applying configuration and restarting services...", Tone::Heading) << "\n";
    std::cout.flush();

    db.apply_environment_changes("NORDISKA_DATABASE", s.pending.database);
    db.apply_environment_changes("REPORTING_WORKER_CONCURRENCY", s.pending.concurrency);
    db.apply_environment_changes("PDF_SIGNER_WORKER_COUNT", s.pending.signer_concurrency);
    db.apply_environment_changes("NORDISKA_PDF_ENABLE_SIGNING", s.pending.signing_enabled);
    db.apply_environment_changes("PDF_SIGNER_MODE", s.pending.signing_mode);

    if (s.pending.tsa_max.empty() || s.pending.tsa_max == "0") {
        db.apply_environment_changes("PDF_SIGNER_TSA_URL", "http://127.0.0.1:8081/");
        db.apply_environment_changes("PDF_SIGNER_TSA_LATENCY_MIN_MS", "");
        db.apply_environment_changes("PDF_SIGNER_TSA_LATENCY_MAX_MS", "");
    } else {
        db.apply_environment_changes("PDF_SIGNER_TSA_URL", "http://127.0.0.1:8082/");
        db.apply_environment_changes("PDF_SIGNER_TSA_LATENCY_MIN_MS", s.pending.tsa_min);
        db.apply_environment_changes("PDF_SIGNER_TSA_LATENCY_MAX_MS", s.pending.tsa_max);
    }

    db.recreate_worker_service();

    s.active = s.pending;
    try {
        auto counts = db.get_counts();
        s.customers = counts.customers;
        s.documents = counts.documents;
    } catch (...) {}

    auto env = db.get_worker_environment();
    s.worker_running = env.found;
    s.status_msg = "Settings applied & worker restarted successfully.";
}

void draw_dashboard(const MenuState& s, int selected_idx, int width) {
    clear_screen();
    std::string border(std::min(width, 78), '=');
    std::string divider(std::min(width, 78), '-');

    std::cout << paint(border, Tone::Heading) << "\n";
    std::cout << paint("                     NORDISKA BENCHMARK CONTROLLER", Tone::Heading) << "\n";
    std::cout << paint(border, Tone::Heading) << "\n";

    // Status lines (shows active running state)
    std::cout << "  Active Database:   "
              << paint(s.active.database, Tone::Warning)
              << " (" << s.customers << " customers | " << s.documents << " docs generated)\n";

    std::string worker_str = s.worker_running
        ? paint("Active", Tone::Success) + " (" + s.active.concurrency + " workers | " + s.active.signer_concurrency + " signers)"
        : paint("Stopped", Tone::Error);
    std::cout << "  Worker Status:     " << worker_str << "\n";

    std::string sign_str;
    if (s.active.signing_enabled == "1") {
        if (s.active.signing_mode == "timestamp" || s.active.signing_mode == "1") {
            sign_str = paint("Timestamped (RFC 3161)", Tone::Success);
            if (!s.active.tsa_min.empty() && !s.active.tsa_max.empty() && s.active.tsa_max != "0") {
                sign_str += " | Latency: " + paint(s.active.tsa_min + "–" + s.active.tsa_max + " ms", Tone::Warning);
            } else {
                sign_str += " | Latency: " + paint("0 ms", Tone::Muted);
            }
        } else {
            sign_str = paint("Plain CMS Signature", Tone::Heading) + " | No TSA";
        }
    } else {
        sign_str = paint("Disabled / Unsigned", Tone::Warning);
    }
    std::cout << "  PDF Signing:       " << sign_str << "\n";
    std::cout << paint(divider, Tone::Heading) << "\n";

    if (s.has_changes()) {
        std::cout << "  " << paint("[!] Unapplied changes detected (*). Select 'Apply Settings' below.", Tone::Warning) << "\n";
        std::cout << paint(divider, Tone::Heading) << "\n";
    } else if (!s.status_msg.empty()) {
        std::cout << "  " << paint("[*] " + s.status_msg, Tone::Success) << "\n";
        std::cout << paint(divider, Tone::Heading) << "\n";
    }

    // Helper for rendering an option item
    auto render_item = [&](int idx, const std::string& label, const std::string& val_str, bool is_action = false, bool changed = false) {
        bool selected = (idx == selected_idx);
        std::string prefix = selected ? "  > " : "    ";
        std::ostringstream ss;
        if (is_action) {
            ss << prefix << "[ " << label << " ]";
        } else {
            ss << prefix << std::left << std::setw(23) << (label + ":") << " < " << val_str << " >";
            if (changed) {
                ss << " *";
            }
        }
        std::string text = ss.str();
        if (selected) {
            std::cout << paint(text, Tone::Selected) << "\n";
        } else if (changed) {
            std::cout << paint(text, Tone::Warning) << "\n";
        } else {
            std::cout << paint(text, Tone::Plain) << "\n";
        }
    };

    // Items
    render_item(static_cast<int>(MenuItemIndex::Run), "Run Whole-DB Benchmark", "", true);

    bool db_changed = (s.pending.database != s.active.database);
    render_item(static_cast<int>(MenuItemIndex::TargetDatabase), "Target Database", s.pending.database, false, db_changed);

    bool conc_changed = (s.pending.concurrency != s.active.concurrency);
    render_item(static_cast<int>(MenuItemIndex::Concurrency), "Worker Concurrency", s.pending.concurrency + " threads", false, conc_changed);

    bool signer_changed = (s.pending.signer_concurrency != s.active.signer_concurrency);
    render_item(static_cast<int>(MenuItemIndex::SignerConcurrency), "Signer Concurrency", s.pending.signer_concurrency + " threads", false, signer_changed);

    bool sign_changed = (s.pending.signing_enabled != s.active.signing_enabled || s.pending.signing_mode != s.active.signing_mode);
    std::string sign_display;
    if (s.pending.signing_enabled != "1") sign_display = "Disabled (Unsigned)";
    else if (s.pending.signing_mode == "plain") sign_display = "Plain CMS Signature";
    else sign_display = "Timestamped (RFC 3161)";
    render_item(static_cast<int>(MenuItemIndex::SigningMode), "PDF Signing Mode", sign_display, false, sign_changed);

    bool tsa_changed = (s.pending.tsa_min != s.active.tsa_min || s.pending.tsa_max != s.active.tsa_max);
    std::string tsa_display;
    if (s.pending.tsa_max.empty() || s.pending.tsa_max == "0") tsa_display = "0 ms (None)";
    else tsa_display = s.pending.tsa_min + "–" + s.pending.tsa_max + " ms";
    render_item(static_cast<int>(MenuItemIndex::TsaLatency), "TSA Simulated Latency", tsa_display, false, tsa_changed);

    if (s.has_changes()) {
        render_item(static_cast<int>(MenuItemIndex::ApplyChanges), "Apply Settings & Restart Services * (Pending)", "", true, true);
    } else {
        render_item(static_cast<int>(MenuItemIndex::ApplyChanges), "Apply Settings & Restart Services", "", true, false);
    }

    render_item(static_cast<int>(MenuItemIndex::ViewLatest), "View Latest Report", "", true);
    render_item(static_cast<int>(MenuItemIndex::Exit), "Exit", "", true);

    std::cout << paint(divider, Tone::Heading) << "\n";
    std::cout << "  " << paint("▲▼ Navigate   ◀▶ Loop Option   Enter Select/Apply   Esc Exit", Tone::Muted) << "\n";
    std::cout << paint(border, Tone::Heading) << "\n" << std::flush;
}

} // namespace

int run_interactive_controller() {
    DatabaseClient db;
    auto root = db.repo_root();
    MenuState state = load_state(db);

    int selected = 0;
    const int total_items = static_cast<int>(MenuItemIndex::Count);

    const std::vector<std::string> concurrency_presets = {"8", "16", "32", "64", "128", "256"};
    const std::vector<std::pair<std::string, std::string>> tsa_presets = {
        {"0", "0"}, {"2", "20"}, {"60", "100"}, {"200", "500"}
    };

    while (true) {
        auto dims = dimensions();
        draw_dashboard(state, selected, dims.first);
        state.status_msg.clear();

        char ch = '\0';
        try {
            KeyboardMode kb;
            ch = key(150);
            if (ch == '\0') continue;
            if (ch == '\x1b') {
                char dir = escape_key();
                if (dir == '\x1b' || dir == '\0') {
                    clear_screen();
                    return 0; // Esc exits
                }
                if (dir == 'A') { // Up
                    selected = (selected > 0) ? (selected - 1) : (total_items - 1);
                    continue;
                }
                if (dir == 'B') { // Down
                    selected = (selected + 1) % total_items;
                    continue;
                }
                if (dir == 'C' || dir == 'D') { // Right ('C') or Left ('D')
                    int delta = (dir == 'C') ? 1 : -1;
                    if (selected == static_cast<int>(MenuItemIndex::TargetDatabase)) {
                        state.pending.database = (state.pending.database == "nordiska_synthetic") ? "nordiska_v2" : "nordiska_synthetic";
                    } else if (selected == static_cast<int>(MenuItemIndex::Concurrency)) {
                        auto it = std::find(concurrency_presets.begin(), concurrency_presets.end(), state.pending.concurrency);
                        int idx = (it != concurrency_presets.end()) ? static_cast<int>(it - concurrency_presets.begin()) : 2;
                        idx = (idx + delta + static_cast<int>(concurrency_presets.size())) % static_cast<int>(concurrency_presets.size());
                        state.pending.concurrency = concurrency_presets[idx];
                    } else if (selected == static_cast<int>(MenuItemIndex::SignerConcurrency)) {
                        auto it = std::find(concurrency_presets.begin(), concurrency_presets.end(), state.pending.signer_concurrency);
                        int idx = (it != concurrency_presets.end()) ? static_cast<int>(it - concurrency_presets.begin()) : 4;
                        idx = (idx + delta + static_cast<int>(concurrency_presets.size())) % static_cast<int>(concurrency_presets.size());
                        state.pending.signer_concurrency = concurrency_presets[idx];
                    } else if (selected == static_cast<int>(MenuItemIndex::SigningMode)) {
                        int mode_idx = 0;
                        if (state.pending.signing_enabled != "1") mode_idx = 0;
                        else if (state.pending.signing_mode == "plain") mode_idx = 1;
                        else mode_idx = 2;

                        mode_idx = (mode_idx + delta + 3) % 3;
                        if (mode_idx == 0) {
                            state.pending.signing_enabled = "0";
                        } else if (mode_idx == 1) {
                            state.pending.signing_enabled = "1";
                            state.pending.signing_mode = "plain";
                        } else {
                            state.pending.signing_enabled = "1";
                            state.pending.signing_mode = "timestamp";
                        }
                    } else if (selected == static_cast<int>(MenuItemIndex::TsaLatency)) {
                        int cur_idx = 0;
                        for (size_t i = 0; i < tsa_presets.size(); ++i) {
                            if (tsa_presets[i].first == state.pending.tsa_min && tsa_presets[i].second == state.pending.tsa_max) {
                                cur_idx = static_cast<int>(i);
                                break;
                            }
                        }
                        cur_idx = (cur_idx + delta + static_cast<int>(tsa_presets.size())) % static_cast<int>(tsa_presets.size());
                        state.pending.tsa_min = tsa_presets[cur_idx].first;
                        state.pending.tsa_max = tsa_presets[cur_idx].second;
                    }
                    continue;
                }
            }
        } catch (const Cancelled&) {
            clear_screen();
            return 0;
        }

        if (ch == 'q' || ch == 'Q') {
            clear_screen();
            return 0;
        }

        if (ch == '\r' || ch == '\n') {
            if (selected == static_cast<int>(MenuItemIndex::Run)) {
                if (state.has_changes()) {
                    apply_settings(db, state);
                }
                clear_screen();
                std::cout << "Target Database: " << state.active.database << "\n";
                std::string input = prompt_line("Customer limit (press Enter for ALL, or number e.g. 100)");
                int64_t limit = 0;
                if (!input.empty()) {
                    try { limit = std::stoll(input); } catch (...) {}
                }
                BenchmarkOptions opt;
                opt.database = state.active.database;
                opt.limit = limit;
                try {
                    BenchmarkRunner runner(opt);
                    runner.run();
                } catch (const std::exception& e) {
                    std::cerr << "Benchmark failed: " << e.what() << "\n";
                }
                std::cout << "\nPress Enter or Esc to return to menu...";
                std::cout.flush();
                KeyboardMode wait_kb;
                while (true) {
                    char k = key();
                    if (k == '\r' || k == '\n' || k == '\x1b' || k == 'q' || k == 'Q') break;
                }
                state = load_state(db);
                continue;
            }

            if (selected == static_cast<int>(MenuItemIndex::TargetDatabase)) {
                state.pending.database = (state.pending.database == "nordiska_synthetic") ? "nordiska_v2" : "nordiska_synthetic";
                continue;
            }

            if (selected == static_cast<int>(MenuItemIndex::Concurrency)) {
                clear_screen();
                std::string input = prompt_line("Enter worker concurrency slots (1–512)", state.pending.concurrency);
                if (!input.empty()) {
                    try {
                        int val = std::stoi(input);
                        if (val > 0 && val <= 512) {
                            state.pending.concurrency = std::to_string(val);
                        }
                    } catch (...) {}
                }
                continue;
            }

            if (selected == static_cast<int>(MenuItemIndex::SignerConcurrency)) {
                clear_screen();
                std::string input = prompt_line("Enter signer worker threads (1–512)", state.pending.signer_concurrency);
                if (!input.empty()) {
                    try {
                        int val = std::stoi(input);
                        if (val > 0 && val <= 512) {
                            state.pending.signer_concurrency = std::to_string(val);
                        }
                    } catch (...) {}
                }
                continue;
            }

            if (selected == static_cast<int>(MenuItemIndex::SigningMode)) {
                int mode_idx = 0;
                if (state.pending.signing_enabled != "1") mode_idx = 0;
                else if (state.pending.signing_mode == "plain") mode_idx = 1;
                else mode_idx = 2;

                mode_idx = (mode_idx + 1) % 3;
                if (mode_idx == 0) {
                    state.pending.signing_enabled = "0";
                } else if (mode_idx == 1) {
                    state.pending.signing_enabled = "1";
                    state.pending.signing_mode = "plain";
                } else {
                    state.pending.signing_enabled = "1";
                    state.pending.signing_mode = "timestamp";
                }
                continue;
            }

            if (selected == static_cast<int>(MenuItemIndex::TsaLatency)) {
                clear_screen();
                std::string in_min = prompt_line("Enter minimum latency ms (0–1000)", state.pending.tsa_min.empty() ? "0" : state.pending.tsa_min);
                std::string in_max = prompt_line("Enter maximum latency ms (0–1000)", state.pending.tsa_max.empty() ? "0" : state.pending.tsa_max);
                try {
                    int mn = std::clamp(std::stoi(in_min), 0, 1000);
                    int mx = std::clamp(std::stoi(in_max), mn, 1000);
                    if (mx == 0) {
                        state.pending.tsa_min = "0";
                        state.pending.tsa_max = "0";
                    } else {
                        state.pending.tsa_min = std::to_string(mn);
                        state.pending.tsa_max = std::to_string(mx);
                    }
                } catch (...) {}
                continue;
            }

            if (selected == static_cast<int>(MenuItemIndex::ApplyChanges)) {
                if (state.has_changes()) {
                    apply_settings(db, state);
                } else {
                    state.status_msg = "No unapplied changes to apply.";
                }
                continue;
            }

            if (selected == static_cast<int>(MenuItemIndex::ViewLatest)) {
                clear_screen();
                fs::path latest_file = root / "tools/benchmark/runs/latest.txt";
                if (fs::exists(latest_file)) {
                    std::ifstream f(latest_file);
                    std::string line;
                    while (std::getline(f, line)) {
                        std::cout << line << "\n";
                    }
                } else {
                    std::cout << "No previous benchmark report found in tools/benchmark/runs/.\n";
                }
                std::cout << "\nPress Enter or Esc to return to menu...";
                std::cout.flush();
                KeyboardMode wait_kb;
                while (true) {
                    char k = key();
                    if (k == '\r' || k == '\n' || k == '\x1b' || k == 'q' || k == 'Q') break;
                }
                continue;
            }

            if (selected == static_cast<int>(MenuItemIndex::Exit)) {
                clear_screen();
                return 0;
            }
        }
    }
}

} // namespace nordiska::benchmark
