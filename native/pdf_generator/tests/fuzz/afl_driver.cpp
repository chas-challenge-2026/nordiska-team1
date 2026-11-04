#include "json_to_pdf.hpp"

#include <cstdio>
#include <filesystem>
#include <fstream>
#include <string_view>
#include <vector>

#ifdef __AFL_HAVE_MANUAL_CONTROL
#include <unistd.h>

__AFL_FUZZ_INIT();
#endif

namespace {

int replay(const char* path, bool require_success) {
    std::error_code error;
    const auto size = std::filesystem::file_size(path, error);
    if (error || size > nordiska::fuzz::kMaxInputBytes) {
        std::fprintf(stderr, "Cannot replay %s: unreadable or exceeds %zu bytes\n", path,
                     nordiska::fuzz::kMaxInputBytes);
        return 2;
    }
    std::ifstream stream(path, std::ios::binary);
    std::vector<std::uint8_t> bytes(static_cast<std::size_t>(size));
    if (!stream.read(reinterpret_cast<char*>(bytes.data()), static_cast<std::streamsize>(size))) {
        std::fprintf(stderr, "Cannot read %s\n", path);
        return 2;
    }
    const bool generated = nordiska::fuzz::json_to_pdf(bytes);
    std::printf("%s: %s\n", generated ? "generated" : "rejected", path);
    return require_success && !generated ? 1 : 0;
}

} // namespace

int main(int argc, char** argv) {
    if (argc == 2 && std::string_view(argv[1]) == "--max-input-bytes") {
        std::printf("%zu\n", nordiska::fuzz::kMaxInputBytes);
        return 0;
    }
    if (argc >= 3 && (std::string_view(argv[1]) == "--replay" || std::string_view(argv[1]) == "--require-success")) {
        const bool require_success = std::string_view(argv[1]) == "--require-success";
        for (int i = 2; i < argc; ++i) {
            if (const int status = replay(argv[i], require_success); status != 0) {
                return status;
            }
        }
        return 0;
    }
    if (argc == 2 && std::string_view(argv[1]) == "--afl") {
#ifdef __AFL_HAVE_MANUAL_CONTROL
        __AFL_INIT();
        auto* buffer = __AFL_FUZZ_TESTCASE_BUF;
        const auto size = static_cast<std::size_t>(__AFL_FUZZ_TESTCASE_LEN);
        (void)nordiska::fuzz::json_to_pdf({buffer, size});
        return 0;
#else
        std::fprintf(stderr, "AFL mode requires an AFL++ compiler; this build supports replay only.\n");
        return 2;
#endif
    }
    std::fprintf(stderr, "Usage: %s --afl | --replay FILE... | --require-success FILE... | --max-input-bytes\n",
                 argv[0]);
    return argc == 2 && std::string_view(argv[1]) == "--help" ? 0 : 2;
}
