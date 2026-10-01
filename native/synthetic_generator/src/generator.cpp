#include "generator.hpp"

#include <array>
#include <cmath>
#include <format>
#include <string_view>
#include <algorithm>

namespace nordiska::synthetic {

static constexpr std::array<std::string_view, 48> FIRST_NAMES = {
    "Anna", "Erik", "Karin", "Johan", "Maria", "Karl", "Sara", "Anders",
    "Emma", "Mikael", "Astrid", "Per", "Elin", "Lars", "Linnea", "Fredrik",
    "Ida", "Gustav", "Maja", "Daniel", "Sofia", "Magnus", "Hanna", "Oskar",
    "Clara", "Niklas", "Ebba", "Alexander", "Agnes", "Stefan", "Ingrid",
    "Henrik", "Alma", "Viktor", "Svea", "Emil", "Signe", "Christian",
    "Alva", "Andreas", "Lovisa", "Jonas", "Frida", "Marcus", "Klara",
    "Filip", "Matilda", "Simon"
};

static constexpr std::array<std::string_view, 31> LAST_NAMES = {
    "Andersson", "Johansson", "Karlsson", "Nilsson", "Eriksson", "Larsson",
    "Olsson", "Persson", "Svensson", "Gustafsson", "Pettersson", "Jonsson",
    "Jansson", "Hansson", "Bengtsson", "Jönsson", "Lindqvist", "Lindgren",
    "Bergström", "Axelsson", "Lundberg", "Forsberg", "Sandberg", "Ekström",
    "Hedlund", "Holm", "Nyström", "Öberg", "Söderberg", "Nordström", "Lundqvist"
};

static constexpr std::array<std::string_view, 13> COMPANIES = {
    "Volvo Group AB", "Ericsson AB", "Spotify AB", "H&M Hennes & Mauritz",
    "Scania AB", "Region Stockholm", "Region Skåne", "Göteborgs Stad",
    "Klarna Bank AB", "IKEA AB", "SEB Group", "Nordea Bank Abp", "SVT AB"
};

static constexpr std::array<std::string_view, 9> MERCHANTS_GROCERY = {
    "ICA Supermarket", "ICA Kvantum", "ICA Maxi", "Coop Konsum",
    "Stora Coop", "Hemköp City", "Willys", "Lidl", "City Gross"
};

static constexpr std::array<std::string_view, 12> MERCHANTS_RETAIL = {
    "Systembolaget", "Apoteket Hjärtat", "Kronans Apotek", "Pressbyrån",
    "7-Eleven", "Espresso House", "Clas Ohlson", "IKEA Barkarby",
    "Elgiganten", "XXL Sport", "Kjell & Company", "Stadium"
};

static constexpr std::array<std::string_view, 8> MERCHANTS_COMMUTE = {
    "SL Spärr Stockholm", "SL Månadskort", "SJ Regionaltåg", "Västtrafik",
    "Skånetrafiken", "Circle K Drivmedel", "OKQ8 Bensin", "Preem Station"
};

static constexpr std::array<std::string_view, 12> BILLS_AND_SERVICES = {
    "Hyra Heimstaden", "Hyra Wallenstam", "Ellevio AB Elnät", "Vattenfall Kund",
    "Telia Sverige", "Tele2 Mobil", "If Skadeförsäkring", "Trygg-Hansa",
    "Klarna Månadsfaktura", "Spotify Prenumeration", "Netflix", "SATS Gymkort"
};

struct AccountTypeConfig {
    std::string_view type;
    std::string_view name;
    double rate;
};

static constexpr std::array<AccountTypeConfig, 5> ACCOUNT_CONFIGS = {{
    {"flex", "Sparkonto Flex", 0.035000},
    {"fix", "Fasträntekonto Fix", 0.041000},
    {"standard", "Standard Sparkonto", 0.025000},
    {"saving", "Högräntekonto Förmån", 0.035000},
    {"premium", "Premium Sparkonto", 0.040000}
}};

static constexpr std::string_view COMMON_PASSWORD_HASH =
    "AQAAAAEAACcQAAAAEPI/M0/zVJYqy70AD5XmxCGusa4f003trDYZVeUzZHaVGSFDDNwRIMlWeLlSS6WCqg==";

static inline uint64_t rotl(const uint64_t x, int k) {
    return (x << k) | (x >> (64 - k));
}

FastRng::FastRng(uint64_t seed) {
    uint64_t z = seed + 0x9E3779B97F4A7C15ULL;
    for (int i = 0; i < 4; ++i) {
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9ULL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBULL;
        state_[i] = z ^ (z >> 31);
        z += 0x9E3779B97F4A7C15ULL;
    }
}

uint64_t FastRng::next_u64() {
    const uint64_t result = rotl(state_[1] * 5, 7) * 9;
    const uint64_t t = state_[1] << 17;
    state_[2] ^= state_[0];
    state_[3] ^= state_[1];
    state_[1] ^= state_[2];
    state_[0] ^= state_[3];
    state_[2] ^= t;
    state_[3] = rotl(state_[3], 45);
    return result;
}

double FastRng::next_double() {
    return (next_u64() >> 11) * (1.0 / 9007199254740992.0);
}

int64_t FastRng::randint(int64_t min_val, int64_t max_val) {
    if (min_val >= max_val) return min_val;
    uint64_t range = static_cast<uint64_t>(max_val - min_val + 1);
    return min_val + static_cast<int64_t>(next_u64() % range);
}

uint64_t stable_seed(uint64_t master_seed, int64_t index) {
    uint64_t h = master_seed ^ (static_cast<uint64_t>(index) * 0x9E3779B97F4A7C15ULL);
    h ^= h >> 33;
    h *= 0xFF51AFD7ED558CCDULL;
    h ^= h >> 33;
    h *= 0xC4CEB9FE1A85EC53ULL;
    h ^= h >> 33;
    return h;
}

int luhn_checksum(std::string_view digits) {
    int total = 0;
    for (size_t i = 0; i < digits.size(); ++i) {
        int d = digits[i] - '0';
        if (i % 2 == 0) {
            d *= 2;
        }
        if (d > 9) {
            d -= 9;
        }
        total += d;
    }
    return (10 - (total % 10)) % 10;
}

std::string generate_personnummer(FastRng& rng) {
    int year = static_cast<int>(rng.randint(1930, 2007));
    int month = static_cast<int>(rng.randint(1, 12));
    int max_days = 28;
    if (month != 2) {
        max_days = (month == 4 || month == 6 || month == 9 || month == 11) ? 30 : 31;
    }
    int day = static_cast<int>(rng.randint(1, max_days));
    int individual = static_cast<int>(rng.randint(100, 999));

    char luhn_buf[10];
    snprintf(luhn_buf, sizeof(luhn_buf), "%02d%02d%02d%03d", year % 100, month, day, individual);
    int check = luhn_checksum(std::string_view(luhn_buf, 9));

    char full_buf[14];
    snprintf(full_buf, sizeof(full_buf), "%04d%02d%02d%03d%d", year, month, day, individual, check);
    return std::string(full_buf, 12);
}

int64_t pareto_sample(FastRng& rng, double x_min, double alpha, double x_max) {
    double u = rng.next_double();
    if (u >= 0.999999) u = 0.999999;
    double val = x_min / std::pow(1.0 - u, 1.0 / alpha);
    double clamped = std::clamp(std::floor(val), x_min, x_max);
    return static_cast<int64_t>(clamped);
}

} // namespace nordiska::synthetic
