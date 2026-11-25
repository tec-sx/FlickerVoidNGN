# The engine's DLSS library over NVIDIA's DLSS SDK; build.ps1 (Windows) and build.sh (Linux) fetch the SDK and the Vulkan
# headers, then build this.
cmake_minimum_required(VERSION 3.22)
project(FlickerVoidDlss LANGUAGES CXX)

set(DLSS_SDK "" CACHE PATH "The DLSS SDK (github.com/NVIDIA/DLSS)")
set(VULKAN_HEADERS "" CACHE PATH "The Vulkan headers (github.com/KhronosGroup/Vulkan-Headers)")

add_library(FlickerVoidDlss SHARED FlickerVoid.Dlss.cpp)
set_target_properties(FlickerVoidDlss PROPERTIES
    OUTPUT_NAME FlickerVoid.Dlss
    CXX_STANDARD 17
    CXX_STANDARD_REQUIRED ON
    CXX_VISIBILITY_PRESET hidden)
# System headers, so their warnings are not the library's.
target_include_directories(FlickerVoidDlss SYSTEM PRIVATE "${DLSS_SDK}/include" "${VULKAN_HEADERS}/include")

if(WIN32)
    # The static C runtime that nvsdk_ngx_s.lib is built for, so the library needs no runtime installed beside it.
    set_property(TARGET FlickerVoidDlss PROPERTY MSVC_RUNTIME_LIBRARY MultiThreaded)
    target_compile_options(FlickerVoidDlss PRIVATE /W4 /WX)
    target_link_libraries(FlickerVoidDlss PRIVATE "${DLSS_SDK}/lib/Windows_x86_64/x64/nvsdk_ngx_s.lib")
else()
    target_compile_options(FlickerVoidDlss PRIVATE -Wall -Wextra -Werror)

    # Keeps NGX's own symbols out of the library's exports.
    target_link_options(FlickerVoidDlss PRIVATE -Wl,--exclude-libs,ALL)
    target_link_libraries(FlickerVoidDlss PRIVATE "${DLSS_SDK}/lib/Linux_x86_64/libnvsdk_ngx.a" ${CMAKE_DL_LIBS})
endif()
