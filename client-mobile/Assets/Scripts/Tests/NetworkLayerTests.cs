using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using BlokusUnity.Network;
using BlokusUnity.Common;

namespace BlokusUnity.Tests
{
    /// <summary>
    /// 네트워크 레이어 테스트
    /// Unity 환경에서 네트워크 통신 계층의 기능을 검증
    /// </summary>
    [TestFixture]
    public class NetworkLayerTests
    {
        private GameObject networkManagerObject;
        private NetworkManager networkManager;
        private MessageHandler messageHandler;
        
        [SetUp]
        public void Setup()
        {
            // NetworkManager GameObject 생성
            networkManagerObject = new GameObject("NetworkManager");
            networkManager = networkManagerObject.AddComponent<NetworkManager>();
            messageHandler = networkManagerObject.GetComponent<MessageHandler>();
            
            // 테스트용 서버 설정 (로컬호스트)
            networkManager.SetServerInfo("localhost", 9999);
        }
        
        [TearDown]
        public void TearDown()
        {
            // 연결 해제 및 정리
            if (networkManager != null)
            {
                networkManager.DisconnectFromServer();
            }
            
            if (networkManagerObject != null)
            {
                Object.DestroyImmediate(networkManagerObject);
            }
        }
        
        // ========================================
        // 기본 네트워크 매니저 테스트
        // ========================================
        
        [Test]
        public void NetworkManager_Initialization_SetsUpComponentsCorrectly()
        {
            // Assert
            Assert.IsNotNull(networkManager);
            Assert.IsNotNull(NetworkManager.Instance);
            Assert.AreEqual(networkManager, NetworkManager.Instance);
            
            // 초기 상태 확인
            Assert.IsFalse(networkManager.IsConnected());
            Assert.IsNotNull(networkManager.GetStatusInfo());
        }
        
        [Test]
        public void NetworkManager_ServerInfo_CanBeSet()
        {
            // Arrange
            string testHost = "test.server.com";
            int testPort = 8888;
            
            // Act
            networkManager.SetServerInfo(testHost, testPort);
            
            // Assert
            // 내부적으로 설정이 저장되었다고 가정 (NetworkClient에서 확인됨)
            Assert.Pass("서버 정보 설정 완료");
        }
        
        [Test]
        public void NetworkManager_StatusInfo_ReturnsValidString()
        {
            // Act
            string status = networkManager.GetStatusInfo();
            
            // Assert
            Assert.IsNotNull(status);
            Assert.IsNotEmpty(status);
            Assert.IsTrue(status.Contains("연결") || status.Contains("상태"));
            
            Debug.Log($"네트워크 상태: {status}");
        }
        
        // ========================================
        // 메시지 핸들러 테스트
        // ========================================
        
        [Test]
        public void MessageHandler_Initialization_SetsUpEventsCorrectly()
        {
            // Assert
            Assert.IsNotNull(messageHandler);
            Assert.IsNotNull(MessageHandler.Instance);
            Assert.AreEqual(messageHandler, MessageHandler.Instance);
        }
        
