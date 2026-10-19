add_executable(nordiska_fuzz_json_to_pdf json_to_pdf.cpp afl_driver.cpp)
target_link_libraries(nordiska_fuzz_json_to_pdf PRIVATE nordiska_document_core)
set_target_properties(nordiska_fuzz_json_to_pdf PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/tests/fuzz")

add_executable(nordiska_fuzz_pdf_signing_slot fuzz_pdf_signing_slot.cpp)
target_link_libraries(nordiska_fuzz_pdf_signing_slot PRIVATE nordiska_document_core OpenSSL::Crypto)
set_target_properties(nordiska_fuzz_pdf_signing_slot PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/tests/fuzz")

add_executable(nordiska_fuzz_combinatorial_pipeline fuzz_combinatorial_pipeline.cpp)
target_link_libraries(nordiska_fuzz_combinatorial_pipeline PRIVATE nordiska_document_core nordiska_document_input_json OpenSSL::Crypto)
set_target_properties(nordiska_fuzz_combinatorial_pipeline PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/tests/fuzz")

add_executable(nordiska_fuzz_c_api fuzz_c_api.cpp)
target_link_libraries(nordiska_fuzz_c_api PRIVATE nordiska_pdf_generator_c_api nordiska_document_core OpenSSL::Crypto)
set_target_properties(nordiska_fuzz_c_api PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/tests/fuzz")

if(BUILD_TESTING)
    file(GLOB seed_inputs CONFIGURE_DEPENDS "${CMAKE_CURRENT_SOURCE_DIR}/seeds/*.json")
    add_test(NAME nordiska_fuzz_seed_replay
        COMMAND nordiska_fuzz_json_to_pdf --require-success ${seed_inputs})

    add_test(NAME nordiska_fuzz_pdf_signing_slot
        COMMAND nordiska_fuzz_pdf_signing_slot)

    add_test(NAME nordiska_fuzz_combinatorial_pipeline
        COMMAND nordiska_fuzz_combinatorial_pipeline)

    add_test(NAME nordiska_fuzz_c_api
        COMMAND nordiska_fuzz_c_api)

    set_tests_properties(
        nordiska_fuzz_seed_replay
        nordiska_fuzz_pdf_signing_slot
        nordiska_fuzz_combinatorial_pipeline
        nordiska_fuzz_c_api
        PROPERTIES WORKING_DIRECTORY "${CMAKE_SOURCE_DIR}"
    )
endif()
