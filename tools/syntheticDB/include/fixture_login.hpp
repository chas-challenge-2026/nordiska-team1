#pragma once

#include <string_view>

namespace nordiska::synthetic {

// Shared, public credentials for generated local-development customers only.
inline constexpr std::string_view fixture_password = "password123";
inline constexpr std::string_view fixture_password_hash =
    "AQAAAAEAACcQAAAAEPI/M0/zVJYqy70AD5XmxCGusa4f003trDYZVeUzZHaVGSFDDNwRIMlWeLlSS6WCqg==";

} // namespace nordiska::synthetic
