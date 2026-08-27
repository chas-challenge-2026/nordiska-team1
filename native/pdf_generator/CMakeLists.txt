cmake_minimum_required(VERSION 3.20)

project(nordiska_pdf_generator LANGUAGES CXX)

include(CTest)

set(CMAKE_EXPORT_COMPILE_COMMANDS ON)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)

find_package(unofficial-libharu CONFIG REQUIRED)
find_package(nlohmann_json CONFIG REQUIRED)
find_package(PkgConfig REQUIRED)
pkg_check_modules(CAIRO REQUIRED IMPORTED_TARGET cairo)

add_library(nordiska_document_core STATIC
    src/domain/report.cpp
    src/application/generate_documents.cpp
)
set_target_properties(nordiska_document_core PROPERTIES POSITION_INDEPENDENT_CODE ON)
target_include_directories(nordiska_document_core PUBLIC include)
target_compile_features(nordiska_document_core PUBLIC cxx_std_23)

add_library(nordiska_document_input_json STATIC
    src/adapters/input/json_input_adapter.cpp
)
set_target_properties(nordiska_document_input_json PROPERTIES POSITION_INDEPENDENT_CODE ON)
target_include_directories(nordiska_document_input_json PUBLIC include)
target_compile_features(nordiska_document_input_json PUBLIC cxx_std_23)
target_link_libraries(nordiska_document_input_json PUBLIC nlohmann_json::nlohmann_json)

add_library(nordiska_document_output STATIC
    src/adapters/output/byte_sinks.cpp
)
set_target_properties(nordiska_document_output PROPERTIES POSITION_INDEPENDENT_CODE ON)
target_include_directories(nordiska_document_output PUBLIC include)
target_compile_features(nordiska_document_output PUBLIC cxx_std_23)

add_library(nordiska_document_renderer_pdf STATIC
    src/adapters/renderers/pdf/pdf_renderer.cpp
    src/adapters/renderers/pdf/engines/libharu_engine.cpp
    src/adapters/renderers/pdf/engines/cairo_engine.cpp
)
set_target_properties(nordiska_document_renderer_pdf PROPERTIES POSITION_INDEPENDENT_CODE ON)
target_include_directories(nordiska_document_renderer_pdf PUBLIC include PRIVATE src)
target_compile_features(nordiska_document_renderer_pdf PUBLIC cxx_std_23)
target_link_libraries(nordiska_document_renderer_pdf PRIVATE unofficial::libharu::hpdf
    PkgConfig::CAIRO)

add_library(nordiska_document_composition STATIC
    src/composition/default_composition.cpp
)
set_target_properties(nordiska_document_composition PROPERTIES POSITION_INDEPENDENT_CODE ON)
target_include_directories(nordiska_document_composition PUBLIC include)
target_compile_features(nordiska_document_composition PUBLIC cxx_std_23)
target_link_libraries(nordiska_document_composition PUBLIC nordiska_document_renderer_pdf)

add_library(nordiska_document_c_api SHARED
    src/c_api/document_c_api.cpp
)
target_include_directories(nordiska_document_c_api PUBLIC include)
target_compile_features(nordiska_document_c_api PUBLIC cxx_std_23)
target_link_libraries(nordiska_document_c_api PRIVATE
    nordiska_document_core
    nordiska_document_input_json
    nordiska_document_output
    nordiska_document_composition
)

add_library(nordiska_document_diagnostics STATIC
    src/diagnostics/benchmark_metrics.cpp
)
target_include_directories(nordiska_document_diagnostics PUBLIC include)
target_compile_features(nordiska_document_diagnostics PUBLIC cxx_std_23)

add_executable(pdf_generator_benchmark src/benchmark/main.cpp)
target_link_libraries(pdf_generator_benchmark PRIVATE
    nordiska_document_core
    nordiska_document_input_json
    nordiska_document_output
    nordiska_document_renderer_pdf
    nordiska_document_diagnostics
)

if(BUILD_TESTING)
    add_executable(nordiska_document_application_tests tests/application_tests.cpp)
    target_link_libraries(nordiska_document_application_tests PRIVATE nordiska_document_core
        nordiska_document_output nordiska_document_renderer_pdf)
    add_test(NAME nordiska_document_application_tests COMMAND nordiska_document_application_tests)

    add_executable(nordiska_json_input_adapter_tests tests/json_input_adapter_tests.cpp)
    target_link_libraries(nordiska_json_input_adapter_tests PRIVATE nordiska_document_input_json)
    add_test(NAME nordiska_json_input_adapter_tests COMMAND nordiska_json_input_adapter_tests)

    add_executable(nordiska_byte_sink_tests tests/byte_sink_tests.cpp)
    target_link_libraries(nordiska_byte_sink_tests PRIVATE nordiska_document_output)
    add_test(NAME nordiska_byte_sink_tests COMMAND nordiska_byte_sink_tests)

    add_executable(nordiska_diagnostics_tests tests/benchmark_metrics_tests.cpp)
    target_link_libraries(nordiska_diagnostics_tests PRIVATE nordiska_document_diagnostics)
    add_test(NAME nordiska_diagnostics_tests COMMAND nordiska_diagnostics_tests)

    add_executable(nordiska_document_c_api_tests tests/document_c_api_tests.cpp)
    target_link_libraries(nordiska_document_c_api_tests PRIVATE nordiska_document_c_api)
    add_test(NAME nordiska_document_c_api_tests COMMAND nordiska_document_c_api_tests)
endif()
