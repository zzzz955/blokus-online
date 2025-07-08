cmake_minimum_required(VERSION 3.24)

# MSVC 설정
if(MSVC)
    add_compile_options(/utf-8)
    add_compile_definitions(_CRT_SECURE_NO_WARNINGS UNICODE _UNICODE)
endif()

# 서버 소스 파일들
set(SERVER_SOURCES
    main.cpp
    src/GameServer.cpp
    src/ClientSession.cpp
    src/Room.cpp
    src/UserManager.cpp
    src/MessageHandler.cpp
    src/NetworkManager.cpp
    src/AuthenticationService.cpp
    src/GameService.cpp
    src/ProtocolHandler.cpp
)

set(SERVER_HEADERS
    include/server/GameServer.h
    include/server/ClientSession.h
    include/server/Room.h
    include/server/UserManager.h
    include/server/MessageHandler.h
    include/server/NetworkManager.h
    include/server/AuthenticationService.h
    include/server/GameService.h
    include/server/ProtocolHandler.h
    include/server/ServerTypes.h
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
    spdlog::spdlog
    nlohmann_json::nlohmann_json
    Boost::system
)

# Windows 설정
if(WIN32)
    target_link_libraries(BlokusServer PRIVATE
        ws2_32
        mswsock
    )
endif()

# 컴파일러 플래그
if(MSVC)
    target_compile_options(BlokusServer PRIVATE /W4 /permissive-)
endif()

# 소스 그룹
source_group("Server\\Headers" FILES ${SERVER_HEADERS})
source_group("Server\\Sources" FILES ${SERVER_SOURCES})