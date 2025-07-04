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
    src/main.cpp
    src/ui/GameBoard.cpp
    src/ui/GameRoomWindow.cpp
    src/ui/ImprovedBlockPalette.cpp
    src/ui/LoginWindow.cpp
    src/ui/LobbyWindow.cpp
    src/game/Block.cpp
    src/common/QtAdapter.cpp
)

set(CLIENT_HEADERS
    include/ui/GameBoard.h
    include/ui/GameRoomWindow.h
    include/ui/ImprovedBlockPalette.h
    include/ui/LoginWindow.h
    include/ui/LobbyWindow.h
    include/common/Types.h
    include/common/QtAdapter.h
    include/game/Block.h
    include/game/GameLogic.h
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
    Qt5::Core
    Qt5::Widgets
    Qt5::Gui
    Qt5::Svg
    Qt5::Network
    spdlog::spdlog
    nlohmann_json::nlohmann_json
)

# Windows 설정
if(WIN32)
    set_target_properties(BlokusClient PROPERTIES
        WIN32_EXECUTABLE TRUE
    )
    
    if(CMAKE_BUILD_TYPE STREQUAL "Debug")
        set_target_properties(BlokusClient PROPERTIES
            LINK_FLAGS "/SUBSYSTEM:CONSOLE"
        )
    endif()
endif()

# 컴파일러 플래그
if(MSVC)
    target_compile_options(BlokusClient PRIVATE /W4 /permissive-)
endif()

# 소스 그룹
source_group("Client\\Headers" FILES ${CLIENT_HEADERS})
source_group("Client\\Sources" FILES ${CLIENT_SOURCES})