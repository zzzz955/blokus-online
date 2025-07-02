cmake_minimum_required(VERSION 3.24)

# 공통 라이브러리 이름
set(LIB_NAME BlokusCommon)

# 소스 파일들
set(SOURCES
    src/Block.cpp
    src/GameLogic.cpp
    src/Utils.cpp
)

# 헤더 파일들 (선택사항, 문서화용)
set(HEADERS
    include/common/Types.h
    include/common/Block.h
    include/common/GameLogic.h
    include/common/Utils.h
)

# 정적 라이브러리 생성
add_library(${LIB_NAME} STATIC ${SOURCES} ${HEADERS})

# 🔥 핵심: PUBLIC include 디렉토리 설정
target_include_directories(${LIB_NAME} PUBLIC
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

# C++ 표준 설정
target_compile_features(${LIB_NAME} PUBLIC cxx_std_20)

# 컴파일러 옵션
if(MSVC)
    target_compile_options(${LIB_NAME} PRIVATE /utf-8)
endif()