cmake_minimum_required(VERSION 3.24)

# MSVC 설정
if(MSVC)
    add_compile_options(/utf-8)
    add_compile_definitions(_CRT_SECURE_NO_WARNINGS UNICODE _UNICODE)
endif()

# 서버 소스 파일들 (추후 구현)
set(SERVER_SOURCES
    main.cpp
    # src/GameServer.cpp
    # src/ClientConnection.cpp
    # src/RoomManager.cpp
    # src/GameManager.cpp
    # src/MessageHandler.cpp
    # src/DatabaseManager.cpp
    # src/RedisManager.cpp
)

set(SERVER_HEADERS
    include/server/GameServer.h
    include/server/ServerTypes.h
    # include/server/ClientConnection.h
    # include/server/RoomManager.h
    # include/server/GameManager.h
    # include/server/MessageHandler.h
    # include/server/DatabaseManager.h
    # include/server/RedisManager.h
)

# 실행 파일 생성
add_executable(BlokusServer 
    ${SERVER_SOURCES} 
    ${SERVER_HEADERS}
)

# 인클루드 디렉토리
target_include_directories(BlokusServer PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

# 라이브러리 링크
target_link_libraries(BlokusServer PRIVATE
    BlokusCommon
    BlokusProto
    spdlog::spdlog
    nlohmann_json::nlohmann_json
    Boost::system
)

# Windows 설정
if(WIN32)
    set_target_properties(BlokusServer PROPERTIES
        WIN32_EXECUTABLE FALSE  # 콘솔 애플리케이션
    )
endif()

# 컴파일러 플래그
if(MSVC)
    target_compile_options(BlokusServer PRIVATE /W4 /permissive-)
endif()

# 소스 그룹
source_group("Server\\Headers" FILES ${SERVER_HEADERS})
source_group("Server\\Sources" FILES ${SERVER_SOURCES})