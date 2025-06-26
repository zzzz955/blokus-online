cmake_minimum_required(VERSION 3.24)

project(BlokusOnline VERSION 1.0.0 LANGUAGES CXX)

# C++ 표준 설정
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# vcpkg 통합
if(DEFINED ENV{VCPKG_ROOT} AND NOT DEFINED CMAKE_TOOLCHAIN_FILE)
    set(CMAKE_TOOLCHAIN_FILE "$ENV{VCPKG_ROOT}/scripts/buildsystems/vcpkg.cmake"
        CACHE STRING "")
endif()

# 전역 설정
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)

# 빌드 타입별 설정
if(NOT CMAKE_BUILD_TYPE)
    set(CMAKE_BUILD_TYPE Debug)
endif()

# MSVC 설정
if(MSVC)
    # UTF-8 인코딩 강제
    add_compile_options(/utf-8)
    # 경고 레벨
    add_compile_options(/W4)
    # 병렬 빌드
    add_compile_options(/MP)
    # 런타임 라이브러리
    set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded$<$<CONFIG:Debug>:Debug>DLL")
endif()

# 디렉토리 추가 (개발 단계별로 추가)
add_subdirectory(client)

# 나중에 추가될 서브 프로젝트들
# add_subdirectory(server)
# add_subdirectory(proto)
# add_subdirectory(web-service)

# 전역 타겟 설정
set_property(GLOBAL PROPERTY USE_FOLDERS ON)

# 패키지 정보 출력
message(STATUS "=== Blokus Online Build Configuration ===")
message(STATUS "Build type: ${CMAKE_BUILD_TYPE}")
message(STATUS "C++ standard: ${CMAKE_CXX_STANDARD}")
message(STATUS "Compiler: ${CMAKE_CXX_COMPILER_ID} ${CMAKE_CXX_COMPILER_VERSION}")
if(CMAKE_TOOLCHAIN_FILE)
    message(STATUS "Toolchain: ${CMAKE_TOOLCHAIN_FILE}")
endif()
message(STATUS "==========================================")