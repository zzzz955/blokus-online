using System.Collections.Generic;
using UnityEngine;

namespace BlokusUnity.Application.Stages
{
    /// <summary>
    /// 캔디크러시 사가 스타일의 뱀 모양 스테이지 경로 생성기
    /// 지그재그 패턴으로 스테이지들을 배치하고 경로를 관리
    /// </summary>
    public class StageFeed : MonoBehaviour
    {
        [Header("레이아웃 설정")]
        [SerializeField] private int stagesPerRow = 5;
        [SerializeField] private float stageSpacing = 120f;
        [SerializeField] private float rowSpacing = 150f;
        [SerializeField] private int totalStages = 100;
        
        [Header("경로 설정")]
        [SerializeField] private bool drawConnectionLines = true;
        [SerializeField] private LineRenderer connectionLinePrefab;
        [SerializeField] private Transform connectionLinesParent;
        
        // 캐시된 경로 데이터
        private Dictionary<int, Vector2> stagePositions = new Dictionary<int, Vector2>();
        private List<Vector2> pathPoints = new List<Vector2>();
        private List<LineRenderer> connectionLines = new List<LineRenderer>();
        
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
            ClearPath();
            
            stagePositions.Clear();
            pathPoints.Clear();
            
            // 지그재그 패턴으로 스테이지 위치 계산
            for (int stage = 1; stage <= totalStages; stage++)
            {
                Vector2 position = CalculateStagePosition(stage);
                stagePositions[stage] = position;
                pathPoints.Add(position);
            }
            
            // 연결선 생성
            if (drawConnectionLines && connectionLinePrefab != null)
            {
                CreateConnectionLines();
            }
            
            OnPathGenerated?.Invoke();
            
            Debug.Log($"스테이지 경로 생성 완료: {totalStages}개 스테이지");
        }
        
        /// <summary>
        /// 특정 스테이지의 월드 좌표 계산
        /// </summary>
        private Vector2 CalculateStagePosition(int stageNumber)
        {
            // 0-based 인덱스로 변환
            int index = stageNumber - 1;
            
            // 몇 번째 행인지 계산
            int rowIndex = index / stagesPerRow;
            
            // 행 내에서의 위치 계산
            int posInRow = index % stagesPerRow;
            
            // 짝수 행은 왼쪽→오른쪽, 홀수 행은 오른쪽→왼쪽
            bool isEvenRow = (rowIndex % 2 == 0);
            
            float x, y;
            
            if (isEvenRow)
            {
                // 왼쪽에서 오른쪽으로 (0, 1, 2, 3, 4)
                x = posInRow * stageSpacing;
            }
            else
            {
                // 오른쪽에서 왼쪽으로 (4, 3, 2, 1, 0)
                x = (stagesPerRow - 1 - posInRow) * stageSpacing;
            }
            
            // Y 좌표는 위에서 아래로 (음수 방향)
            y = -rowIndex * rowSpacing;
            
            return new Vector2(x, y);
        }
        
        /// <summary>
        /// 스테이지 간 연결선 생성
        /// </summary>
        private void CreateConnectionLines()
        {
            if (connectionLinesParent == null)
            {
                connectionLinesParent = transform;
            }
            
            for (int stage = 1; stage < totalStages; stage++)
            {
                if (stagePositions.ContainsKey(stage) && stagePositions.ContainsKey(stage + 1))
                {
                    CreateConnectionLine(stagePositions[stage], stagePositions[stage + 1]);
                }
            }
        }
        
        /// <summary>
        /// 개별 연결선 생성
        /// </summary>
        private void CreateConnectionLine(Vector2 from, Vector2 to)
        {
            if (connectionLinePrefab == null) return;
            
            GameObject lineObj = Instantiate(connectionLinePrefab.gameObject, connectionLinesParent);
            LineRenderer line = lineObj.GetComponent<LineRenderer>();
            
            if (line != null)
            {
                line.positionCount = 2;
                line.useWorldSpace = false;
                line.SetPosition(0, new Vector3(from.x, from.y, 0));
                line.SetPosition(1, new Vector3(to.x, to.y, 0));
                
                connectionLines.Add(line);
            }
        }
        
        /// <summary>
        /// 기존 경로 데이터 정리
        /// </summary>
        private void ClearPath()
        {
            // 기존 연결선들 제거
            foreach (var line in connectionLines)
            {
                if (line != null)
                {
                    DestroyImmediate(line.gameObject);
                }
            }
            connectionLines.Clear();
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
        /// 특정 스테이지가 속한 행 계산
        /// </summary>
        public int GetStageRow(int stageNumber)
        {
            if (!IsValidStage(stageNumber)) return -1;
            
            return (stageNumber - 1) / stagesPerRow;
        }
        
        /// <summary>
        /// 경로 전체 높이 계산 (스크롤뷰 Content 크기 설정용)
        /// </summary>
        public float GetTotalHeight()
        {
            int totalRows = Mathf.CeilToInt((float)totalStages / stagesPerRow);
            return (totalRows - 1) * rowSpacing + 100f; // 여유 공간 추가
        }
        
        /// <summary>
        /// 경로 전체 너비 계산
        /// </summary>
        public float GetTotalWidth()
        {
            return (stagesPerRow - 1) * stageSpacing + 100f; // 여유 공간 추가
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