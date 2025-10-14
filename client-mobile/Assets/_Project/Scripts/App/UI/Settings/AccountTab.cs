using UnityEngine;
using TMPro;
using App.Core;

namespace App.UI.Settings
{
    /// <summary>
    /// Account 정보 탭 (읽기 전용)
    /// SessionManager에서 사용자 정보를 가져와 표시
    /// </summary>
    public class AccountTab : MonoBehaviour
    {
        [Header("계정 정보 표시")]
        [SerializeField] private TMP_Text displayNameText;
        [SerializeField] private TMP_Text userIdText;
        [SerializeField] private TMP_Text usernameText;

        [Header("로그인 정보 (옵션)")]
        [SerializeField] private TMP_Text loginTimeText;
        [SerializeField] private TMP_Text sessionStatusText;

        [Header("라벨 (옵션)")]
        [SerializeField] private string displayNameLabel = "Display Name";
        [SerializeField] private string userIdLabel = "User ID";
        [SerializeField] private string usernameLabel = "Username";
        [SerializeField] private string loginTimeLabel = "Login Time";
        [SerializeField] private string sessionStatusLabel = "Session Status";

        [Header("디버그")]
        [SerializeField] private bool debugMode = true;

        void OnEnable()
        {
            // SessionManager 존재 확인
            if (SessionManager.Instance == null)
            {
                Debug.LogError("[AccountTab] SessionManager.Instance가 null입니다!");
                ShowNotLoggedIn();
                return;
            }

            // UI 초기화
            RefreshAccountInfo();
        }

        /// <summary>
        /// 계정 정보 새로고침
        /// </summary>
        public void RefreshAccountInfo()
        {
            if (SessionManager.Instance == null)
            {
                ShowNotLoggedIn();
                return;
            }

            // 로그인 상태 확인
            if (!SessionManager.Instance.IsLoggedIn)
            {
                ShowNotLoggedIn();
                return;
            }

            // DisplayName 표시
            if (displayNameText != null)
            {
                string displayName = SessionManager.Instance.DisplayName;
                displayNameText.text = string.IsNullOrEmpty(displayName)
                    ? $"{displayNameLabel}: -"
                    : $"{displayNameLabel}: {displayName}";
            }

            // UserID 표시 (난독화)
            if (userIdText != null)
            {
                int userId = SessionManager.Instance.UserId;
                if (userId > 0)
                {
                    string obfuscatedId = ObfuscateUserId(userId);
                    userIdText.text = $"{userIdLabel}: {obfuscatedId}";
                }
                else
                {
                    userIdText.text = $"{userIdLabel}: -";
                }
            }

            // Username (CachedId) 표시
            if (usernameText != null)
            {
                string username = SessionManager.Instance.CachedId;
                usernameText.text = string.IsNullOrEmpty(username)
                    ? $"{usernameLabel}: -"
                    : $"{usernameLabel}: {username}";
            }

            // 로그인 시간 표시 (PlayerPrefs에서 가져옴)
            if (loginTimeText != null)
            {
                string loginTime = GetLoginTimeString();
                loginTimeText.text = $"{loginTimeLabel}: {loginTime}";
            }

            // 세션 상태 표시
            if (sessionStatusText != null)
            {
                string status = SessionManager.Instance.IsLoggedIn ? "Active" : "Inactive";
                sessionStatusText.text = $"{sessionStatusLabel}: {status}";
            }

            if (debugMode)
            {
                Debug.Log("[AccountTab] 계정 정보 표시 완료");
            }
        }

        /// <summary>
        /// 로그인하지 않은 상태 표시
        /// </summary>
        private void ShowNotLoggedIn()
        {
            if (displayNameText != null)
            {
                displayNameText.text = $"{displayNameLabel}: Not Logged In";
            }

            if (userIdText != null)
            {
                userIdText.text = $"{userIdLabel}: -";
            }

            if (usernameText != null)
            {
                usernameText.text = $"{usernameLabel}: -";
            }

            if (loginTimeText != null)
            {
                loginTimeText.text = $"{loginTimeLabel}: -";
            }

            if (sessionStatusText != null)
            {
                sessionStatusText.text = $"{sessionStatusLabel}: Inactive";
            }

            if (debugMode)
            {
                Debug.LogWarning("[AccountTab] 로그인하지 않은 상태");
            }
        }

        /// <summary>
        /// 로그인 시간 문자열 반환 (PlayerPrefs에서 가져옴)
        /// </summary>
        private string GetLoginTimeString()
        {
            // SessionManager의 저장 키 사용
            string savedTimeStr = "";

#if UNITY_EDITOR
            savedTimeStr = UnityEditor.EditorPrefs.GetString("BlokusEditor_blokus_saved_at", "");
#else
            savedTimeStr = PlayerPrefs.GetString("blokus_saved_at", "");
#endif

            if (string.IsNullOrEmpty(savedTimeStr))
            {
                return "-";
            }

            // Binary 시간 파싱
            if (long.TryParse(savedTimeStr, out long timeBinary))
            {
                try
                {
                    System.DateTime loginTime = System.DateTime.FromBinary(timeBinary);
                    System.TimeSpan elapsed = System.DateTime.Now - loginTime;

                    // 경과 시간 표시
                    if (elapsed.TotalDays >= 1)
                    {
                        return $"{Mathf.FloorToInt((float)elapsed.TotalDays)}d ago";
                    }
                    else if (elapsed.TotalHours >= 1)
                    {
                        return $"{Mathf.FloorToInt((float)elapsed.TotalHours)}h ago";
                    }
                    else if (elapsed.TotalMinutes >= 1)
                    {
                        return $"{Mathf.FloorToInt((float)elapsed.TotalMinutes)}m ago";
                    }
                    else
                    {
                        return "Just now";
                    }
                }
                catch (System.Exception ex)
                {
                    if (debugMode)
                    {
                        Debug.LogError($"[AccountTab] 로그인 시간 파싱 오류: {ex.Message}");
                    }
                    return "-";
                }
            }

            return "-";
        }

        /// <summary>
        /// 계정 정보 복사 (클립보드) - 옵션
        /// </summary>
        public void CopyDisplayName()
        {
            if (SessionManager.Instance != null && !string.IsNullOrEmpty(SessionManager.Instance.DisplayName))
            {
                GUIUtility.systemCopyBuffer = SessionManager.Instance.DisplayName;

                if (debugMode)
                {
                    Debug.Log($"[AccountTab] DisplayName 복사됨: {SessionManager.Instance.DisplayName}");
                }
            }
        }

        /// <summary>
        /// UserID 복사 (클립보드) - 난독화된 ID 복사
        /// </summary>
        public void CopyUserId()
        {
            if (SessionManager.Instance != null && SessionManager.Instance.UserId > 0)
            {
                string obfuscatedId = ObfuscateUserId(SessionManager.Instance.UserId);
                GUIUtility.systemCopyBuffer = obfuscatedId;

                if (debugMode)
                {
                    Debug.Log($"[AccountTab] 난독화된 UserID 복사됨: {obfuscatedId}");
                }
            }
        }

        /// <summary>
        /// UserID 난독화 (MD5 해시 기반)
        /// 원본 ID를 숨기면서 고유한 식별자 생성
        /// </summary>
        private string ObfuscateUserId(int userId)
        {
            // MD5 해시 생성 (복호화 불가능)
            string input = $"blokus_{userId}_v1";
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));

                // 해시의 앞 8자리를 16진수로 변환
                return System.BitConverter.ToString(hash, 0, 4).Replace("-", "").ToUpper();
            }
        }
    }
}
