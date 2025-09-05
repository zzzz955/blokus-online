using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Shared.Models;

namespace Features.Multi.UI
{
    /// <summary>
    /// 랭킹 목록 아이템 UI (Stub 구현)
    /// 로비에서 플레이어 랭킹을 표시하는 아이템
    /// </summary>
    public class RankingItemUI : MonoBehaviour
    {
        [Header("UI 컴포넌트")]
        [SerializeField] private TextMeshProUGUI rankText;
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI winRateText;
        [SerializeField] private Image backgroundImage;
        
        [Header("랭킹 색상")]
        [SerializeField] private Color rank1Color = Color.yellow;
        [SerializeField] private Color rank2Color = Color.white;
        [SerializeField] private Color rank3Color = new Color(1f, 0.5f, 0f); // 브론즈
        [SerializeField] private Color normalColor = Color.gray;
        
        private UserInfo userData;
        private int rankPosition;
        
        /// <summary>
        /// 랭킹 데이터 설정
        /// </summary>
        public void SetupRanking(UserInfo user, int rank)
        {
            userData = user;
            rankPosition = rank;
            UpdateUI();
        }
        
        /// <summary>
        /// UI 업데이트
        /// </summary>
        private void UpdateUI()
        {
            if (userData == null) return;
            
            // 순위 표시
            if (rankText != null)
                rankText.text = $"{rankPosition}";
            
            // 플레이어 이름 (멀티플레이어에서는 displayName만 사용)
            if (playerNameText != null)
                playerNameText.text = userData.display_name ?? "Unknown";
            
            // 점수 표시 (Stub - 임시로 GetWinRate() * 10으로 계산)
            if (scoreText != null)
            {
                int score = userData.totalGames > 0 ? Mathf.RoundToInt((float)userData.GetWinRate() * 10) : 0;
                scoreText.text = $"{score}";
            }
            
            // 승률 표시
            if (winRateText != null)
            {
                if (userData.totalGames > 0)
                {
                    double winRate = userData.GetWinRate();
                    winRateText.text = $"{winRate:F1}%";
                }
                else
                {
                    winRateText.text = "N/A";
                }
            }
            
            // 랭킹에 따른 배경 색상 설정
            if (backgroundImage != null)
            {
                Color bgColor = normalColor;
                switch (rankPosition)
                {
                    case 1: bgColor = rank1Color; break;
                    case 2: bgColor = rank2Color; break;
                    case 3: bgColor = rank3Color; break;
                    default: bgColor = normalColor; break;
                }
                backgroundImage.color = bgColor;
            }
        }
        
        /// <summary>
        /// 랭킹 위치 업데이트
        /// </summary>
        public void UpdateRank(int newRank)
        {
            rankPosition = newRank;
            UpdateUI();
        }
    }
}