cmake_minimum_required(VERSION 3.20)

project(nordiska_pdf_generator LANGUAGES CXX)

include(CTest)

set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_CXX_VISIBILITY_PRESET hidden)
set(CMAKE_VISIBILITY_INLINES_HIDDEN ON)

find_package(unofficial-libharu CONFIG REQUIRED)
find_package(nlohmann_json CONFIG REQUIRED)
find_package(simdjson CONFIG REQUIRED)
find_package(ZLIB REQUIRED)
find_package(PkgConfig REQUIRED)
pkg_check_modules(CAIRO REQUIRED IMPORTED_TARGET cairo)

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
)
set_target_properties(nordiska_document_core PROPERTIES POSITION_INDEPENDENT_CODE ON)
target_include_directories(nordiska_document_core PUBLIC include)
target_compile_features(nordiska_document_core PUBLIC cxx_std_23)
target_link_libraries(nordiska_document_core PUBLIC
    nordiska_document_input_json
    unofficial::libharu::hpdf
    PkgConfig::CAIRO
    ZLIB::ZLIB
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
    add_executable(nordiska_diagnostics_tests tests/benchmark_metrics_tests.cpp)
    target_link_libraries(nordiska_diagnostics_tests PRIVATE nordiska_document_diagnostics)
    add_test(NAME nordiska_diagnostics_tests COMMAND nordiska_diagnostics_tests)

    add_executable(nordiska_json_ingestor_tests tests/json_ingestor_tests.cpp)
    target_link_libraries(nordiska_json_ingestor_tests PRIVATE nordiska_document_input_json nordiska_document_core)
    add_test(NAME nordiska_json_ingestor_tests COMMAND nordiska_json_ingestor_tests)

    add_executable(nordiska_pdf_generator_c_api_tests tests/pdf_generator_c_api_tests.cpp)
    target_link_libraries(nordiska_pdf_generator_c_api_tests PRIVATE nordiska_pdf_generator_c_api nordiska_document_core)
    add_test(NAME nordiska_pdf_generator_c_api_tests COMMAND nordiska_pdf_generator_c_api_tests)
endif()
