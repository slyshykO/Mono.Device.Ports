if(NOT CMAKE_SYSTEM_NAME MATCHES "^(Linux|Darwin)$")
    message(FATAL_ERROR "The native helper targets Linux/macOS. Windows uses the managed Win32 backend; use the Zig toolchain to cross-compile for Linux.")
endif()

include(CheckCSourceCompiles)
if(CMAKE_SYSTEM_NAME STREQUAL "Linux")
    check_c_source_compiles("
        #include <asm/termbits.h>
        #include <asm/ioctls.h>
        #include <sys/ioctl.h>
        int main(void) {
            struct termios2 t = {0};
            t.c_cflag = BOTHER;
            return ioctl(-1, TCGETS2, &t) + ioctl(-1, TCSETS2, &t);
        }" MDP_HAVE_TERMIOS2)
endif()

add_library(MonoDevicePortsNative SHARED serial.c)
target_compile_features(MonoDevicePortsNative PRIVATE c_std_99)
target_compile_definitions(MonoDevicePortsNative PRIVATE _GNU_SOURCE)
target_include_directories(MonoDevicePortsNative PUBLIC "${CMAKE_CURRENT_SOURCE_DIR}")
set_target_properties(MonoDevicePortsNative PROPERTIES
    C_VISIBILITY_PRESET hidden
    LIBRARY_OUTPUT_DIRECTORY "${CMAKE_BINARY_DIR}/lib")
if(CMAKE_C_COMPILER_ID MATCHES "GNU|Clang")
    target_compile_options(MonoDevicePortsNative PRIVATE -Wall -Wextra -Werror)
endif()
if(MDP_HAVE_TERMIOS2)
    target_sources(MonoDevicePortsNative PRIVATE termios2.c)
    target_compile_definitions(MonoDevicePortsNative PRIVATE MDP_HAVE_TERMIOS2=1)
endif()

# Install directly into a NuGet staging tree when a runtime identifier is supplied.
set(MDP_RUNTIME_ID "" CACHE STRING "NuGet runtime identifier, e.g. linux-musl-arm64")
if(MDP_RUNTIME_ID)
    install(TARGETS MonoDevicePortsNative LIBRARY DESTINATION "runtimes/${MDP_RUNTIME_ID}/native")
else()
    install(TARGETS MonoDevicePortsNative LIBRARY DESTINATION lib)
endif()

if(BUILD_TESTING)
    add_executable(serial_native_tests tests/serial_tests.c)
    target_compile_definitions(serial_native_tests PRIVATE _GNU_SOURCE)
    target_link_libraries(serial_native_tests PRIVATE MonoDevicePortsNative)
    if(MDP_HAVE_TERMIOS2)
        target_sources(serial_native_tests PRIVATE tests/custom_baud_test.c)
        target_compile_definitions(serial_native_tests PRIVATE MDP_HAVE_TERMIOS2=1)
    endif()
    add_test(NAME serial_native COMMAND serial_native_tests)
    set_tests_properties(serial_native PROPERTIES TIMEOUT 15)
    add_test(NAME zig_version_order COMMAND "${CMAKE_COMMAND}" -P "${PROJECT_SOURCE_DIR}/cmake/tests/ZigVersionTests.cmake")
endif()
