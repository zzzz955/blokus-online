using UnityEngine;
using System.Collections.Generic;
using Shared.Models;

namespace Features.Multi.Core
{
    /// <summary>
    /// 멀티플레이 전용 사용자 데이터 캐시
    /// 서버로부터 받은 멀티플레이 관련 데이터를 캐싱
    /// </summary>
    public class MultiUserDataCache : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool debugMode = true;

        // 싱글톤
        public static MultiUserDataCache Instance { get; private set; }

        // 사용자 데이터
        private UserInfo myUserInfo;
        private List<UserInfo> onlineUsers = new List<UserInfo>();
        private List<UserInfo> rankingData = new List<UserInfo>();
        private List<RoomInfo> roomList = new List<RoomInfo>();

        // 초기화 상태
        private bool isInitialized = false;
        private bool isDataSynced = false;

        // 이벤트
        public event System.Action OnUserDataUpdated;
        public event System.Action OnOnlineUsersUpdated;
        public event System.Action OnRankingUpdated;
        public event System.Action OnRoomListUpdated;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (debugMode)
                    Debug.Log("[MultiUserDataCache] Instance created");
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                if (debugMode)
                    Debug.Log("[MultiUserDataCache] Instance destroyed");
            }
        }

        // ========================================
        // Initialization
        // ========================================

        public void Initialize()
        {
            if (debugMode)
                Debug.Log("[MultiUserDataCache] Initializing...");

            ClearAllData();
            isInitialized = true;
            isDataSynced = false;

            if (debugMode)
                Debug.Log("[MultiUserDataCache] Initialization complete");
        }

        public void Cleanup()
        {
            if (debugMode)
                Debug.Log("[MultiUserDataCache] Cleaning up...");

            ClearAllData();
            isInitialized = false;
            isDataSynced = false;
        }

        private void ClearAllData()
        {
            myUserInfo = new UserInfo();
            onlineUsers.Clear();
            rankingData.Clear();
            roomList.Clear();
        }

        // ========================================
        // Data Management
        // ========================================

        public void UpdateMyUserInfo(UserInfo userInfo)
        {
            myUserInfo = userInfo;
            isDataSynced = true;

            if (debugMode)
                Debug.Log($"[MultiUserDataCache] My user info updated: {userInfo.username}");

            OnUserDataUpdated?.Invoke();
        }

        public void UpdateOnlineUsers(List<UserInfo> users)
        {
            onlineUsers.Clear();
            onlineUsers.AddRange(users);

            if (debugMode)
                Debug.Log($"[MultiUserDataCache] Online users updated: {users.Count} users");

            OnOnlineUsersUpdated?.Invoke();
        }

        public void UpdateRanking(List<UserInfo> ranking)
        {
            rankingData.Clear();
            rankingData.AddRange(ranking);

            if (debugMode)
                Debug.Log($"[MultiUserDataCache] Ranking data updated: {ranking.Count} entries");

            OnRankingUpdated?.Invoke();
        }

        public void UpdateRoomList(List<RoomInfo> rooms)
        {
            roomList.Clear();
            roomList.AddRange(rooms);

            if (debugMode)
                Debug.Log($"[MultiUserDataCache] Room list updated: {rooms.Count} rooms");

            OnRoomListUpdated?.Invoke();
        }

        // ========================================
        // Data Access
        // ========================================

        public UserInfo GetMyUserInfo()
        {
            return myUserInfo;
        }

        public List<UserInfo> GetOnlineUsers()
        {
            return new List<UserInfo>(onlineUsers);
        }

        public List<UserInfo> GetRankingData()
        {
            return new List<UserInfo>(rankingData);
        }

        public List<RoomInfo> GetRoomList()
        {
            return new List<RoomInfo>(roomList);
        }

        // ========================================
        // State Management
        // ========================================

        public bool IsInitialized
        {
            get { return isInitialized; }
        }

        public bool IsDataSynced
        {
            get { return isDataSynced; }
        }

        public string GetCurrentUserId()
        {
            return myUserInfo?.username ?? "";
        }

        public bool IsLoggedIn()
        {
            return !string.IsNullOrEmpty(myUserInfo?.username);
        }
    }
}