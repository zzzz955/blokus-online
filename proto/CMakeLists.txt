cmake_minimum_required(VERSION 3.24)

# Protobuf 찾기 (최신 방식)
find_package(protobuf CONFIG REQUIRED)

# 프로토 파일들 수집
file(GLOB PROTO_FILES "${CMAKE_CURRENT_SOURCE_DIR}/*.proto")

#  최신 protobuf_generate 함수 사용
protobuf_generate(
    LANGUAGE cpp
    OUT_VAR PROTO_GENERATED_FILES
    PROTOS ${PROTO_FILES}
    PROTOC_OPTIONS --error_format=msvs
)

# 라이브러리 생성
add_library(BlokusProto STATIC ${PROTO_GENERATED_FILES})

# 인클루드 디렉토리 설정
target_include_directories(BlokusProto PUBLIC
    ${CMAKE_CURRENT_BINARY_DIR}  # 생성된 .pb.h 파일들
)

# protobuf 링크
target_link_libraries(BlokusProto PUBLIC 
    protobuf::libprotobuf
)

# C++ 표준
target_compile_features(BlokusProto PUBLIC cxx_std_20)

# 생성된 파일들 표시
message(STATUS "Proto files: ${PROTO_FILES}")
message(STATUS "Generated files: ${PROTO_GENERATED_FILES}")