using System.Collections.Generic;
using UnityEngine;
using BlokusUnity.Common;

namespace BlokusUnity.Application.Stages
{
    /// <summary>
    /// 캔디크러시 사가 스타일의 곡선 스테이지 경로 생성기
    /// 한 줄에 하나씩 스테이지를 배치하고 베지어 곡선으로 연결
    /// </summary>
    public class StageFeed : MonoBehaviour
    {
        [Header("레이아웃 설정")]
        [SerializeField] private float stageVerticalSpacing = 180f; // 간격 더 넓게
        [SerializeField] private float maxHorizontalOffset = 300f; // 좌우 범위도 더 넓게
        [SerializeField] private int totalStages = 100;
        [SerializeField] private AnimationCurve horizontalPattern; // 에디터에서 패턴 조정 가능
        
        
        // 캐시된 경로 데이터
        private Dictionary<int, Vector2> stagePositions = new Dictionary<int, Vector2>();
        private List<Vector2> pathPoints = new List<Vector2>();
        
        // 이벤트
        public System.Action OnPathGenerated;
        
        void Awake()
        {
            GeneratePath();
        }
        
        /// <summary>
        /// 뱀 모양 경로 생성
        /// </summary>
        public void GeneratePath()
        {
            stagePositions.Clear();
            pathPoints.Clear();
            
            // 지그재그 패턴으로 스테이지 위치 계산
            for (int stage = 1; stage <= totalStages; stage++)
            {
                Vector2 position = CalculateStagePosition(stage);
                stagePositions[stage] = position;
                pathPoints.Add(position);
            }
            
            OnPathGenerated?.Invoke();
            
        }
        
        /// <summary>
        /// 특정 스테이지의 월드 좌표 계산 (캔디크러시 사가 스타일)
        /// </summary>
        private Vector2 CalculateStagePosition(int stageNumber)
        {
            // Y 좌표: 한 줄에 하나씩, 위에서 아래로
            float y = -(stageNumber - 1) * stageVerticalSpacing;
            
            // X 좌표: 곡선 경로 패턴
            float x = CalculateXPosition(stageNumber);
            
            return new Vector2(x, y);
        }
        
        /// <summary>
        /// X 위치 계산 - 캔디크러시 사가 스타일 곡선 패턴
        /// </summary>
        private float CalculateXPosition(int stageNumber)
        {
            // Animation Curve가 설정되어 있으면 사용
            if (horizontalPattern != null && horizontalPattern.length > 0)
            {
                float curveInput = (stageNumber - 1) / 100f; // 0~1 범위로 정규화
                return horizontalPattern.Evaluate(curveInput) * maxHorizontalOffset;
            }
            
            // 기본 패턴: 사인파 + 무작위성
            float normalizedStage = (stageNumber - 1) / 10f; // 주기 조정
            
            // 기본 사인파 패턴
            float sinePattern = Mathf.Sin(normalizedStage * 0.8f) * maxHorizontalOffset * 0.7f;
            
            // 약간의 무작위성 추가 (시드 기반으로 일관성 유지)
            System.Random random = new System.Random(stageNumber * 1337);
            float randomOffset = ((float)random.NextDouble() - 0.5f) * maxHorizontalOffset * 0.5f;
            
            // 가끔 큰 변화 주기
            if (stageNumber % 7 == 0)
            {
                randomOffset *= 1.5f;
            }
            
            return sinePattern + randomOffset;
        }
        
        
        // ========================================
        // Public API
        // ========================================
        
        /// <summary>
        /// 특정 스테이지의 위치 반환
        /// </summary>
        public Vector2 GetStagePosition(int stageNumber)
        {
            if (stagePositions.ContainsKey(stageNumber))
            {
                return stagePositions[stageNumber];
            }
            
            Debug.LogWarning($"스테이지 {stageNumber}의 위치를 찾을 수 없습니다!");
            return Vector2.zero;
        }
        
        /// <summary>
        /// 전체 경로 포인트들 반환
        /// </summary>
        public List<Vector2> GetPathPoints()
        {
            return new List<Vector2>(pathPoints);
        }
        
        /// <summary>
        /// 특정 스테이지가 유효한지 확인
        /// </summary>
        public bool IsValidStage(int stageNumber)
        {
            return stageNumber >= 1 && stageNumber <= totalStages;
        }
        
        /// <summary>
        /// 총 스테이지 수 반환
        /// </summary>
        public int GetTotalStages()
        {
            return totalStages;
        }
        
        /// <summary>
        /// 특정 스테이지가 속한 행 계산 (캔디크러시 스타일에서는 스테이지 번호와 동일)
        /// </summary>
        public int GetStageRow(int stageNumber)
        {
            if (!IsValidStage(stageNumber)) return -1;
            
            return stageNumber; // 한 줄에 하나씩
        }
        
        /// <summary>
        /// 경로 전체 높이 계산 (스크롤뷰 Content 크기 설정용)
        /// </summary>
        public float GetTotalHeight()
        {
            return (totalStages - 1) * stageVerticalSpacing + 200f; // 여유 공간 추가
        }
        
        /// <summary>
        /// 경로 전체 너비 계산
        /// </summary>
        public float GetTotalWidth()
        {
            return maxHorizontalOffset * 2f + 200f; // 좌우 최대 범위 + 여유 공간
        }
        
        // ========================================
        // 에디터용 기능들
        // ========================================
        
        #if UNITY_EDITOR
        /// <summary>
        /// 에디터에서 경로 시각화
        /// </summary>
        void OnDrawGizmos()
        {
            if (stagePositions == null || stagePositions.Count == 0) return;
            
            Gizmos.color = Color.blue;
            
            // 스테이지 위치들 그리기
            foreach (var kvp in stagePositions)
            {
                Vector3 worldPos = transform.TransformPoint(new Vector3(kvp.Value.x, kvp.Value.y, 0));
                Gizmos.DrawWireCube(worldPos, Vector3.one * 50f);
                
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(worldPos, kvp.Key.ToString());
                #endif
            }
            
            // 경로 연결선 그리기
            Gizmos.color = Color.green;
            for (int i = 0; i < pathPoints.Count - 1; i++)
            {
                Vector3 from = transform.TransformPoint(new Vector3(pathPoints[i].x, pathPoints[i].y, 0));
                Vector3 to = transform.TransformPoint(new Vector3(pathPoints[i + 1].x, pathPoints[i + 1].y, 0));
                Gizmos.DrawLine(from, to);
            }
        }
        
        [ContextMenu("Regenerate Path")]
        public void RegeneratePath()
        {
            GeneratePath();
        }
        #endif
    }
}