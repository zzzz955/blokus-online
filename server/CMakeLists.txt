cmake_minimum_required(VERSION 3.24)

# MSVC ����
if(MSVC)
    add_compile_options(/utf-8)
    add_compile_definitions(_CRT_SECURE_NO_WARNINGS UNICODE _UNICODE)
    add_compile_definitions(SPDLOG_WCHAR_TO_UTF8_SUPPORT)
endif()

# ���� �ҽ� ���ϵ�
set(SERVER_SOURCES
    main.cpp
    src/ConfigManager.cpp
    src/DatabaseManager.cpp
    src/GameServer.cpp
    src/Session.cpp
    src/GameRoom.cpp
    src/MessageHandler.cpp
    src/RoomManager.cpp
    src/AuthenticationService.cpp
    src/ServerTypes.cpp
    src/PlayerInfo.cpp
    src/VersionManager.cpp
)

# �ٽ� ��� ���ϵ�
set(SERVER_HEADERS
    include/ServerTypes.h
    include/GameServer.h
    include/Session.h
    include/MessageHandler.h
    include/GameRoom.h
    include/RoomManager.h
    include/DatabaseManager.h
    include/ConfigManager.h
    include/AuthenticationService.h
    include/PlayerInfo.h
    include/VersionManager.h
)

# ���� ���� ����
add_executable(BlokusServer 
    ${SERVER_SOURCES} 
    ${SERVER_HEADERS}
)

# C++ ǥ�� ���� (ȣȯ���� ���� 17�� ����)
set_property(TARGET BlokusServer PROPERTY CXX_STANDARD 17)
set_property(TARGET BlokusServer PROPERTY CXX_STANDARD_REQUIRED ON)

# ��Ŭ��� ���丮
target_include_directories(BlokusServer PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/include
)

# PostgreSQL �� ��Ÿ ���̺귯�� ã��
find_package(libpqxx CONFIG REQUIRED)

# ���̺귯�� ��ũ
target_link_libraries(BlokusServer PRIVATE
    BlokusCommon
    Boost::system
    libpqxx::pqxx
)

# Windows ����
if(WIN32)
    set_target_properties(BlokusServer PROPERTIES
        WIN32_EXECUTABLE FALSE  # �ܼ� ���ø����̼�
    )
endif()

# �����Ϸ� �÷���
if(MSVC)
    target_compile_options(BlokusServer PRIVATE 
        /W4 
        /permissive-
        /Zc:preprocessor  # ��ó���� ȣȯ��
        /D_SILENCE_CXX17_ITERATOR_BASE_CLASS_DEPRECATION_WARNING
    )
    target_compile_definitions(BlokusServer PRIVATE
        NOMINMAX
        WIN32_LEAN_AND_MEAN
        _WIN32_WINNT=0x0601  # Windows 7+
    )
endif()

# �ҽ� �׷�
source_group("Server\\Headers" FILES ${SERVER_HEADERS})
source_group("Server\\Sources" FILES ${SERVER_SOURCES})