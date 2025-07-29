cmake_minimum_required(VERSION 3.24)

# 소스 파일들
set(SOURCES
    src/Block.cpp
    src/GameLogic.cpp
    src/Utils.cpp
)

set(HEADERS
    "include/Types.h"
    "include/Block.h"
    "include/GameLogic.h"
    "include/Utils.h"
)

# 정적 라이브러리 생성
add_library(BlokusCommon STATIC ${SOURCES} ${HEADERS})

# 인클루드 디렉토리 설정
target_include_directories(BlokusCommon PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

# 라이브러리 링크
target_link_libraries(BlokusCommon PUBLIC
    BlokusProto
    spdlog::spdlog
    nlohmann_json::nlohmann_json
)

# C++ 표준 설정
target_compile_features(BlokusCommon PUBLIC cxx_std_20)

# 컴파일러 옵션
if(MSVC)
    target_compile_options(BlokusCommon PRIVATE /utf-8)
endif()