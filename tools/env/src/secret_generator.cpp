#include "secret_generator.hpp"
#include "env_file.hpp"

#include <fstream>
#include <sstream>
#include <iomanip>
#include <random>
#include <algorithm>

namespace nordiska::env {

const std::vector<std::string>& SecretGenerator::standard_secrets() {
    static const std::vector<std::string> secrets = {
        "POSTGRES_BOOTSTRAP_PASSWORD",
        "NORDISKA_MIGRATOR_PASSWORD",
        "NORDISKA_API_PASSWORD",
        "NORDISKA_REPORTING_WORKER_PASSWORD",
        "NORDISKA_JWT_SECRET",
        "NORDISKA_AUDIT_SIGNING_KEY",
        "FAQ_SEARCH_LOG_SALT",
        "PDF_SIGNER_PKCS11_PIN",
        "PDF_SIGNER_PKCS11_SO_PIN",
        "ADMIN_USER_PASSWORD"
    };
    return secrets;
}

std::string SecretGenerator::generate_hex(size_t num_bytes) {
    std::vector<unsigned char> buffer(num_bytes);
    std::ifstream urandom("/dev/urandom", std::ios::binary);
    if (urandom.is_open()) {
        urandom.read(reinterpret_cast<char*>(buffer.data()), num_bytes);
    } else {
        std::random_device rd;
        std::mt19937 gen(rd());
        std::uniform_int_distribution<unsigned short> dist(0, 255);
        for (size_t i = 0; i < num_bytes; ++i) {
            buffer[i] = static_cast<unsigned char>(dist(gen));
        }
    }

    std::ostringstream ss;
    ss << std::hex << std::setfill('0');
    for (unsigned char b : buffer) {
        ss << std::setw(2) << static_cast<int>(b);
    }
    return ss.str();
}

std::string SecretGenerator::generate_pin(size_t digits) {
    std::random_device rd;
    std::mt19937 gen(rd());
    
    // First digit 1-9 so no leading zeros
    std::uniform_int_distribution<int> first_digit_dist(1, 9);
    std::uniform_int_distribution<int> digit_dist(0, 9);

    std::string pin;
    pin += std::to_string(first_digit_dist(gen));
    for (size_t i = 1; i < digits; ++i) {
        pin += std::to_string(digit_dist(gen));
    }
    return pin;
}

std::string SecretGenerator::generate_for(const std::string& var_name) {
    if (var_name == "PDF_SIGNER_PKCS11_PIN") {
        return generate_pin(4);
    }
    if (var_name == "PDF_SIGNER_PKCS11_SO_PIN") {
        return generate_pin(8);
    }
    if (var_name == "POSTGRES_BOOTSTRAP_PASSWORD" ||
        var_name == "NORDISKA_MIGRATOR_PASSWORD" ||
        var_name == "NORDISKA_API_PASSWORD" ||
        var_name == "NORDISKA_REPORTING_WORKER_PASSWORD") {
        return generate_hex(16); // 32 char hex password
    }
    // Default 32 bytes = 64 hex characters (JWT, audit signing key, salt, admin password)
    return generate_hex(32);
}

size_t SecretGenerator::fill_secrets(EnvFile& env, bool force_all) {
    size_t updated = 0;
    for (const auto& sec : standard_secrets()) {
        auto current = env.get(sec);
        bool needs_generation = force_all || !current.has_value() || current->empty() ||
                                current->find("placeholder") != std::string::npos ||
                                current->find("replace-with") != std::string::npos;
        if (needs_generation) {
            std::string new_val = generate_for(sec);
            env.set(sec, new_val);
            updated++;
        }
    }
    return updated;
}

} // namespace nordiska::env
