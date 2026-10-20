#pragma once

#include <string>
#include <vector>

namespace nordiska::env {

class EnvFile;

class SecretGenerator {
public:
    static const std::vector<std::string>& standard_secrets();

    static std::string generate_hex(size_t num_bytes = 32);
    static std::string generate_pin(size_t digits = 4);
    static std::string generate_for(const std::string& var_name);

    // Checks and fills empty or placeholder secrets in EnvFile.
    // If force_all is true, regenerates all standard secrets.
    // Returns count of secrets generated/updated.
    static size_t fill_secrets(EnvFile& env, bool force_all = false);
};

} // namespace nordiska::env
