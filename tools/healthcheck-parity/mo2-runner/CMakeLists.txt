cmake_minimum_required(VERSION 3.16)

# Standalone parity runner.
#
# Deliberately independent of the main organizer target: the health-check core
# (resolver + mapper) has no dependency on MO2's UI, plugin or VFS layers, so the
# parity suite can be built and run without building the whole application.

project(healthcheck-parity-runner CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_AUTOMOC OFF)

find_package(Qt6 REQUIRED COMPONENTS Core)

set(HEALTHCHECK_DIR "${CMAKE_CURRENT_SOURCE_DIR}/../../../src/healthcheck")

add_executable(healthcheck-parity-runner
    main.cpp
    "${HEALTHCHECK_DIR}/healthchecktypes.cpp"
    "${HEALTHCHECK_DIR}/nexusuid.cpp"
    "${HEALTHCHECK_DIR}/filedependencyresolver.cpp"
    "${HEALTHCHECK_DIR}/requirementsreportmapper.cpp"
)

target_include_directories(healthcheck-parity-runner PRIVATE "${HEALTHCHECK_DIR}")
target_link_libraries(healthcheck-parity-runner PRIVATE Qt6::Core)

if (MSVC)
    target_compile_options(healthcheck-parity-runner PRIVATE /EHsc /W4)
endif()