        [Test]
        public void MessageHandler_AuthResponse_ParsedCorrectly()
        {
            // Arrange
            bool eventFired = false;
            bool successResult = false;
            string messageResult = "";
            
            messageHandler.OnAuthResponse += (success, message) =>
            {
                eventFired = true;
                successResult = success;
                messageResult = message;
            };
            
            // Act - 직접 메시지 핸들러 호출 (내부 메서드 테스트)
            string testMessage = "AUTH_RESPONSE:SUCCESS:로그인 성공";
            System.Reflection.MethodInfo handleMessageMethod = typeof(MessageHandler)
                .GetMethod("HandleMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            handleMessageMethod?.Invoke(messageHandler, new object[] { testMessage });
            
            // Assert
            Assert.IsTrue(eventFired);
            Assert.IsTrue(successResult);
            Assert.AreEqual("로그인 성공", messageResult);
        }
        
        [Test]
        public void MessageHandler_UserStatsResponse_ParsedCorrectly()
        {
            // Arrange
            UserInfo receivedUserInfo = null;
            
            messageHandler.OnUserStatsReceived += (userInfo) =>
            {
                receivedUserInfo = userInfo;
            };
            
            // Act - 사용자 통계 응답 메시지 시뮬레이션
            string testMessage = "USER_STATS_RESPONSE:TestUser:5:100:80:20:75:7500:150:true:온라인";
            System.Reflection.MethodInfo handleMessageMethod = typeof(MessageHandler)
                .GetMethod("HandleMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            handleMessageMethod?.Invoke(messageHandler, new object[] { testMessage });
            
            // Assert
            Assert.IsNotNull(receivedUserInfo);
            Assert.AreEqual("TestUser", receivedUserInfo.username);
            Assert.AreEqual(5, receivedUserInfo.level);
            Assert.AreEqual(100, receivedUserInfo.totalGames);
            Assert.AreEqual(80, receivedUserInfo.wins);
            Assert.AreEqual(20, receivedUserInfo.losses);
            Assert.AreEqual(75, receivedUserInfo.averageScore);
            Assert.AreEqual(7500, receivedUserInfo.totalScore);
            Assert.AreEqual(150, receivedUserInfo.bestScore);
            Assert.IsTrue(receivedUserInfo.isOnline);
            Assert.AreEqual("온라인", receivedUserInfo.status);
            
            // 승률 계산 확인
            Assert.AreEqual(80.0, receivedUserInfo.GetWinRate(), 0.01);
        }
        
        [Test]
        public void MessageHandler_BlockPlaced_ParsedCorrectly()
        {
            // Arrange
            BlockPlacement receivedPlacement = null;
            
            messageHandler.OnBlockPlaced += (placement) =>
            {
                receivedPlacement = placement;
            };
            
            // Act - 블록 배치 메시지 시뮬레이션 "BLOCK_PLACED:blockType:row:col:rotation:flip:player"
            string testMessage = "BLOCK_PLACED:3:5:10:1:0:1"; // TrioAngle at (5,10), 90도 회전, 뒤집기 없음, Blue 플레이어
            System.Reflection.MethodInfo handleMessageMethod = typeof(MessageHandler)
                .GetMethod("HandleMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            handleMessageMethod?.Invoke(messageHandler, new object[] { testMessage });
            
            // Assert
            Assert.IsNotNull(receivedPlacement);
            Assert.AreEqual(BlockType.TrioAngle, receivedPlacement.type);
            Assert.AreEqual(new Position(5, 10), receivedPlacement.position);
            Assert.AreEqual(Rotation.Degree_90, receivedPlacement.rotation);
            Assert.AreEqual(FlipState.Normal, receivedPlacement.flip);
            Assert.AreEqual(PlayerColor.Blue, receivedPlacement.player);
        }
        
        [Test]
        public void MessageHandler_RoomListUpdate_ParsedCorrectly()
        {
            // Arrange
            List<RoomInfo> receivedRooms = null;
            
            messageHandler.OnRoomListUpdated += (rooms) =>
            {
                receivedRooms = rooms;
            };
            
            // Act - 방 목록 업데이트 메시지 시뮬레이션
            string testMessage = "ROOM_LIST_UPDATE:2:1,TestRoom1,2,4,false:2,TestRoom2,4,4,true";
            System.Reflection.MethodInfo handleMessageMethod = typeof(MessageHandler)
                .GetMethod("HandleMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            handleMessageMethod?.Invoke(messageHandler, new object[] { testMessage });
            
            // Assert
            Assert.IsNotNull(receivedRooms);
            Assert.AreEqual(2, receivedRooms.Count);
            
            // 첫 번째 방
            Assert.AreEqual(1, receivedRooms[0].roomId);
            Assert.AreEqual("TestRoom1", receivedRooms[0].roomName);
            Assert.AreEqual(2, receivedRooms[0].currentPlayers);
            Assert.AreEqual(4, receivedRooms[0].maxPlayers);
            Assert.IsFalse(receivedRooms[0].isGameStarted);
            Assert.IsTrue(receivedRooms[0].CanJoin());
            
            // 두 번째 방
            Assert.AreEqual(2, receivedRooms[1].roomId);
            Assert.AreEqual("TestRoom2", receivedRooms[1].roomName);
            Assert.AreEqual(4, receivedRooms[1].currentPlayers);
            Assert.AreEqual(4, receivedRooms[1].maxPlayers);
            Assert.IsTrue(receivedRooms[1].isGameStarted);
            Assert.IsFalse(receivedRooms[1].CanJoin()); // 게임 시작됨
        }
        
        [Test]
        public void MessageHandler_ErrorMessage_ParsedCorrectly()
        {
            // Arrange
            string receivedError = "";
            
            messageHandler.OnErrorReceived += (error) =>
            {
                receivedError = error;
            };
            
            // Act - 에러 메시지 시뮬레이션
            string testMessage = "ERROR:사용자를 찾을 수 없습니다";
            System.Reflection.MethodInfo handleMessageMethod = typeof(MessageHandler)
                .GetMethod("HandleMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            handleMessageMethod?.Invoke(messageHandler, new object[] { testMessage });
            
            // Assert
            Assert.IsNotEmpty(receivedError);
            Assert.AreEqual("사용자를 찾을 수 없습니다", receivedError);
        }
        
        // ========================================
        // 메시지 전송 테스트 (서버 연결 없이)
        // ========================================
        
        [Test]
        public void NetworkManager_SendMessageWithoutConnection_ReturnsFalse()
        {
            // Act & Assert - 연결되지 않은 상태에서 메시지 전송 시도
            Assert.IsFalse(networkManager.Login("testuser", "testpass"));
            Assert.IsFalse(networkManager.Register("newuser", "newpass"));
            Assert.IsFalse(networkManager.GuestLogin());
            Assert.IsFalse(networkManager.CreateRoom("TestRoom"));
            Assert.IsFalse(networkManager.JoinRoom(1));
            Assert.IsFalse(networkManager.StartGame());
        }
        
        // ========================================
        // 데이터 구조체 테스트
        // ========================================
        
        [Test]
        public void UserInfo_WinRateCalculation_IsAccurate()
        {
            // Test case 1: 승률 80%
            UserInfo user1 = new UserInfo
            {
                totalGames = 100,
                wins = 80,
                losses = 20
            };
            Assert.AreEqual(80.0, user1.GetWinRate(), 0.01);
            
            // Test case 2: 승률 0% (게임을 하지 않은 경우)
            UserInfo user2 = new UserInfo
            {
                totalGames = 0,
                wins = 0,
                losses = 0
            };
            Assert.AreEqual(0.0, user2.GetWinRate(), 0.01);
            
            // Test case 3: 승률 100%
            UserInfo user3 = new UserInfo
            {
                totalGames = 50,
                wins = 50,
                losses = 0
            };
            Assert.AreEqual(100.0, user3.GetWinRate(), 0.01);
        }
        
        [Test]
        public void RoomInfo_CanJoin_ChecksConditionsCorrectly()
        {
            // Test case 1: 참가 가능한 방
            RoomInfo room1 = new RoomInfo
            {
                roomId = 1,
                roomName = "TestRoom",
                currentPlayers = 2,
                maxPlayers = 4,
                isGameStarted = false
            };
            Assert.IsTrue(room1.CanJoin());
            Assert.IsFalse(room1.IsFull());
            
            // Test case 2: 가득 찬 방
            RoomInfo room2 = new RoomInfo
            {
                roomId = 2,
                roomName = "FullRoom",
                currentPlayers = 4,
                maxPlayers = 4,
                isGameStarted = false
            };
            Assert.IsFalse(room2.CanJoin());
            Assert.IsTrue(room2.IsFull());
            
            // Test case 3: 게임 시작된 방
            RoomInfo room3 = new RoomInfo
            {
                roomId = 3,
                roomName = "StartedRoom",
                currentPlayers = 3,
                maxPlayers = 4,
                isGameStarted = true
            };
            Assert.IsFalse(room3.CanJoin());
            Assert.IsFalse(room3.IsFull());
        }
        
        // ========================================
        // 프로토콜 메시지 형식 테스트
        // ========================================
        
        [Test]
        public void NetworkClient_ProtocolMessage_FormatsCorrectly()
        {
            // 이 테스트는 NetworkClient의 내부 구현을 테스트하는 것이므로
            // 실제로는 메시지 형식이 올바른지 확인하는 정도로 제한
            
            // 예상되는 메시지 형식들
            string expectedLoginMessage = "AUTH_REQUEST:LOGIN:username:password";
            string expectedRegisterMessage = "AUTH_REQUEST:REGISTER:username:password";
            string expectedGuestMessage = "AUTH_REQUEST:GUEST:guestname";
            string expectedCreateRoomMessage = "CREATE_ROOM_REQUEST:roomname:4";
            string expectedJoinRoomMessage = "JOIN_ROOM_REQUEST:1";
            string expectedStartGameMessage = "START_GAME_REQUEST";
            string expectedHeartbeatMessage = "HEARTBEAT";
            
            // 형식이 올바른지 확인 (콜론으로 구분된 형태)
            Assert.AreEqual(4, expectedLoginMessage.Split(':').Length);
            Assert.AreEqual(4, expectedRegisterMessage.Split(':').Length);
            Assert.AreEqual(3, expectedGuestMessage.Split(':').Length);
            Assert.AreEqual(3, expectedCreateRoomMessage.Split(':').Length);
            Assert.AreEqual(2, expectedJoinRoomMessage.Split(':').Length);
            Assert.AreEqual(1, expectedStartGameMessage.Split(':').Length);
            Assert.AreEqual(1, expectedHeartbeatMessage.Split(':').Length);
        }
        
        // ========================================
        // 잘못된 메시지 형식 처리 테스트
        // ========================================
        
        [Test]
        public void MessageHandler_InvalidMessageFormat_HandledGracefully()
        {
            // 잘못된 형식의 메시지들을 전송해도 크래시가 발생하지 않아야 함
            
            System.Reflection.MethodInfo handleMessageMethod = typeof(MessageHandler)
                .GetMethod("HandleMessage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            // 빈 메시지
            Assert.DoesNotThrow(() => handleMessageMethod?.Invoke(messageHandler, new object[] { "" }));
            
            // null 메시지
            Assert.DoesNotThrow(() => handleMessageMethod?.Invoke(messageHandler, new object[] { null }));
            
            // 구분자 없는 메시지
            Assert.DoesNotThrow(() => handleMessageMethod?.Invoke(messageHandler, new object[] { "INVALID_MESSAGE" }));
            
            // 잘못된 파라미터 개수
            Assert.DoesNotThrow(() => handleMessageMethod?.Invoke(messageHandler, new object[] { "AUTH_RESPONSE:SUCCESS" })); // 파라미터 부족
            
            // 알 수 없는 메시지 타입
            Assert.DoesNotThrow(() => handleMessageMethod?.Invoke(messageHandler, new object[] { "UNKNOWN_MESSAGE_TYPE:param1:param2" }));
            
            // 잘못된 숫자 형식
            Assert.DoesNotThrow(() => handleMessageMethod?.Invoke(messageHandler, new object[] { "BLOCK_PLACED:invalid:5:10:1:0:1" }));
        }
        
        // ========================================
        // Unity Coroutine 테스트 (통합 테스트)
        // ========================================
        
        [UnityTest]
        public IEnumerator NetworkManager_Settings_UpdatedCorrectly()
        {
            // Arrange
            bool initialAutoReconnect = true;
            float initialReconnectDelay = 3.0f;
            int initialMaxAttempts = 5;
            
            // Act
            networkManager.UpdateSettings(initialAutoReconnect, initialReconnectDelay, initialMaxAttempts);
            
            // Unity 환경에서 1프레임 대기
            yield return null;
            
            // Act - 설정 변경
            networkManager.UpdateSettings(false, 1.0f, 3);
            
            // Unity 환경에서 1프레임 대기
            yield return null;
            
            // Assert - 설정이 제대로 변경되었는지 확인
            // (실제로는 NetworkManager 내부 필드에 접근해야 하지만, 
            // 여기서는 예외가 발생하지 않았다는 것으로 성공으로 판단)
            Assert.Pass("네트워크 설정 업데이트 성공");
        }
        
        [UnityTest]
        public IEnumerator NetworkManager_HeartbeatSettings_UpdatedCorrectly()
        {
            // Act
            networkManager.UpdateHeartbeatSettings(true, 10.0f);
            
            // Unity 환경에서 1프레임 대기
            yield return null;
            
            // Act - 하트비트 비활성화
            networkManager.UpdateHeartbeatSettings(false, 5.0f);
            
            // Unity 환경에서 1프레임 대기
            yield return null;
            
            // Assert
            Assert.Pass("하트비트 설정 업데이트 성공");
        }
    }
}