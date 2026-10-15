#include "generator.hpp"

#include <algorithm>
#include <chrono>
#include <iostream>
#include <limits>
#include <stdexcept>

using namespace nordiska::synthetic;

static void require(bool condition, const char* message) {
    if (!condition) throw std::runtime_error(message);
}

static uint64_t position_of(std::string_view value) {
    auto number = [&](size_t offset, size_t count) {
        unsigned result = 0;
        for (size_t i = offset; i < offset + count; ++i) result = result * 10 + value[i] - '0';
        return result;
    };
    require(value.size() == 12 && std::all_of(value.begin(), value.end(), [](char c) {
        return c >= '0' && c <= '9';
    }), "Expected twelve digits");
    const auto date = std::chrono::year{int(number(0, 4))}/number(4, 2)/number(6, 2);
    require(date.ok(), "Invalid birth date");
    const auto days = std::chrono::sys_days{date};
    require(days >= personal_number_first_date && days < personal_number_end_date, "Birth date outside fixture range");
    require(luhn_checksum(value.substr(2, 9)) == int(number(11, 1)), "Invalid Luhn checksum");
    return (days - personal_number_first_date).count() * 1000 + number(8, 3);
}

int main() {
    try {
        require(personal_number_capacity == 37'620'000, "Wrong capacity");
        require(generate_personnummer(0, 0).substr(0, 11) == "19050101000", "Incorrect first identity");
        require(generate_personnummer(0, personal_number_capacity - 1).substr(0, 11) == "20071231999", "Incorrect last identity");

        // Validate every identity and its inverse position. A full traversal of
        // all positions proves uniqueness without a test-side hash set.
        for (uint64_t ordinal = 0; ordinal < personal_number_capacity; ++ordinal) {
            require(position_of(generate_personnummer(42, ordinal)) ==
                (42 + ordinal) % personal_number_capacity, "Duplicate, missing, or incorrectly ordered identity");
        }
        const auto leap_day = std::chrono::sys_days{std::chrono::year{2000}/2/29};
        const auto leap_position = (leap_day - personal_number_first_date).count() * 1000;
        require(generate_personnummer(0, leap_position).substr(0, 11) == "20000229000", "Missing leap day");
        for (auto seed : {uint64_t{0}, personal_number_capacity - 1, std::numeric_limits<uint64_t>::max()}) {
            for (uint64_t ordinal = 0; ordinal < 1001; ++ordinal)
                require(position_of(generate_personnummer(seed, ordinal)) ==
                    (seed % personal_number_capacity + ordinal) % personal_number_capacity, "Seed overflow or wrap error");
        }
        bool exhausted = false;
        try { generate_personnummer(0, personal_number_capacity); }
        catch (const std::out_of_range&) { exhausted = true; }
        require(exhausted, "Capacity must prevent repeated identities");

        // Append compatibility: skip overlapping identities once, regardless of
        // the seed used to generate the previous fixture. Ignore other formats.
        PersonalNumberSequence sequence(personal_number_capacity - 2);
        sequence.reserve(generate_personnummer(0, personal_number_capacity - 1));
        sequence.reserve(generate_personnummer(0, 0));
        sequence.reserve(generate_personnummer(0, 0));
        sequence.reserve("not-an-identity");
        sequence.reserve("190502300000");
        sequence.reserve("190412319999");
        require(sequence.remaining() == personal_number_capacity - 2, "Incorrect reservation count");
        require(position_of(sequence.next()) == personal_number_capacity - 2, "Unexpected append start");
        require(position_of(sequence.next()) == 1, "Append did not skip reserved positions across wrap");
        require(position_of(sequence.next()) == 2, "Append repeated a generated position");
        std::cout << "PASS: all 37,620,000 identities are unique, valid dates and Luhn; seeds, wrap, exhaustion and append reservations\n";
    } catch (const std::exception& error) {
        std::cerr << error.what() << '\n';
        return 1;
    }
}
