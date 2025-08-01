cmake_minimum_required(VERSION 3.24)

# Qt 설정
set(CMAKE_AUTOMOC ON)
set(CMAKE_AUTORCC ON)
set(CMAKE_AUTOUIC ON)

# MSVC 설정
if(MSVC)
    add_compile_options(/utf-8)
    add_compile_definitions(_CRT_SECURE_NO_WARNINGS UNICODE _UNICODE)
endif()

# 클라이언트 소스 파일들
set(CLIENT_SOURCES
    main.cpp
    "src/GameBoard.cpp"
    "src/GameRoomWindow.cpp"
    "src/ImprovedBlockPalette.cpp"
    "src/LoginWindow.cpp"
    "src/LobbyWindow.cpp"
    "src/UserInfoDialog.cpp"
    "src/AfkNotificationDialog.cpp"
    "src/Block.cpp"
    "src/QtAdapter.cpp"
    "src/ClientUtils.cpp"
    "src/ResponsiveUI.cpp"
    "src/NetworkClient.cpp"
    "src/ClientConfigManager.cpp"
)

set(CLIENT_HEADERS
    "include/GameBoard.h"
    "include/GameRoomWindow.h"
    "include/ImprovedBlockPalette.h"
    "include/LoginWindow.h"
    "include/LobbyWindow.h"
    "include/UserInfoDialog.h"
    "include/AfkNotificationDialog.h"
    "include/ClientTypes.h"
    "include/QtAdapter.h"
    "include/ClientBlock.h"
    "include/ClientLogic.h"
    "include/ResponsiveUI.h"
    "include/NetworkClient.h"
    "include/ClientConfigManager.h"
)

# 실행 파일 생성
add_executable(BlokusClient 
    ${CLIENT_SOURCES} 
    ${CLIENT_HEADERS}
)

# 인클루드 디렉토리
target_include_directories(BlokusClient PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

# 라이브러리 링크
target_link_libraries(BlokusClient PRIVATE
    BlokusCommon
    BlokusProto
    Qt5::Core
    Qt5::Widgets
    Qt5::Gui
    Qt5::Svg
    Qt5::Network
)

# Windows 설정
if(WIN32)
    # 디버그 빌드에서만 콘솔 창 표시
    if(CMAKE_BUILD_TYPE STREQUAL "Debug")
        set_target_properties(BlokusClient PROPERTIES
            WIN32_EXECUTABLE FALSE  # 콘솔 애플리케이션으로 설정
        )
        message(STATUS "Debug build: Console window enabled")
    else()
        set_target_properties(BlokusClient PROPERTIES
            WIN32_EXECUTABLE TRUE   # Windows 애플리케이션으로 설정 (콘솔 창 숨김)
        )
        message(STATUS "Release build: Console window disabled")
    endif()
endif()

# 컴파일러 플래그
if(MSVC)
    target_compile_options(BlokusClient PRIVATE /W4 /permissive-)
endif()

# 소스 그룹
source_group("Client\\Headers" FILES ${CLIENT_HEADERS})
source_group("Client\\Sources" FILES ${CLIENT_SOURCES})

# ===========================================
# 환경변수 기반 설정
# ===========================================

message(STATUS "Server configuration now uses environment variables:")
message(STATUS "  BLOKUS_SERVER_HOST (default: localhost)")
message(STATUS "  BLOKUS_SERVER_PORT (default: 9999)")