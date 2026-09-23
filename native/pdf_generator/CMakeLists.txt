cmake_minimum_required(VERSION 3.20)

project(nordiska_pdf_generator LANGUAGES C CXX)

include(CTest)

set(CMAKE_EXPORT_COMPILE_COMMANDS ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_CXX_VISIBILITY_PRESET hidden)
set(CMAKE_VISIBILITY_INLINES_HIDDEN ON)

set(BUILD_SHARED_LIBS OFF CACHE BOOL "" FORCE)
include(FetchContent)

# 1. ZLIB (System package)
find_package(ZLIB REQUIRED)

# 2. OpenSSL (System package)
find_package(OpenSSL REQUIRED COMPONENTS Crypto)

# 3. Cairo (System package via pkg-config)
find_package(PkgConfig REQUIRED)
pkg_check_modules(CAIRO REQUIRED IMPORTED_TARGET cairo)

# 4. nlohmann_json (System package or FetchContent fallback)
find_package(nlohmann_json 3.11 CONFIG QUIET)
if(NOT nlohmann_json_FOUND)
    FetchContent_Declare(
        nlohmann_json
        GIT_REPOSITORY https://github.com/nlohmann/json.git
        GIT_TAG v3.11.3
    )
    FetchContent_MakeAvailable(nlohmann_json)
endif()

# 5. simdjson (System package or FetchContent fallback)
find_package(simdjson CONFIG QUIET)
if(NOT simdjson_FOUND)
    FetchContent_Declare(
        simdjson
        GIT_REPOSITORY https://github.com/simdjson/simdjson.git
        GIT_TAG v3.10.1
    )
    FetchContent_MakeAvailable(simdjson)
endif()

# 6. libharu (Config package, system library, or FetchContent fallback)
find_package(unofficial-libharu CONFIG QUIET)
if(NOT unofficial-libharu_FOUND)
    find_path(HPDF_INCLUDE_DIR NAMES hpdf.h)
    find_library(HPDF_LIB NAMES hpdf libhpdf)
    if(HPDF_INCLUDE_DIR AND HPDF_LIB)
        add_library(unofficial::libharu::hpdf UNKNOWN IMPORTED)
        set_target_properties(unofficial::libharu::hpdf PROPERTIES
            IMPORTED_LOCATION "${HPDF_LIB}"
            INTERFACE_INCLUDE_DIRECTORIES "${HPDF_INCLUDE_DIR}"
        )
    else()
        FetchContent_Declare(
            libharu
            GIT_REPOSITORY https://github.com/libharu/libharu.git
            GIT_TAG v2.4.6
        )
        set(LIBHPDF_EXAMPLES OFF CACHE BOOL "" FORCE)
        FetchContent_MakeAvailable(libharu)
        if(TARGET hpdf AND NOT TARGET unofficial::libharu::hpdf)
            target_include_directories(hpdf PUBLIC
                $<BUILD_INTERFACE:${libharu_SOURCE_DIR}/include>
                $<BUILD_INTERFACE:${libharu_BINARY_DIR}/include>
            )
            add_library(unofficial::libharu::hpdf ALIAS hpdf)
        endif()
    endif()
endif()

# pdf-signer integration — we compile the signer developer's C sources from his
# module directory, using the same settings as his Makefile: C11, his include/,
# and OpenSSL::Crypto. We do not own, copy, or modify any file under that path.
# NORDISKA_PDF_SIGNER_SOURCE_DIR can be overridden via -D to point at a
# different checkout (e.g. CI) without changing this file.
set(NORDISKA_PDF_SIGNER_SOURCE_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../pdf-signer" CACHE PATH "PDF signer source directory")
add_library(nordiska_pdf_signer_native STATIC
    ${NORDISKA_PDF_SIGNER_SOURCE_DIR}/src/pdf_sign.c
    ${NORDISKA_PDF_SIGNER_SOURCE_DIR}/src/key_load.c
    ${NORDISKA_PDF_SIGNER_SOURCE_DIR}/src/cert_load.c
    ${NORDISKA_PDF_SIGNER_SOURCE_DIR}/src/debug_log.c
)
set_target_properties(nordiska_pdf_signer_native PROPERTIES
    POSITION_INDEPENDENT_CODE ON
    C_STANDARD 11
    C_STANDARD_REQUIRED ON
    C_VISIBILITY_PRESET hidden
)
target_compile_options(nordiska_pdf_signer_native PRIVATE -Wall -Wextra -Wpedantic)
target_include_directories(nordiska_pdf_signer_native PUBLIC ${NORDISKA_PDF_SIGNER_SOURCE_DIR}/include)
target_link_libraries(nordiska_pdf_signer_native PRIVATE OpenSSL::Crypto)

# JSON Ingestion Library
add_library(nordiska_document_input_json STATIC
    src/ingestion/json_ingestor.cpp
)
set_target_properties(nordiska_document_input_json PROPERTIES POSITION_INDEPENDENT_CODE ON)
target_include_directories(nordiska_document_input_json PUBLIC include)
target_compile_features(nordiska_document_input_json PUBLIC cxx_std_23)
target_link_libraries(nordiska_document_input_json PUBLIC
    nlohmann_json::nlohmann_json
    simdjson::simdjson
)

# Core Domain, Layout & PDF Rendering Engine Library
add_library(nordiska_document_core STATIC
    src/application/pdf_generator.cpp
    src/rendering/pdf_engine.cpp
    src/rendering/haru_pdf_engine.cpp
    src/rendering/cairo_pdf_engine.cpp
    src/rendering/native_pdf_engine.cpp
    src/layout/layout_builder.cpp
    src/signing/pdf_signer.cpp
    src/signing/signature_slot_appender.cpp
)
set_target_properties(nordiska_document_core PROPERTIES POSITION_INDEPENDENT_CODE ON)
target_include_directories(nordiska_document_core PUBLIC include)
target_compile_features(nordiska_document_core PUBLIC cxx_std_23)
target_link_libraries(nordiska_document_core PUBLIC
    nordiska_document_input_json
    unofficial::libharu::hpdf
    PkgConfig::CAIRO
    ZLIB::ZLIB
    PRIVATE OpenSSL::Crypto nordiska_pdf_signer_native
)

# C ABI Shared Library for P/Invoke (.NET / Host Interop)
add_library(nordiska_pdf_generator_c_api SHARED
    src/c_api/pdf_generator_c_api.cpp
)
set_target_properties(nordiska_pdf_generator_c_api PROPERTIES
    C_VISIBILITY_PRESET hidden
    CXX_VISIBILITY_PRESET hidden
    VISIBILITY_INLINES_HIDDEN ON
)
target_include_directories(nordiska_pdf_generator_c_api PUBLIC include)
target_compile_features(nordiska_pdf_generator_c_api PUBLIC cxx_std_23)
target_link_libraries(nordiska_pdf_generator_c_api PRIVATE
    nordiska_document_core
    nordiska_document_input_json
)

# Diagnostics & Metrics Library
add_library(nordiska_document_diagnostics STATIC
    src/diagnostics/benchmark_metrics.cpp
)
target_include_directories(nordiska_document_diagnostics PUBLIC include)
target_compile_features(nordiska_document_diagnostics PUBLIC cxx_std_23)

# Benchmark Executable
add_executable(pdf_generator_benchmark src/benchmark/main.cpp)
target_link_libraries(pdf_generator_benchmark PRIVATE
    nordiska_document_core
    nordiska_document_input_json
    nordiska_document_diagnostics
    nordiska_pdf_generator_c_api
)

# Standalone CLI Subproject
add_subdirectory(cli)

# Installation targets
install(TARGETS nordiska_pdf_generator_c_api LIBRARY DESTINATION lib ARCHIVE DESTINATION lib)

if(BUILD_TESTING)
    add_executable(nordiska_pdf_signing_tests tests/pdf_signing_tests.cpp)
    target_link_libraries(nordiska_pdf_signing_tests PRIVATE nordiska_document_core nordiska_pdf_signer_native OpenSSL::Crypto)
    add_test(NAME nordiska_pdf_signing_tests COMMAND nordiska_pdf_signing_tests)

    add_executable(nordiska_diagnostics_tests tests/benchmark_metrics_tests.cpp)
    target_link_libraries(nordiska_diagnostics_tests PRIVATE nordiska_document_diagnostics)
    add_test(NAME nordiska_diagnostics_tests COMMAND nordiska_diagnostics_tests)

    add_executable(nordiska_json_ingestor_tests tests/json_ingestor_tests.cpp)
    target_link_libraries(nordiska_json_ingestor_tests PRIVATE nordiska_document_input_json nordiska_document_core)
    add_test(NAME nordiska_json_ingestor_tests COMMAND nordiska_json_ingestor_tests)

    add_executable(nordiska_pdf_generator_c_api_tests tests/pdf_generator_c_api_tests.cpp)
    target_link_libraries(nordiska_pdf_generator_c_api_tests PRIVATE nordiska_pdf_generator_c_api nordiska_document_core)
    add_test(NAME nordiska_pdf_generator_c_api_tests COMMAND nordiska_pdf_generator_c_api_tests)

    set_tests_properties(
        nordiska_pdf_signing_tests
        nordiska_diagnostics_tests
        nordiska_json_ingestor_tests
        nordiska_pdf_generator_c_api_tests
        PROPERTIES WORKING_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}"
    )
endif()
