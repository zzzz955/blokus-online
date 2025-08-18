using System.Collections.Generic;
using UnityEngine;
using BlokusUnity.UI;

namespace BlokusUnity.Application.Stages
{
    /// <summary>
    /// 스테이지 버튼 오브젝트 풀링 시스템
    /// 기존 StageButton 컴포넌트를 재사용하여 성능 최적화
    /// </summary>
    public class StageButtonPool : MonoBehaviour
    {
        [Header("풀링 설정")]
        [SerializeField] private GameObject stageButtonPrefab; // StageButton 컴포넌트가 있는 프리팹
        [SerializeField] private int initialPoolSize = 20;
        [SerializeField] private int maxPoolSize = 100;
        [SerializeField] private Transform poolParent;
        
        // 오브젝트 풀
        private Queue<StageButton> availableButtons = new Queue<StageButton>();
        private HashSet<StageButton> usedButtons = new HashSet<StageButton>();
        
        // 싱글톤
        public static StageButtonPool Instance { get; private set; }
        
        void Awake()
        {
            // 싱글톤 설정
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
            
            // 풀 부모 설정
            if (poolParent == null)
            {
                poolParent = transform;
            }
            
            // 초기 풀 생성
            CreateInitialPool();
        }
        
        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
        
        /// <summary>
        /// 초기 오브젝트 풀 생성
        /// </summary>
        private void CreateInitialPool()
        {
            if (stageButtonPrefab == null)
            {
                Debug.LogError("StageButtonPrefab이 설정되지 않았습니다!");
                return;
            }
            
            for (int i = 0; i < initialPoolSize; i++)
            {
                CreateNewButton();
            }
            
            Debug.Log($"StageButtonPool 초기화 완료: {initialPoolSize}개 버튼 생성");
        }
        
        /// <summary>
        /// 새 버튼 생성 및 풀에 추가
        /// </summary>
        private StageButton CreateNewButton()
        {
            GameObject buttonObj = Instantiate(stageButtonPrefab, poolParent);
            StageButton button = buttonObj.GetComponent<StageButton>();
            
            if (button == null)
            {
                Debug.LogError("StageButtonPrefab에 StageButton 컴포넌트가 없습니다!");
                DestroyImmediate(buttonObj);
                return null;
            }
            
            // 초기 상태로 비활성화
            buttonObj.SetActive(false);
            
            // 풀에 추가
            availableButtons.Enqueue(button);
            
            return button;
        }
        
        /// <summary>
        /// 풀에서 버튼 가져오기
        /// </summary>
        public StageButton GetButton()
        {
            StageButton button = null;
            
            // 사용 가능한 버튼이 있으면 가져오기
            if (availableButtons.Count > 0)
            {
                button = availableButtons.Dequeue();
            }
            // 없으면 새로 생성 (최대 개수 제한)
            else if (GetTotalButtonCount() < maxPoolSize)
            {
                button = CreateNewButton();
                if (button != null)
                {
                    // 방금 큐에 넣었으니 다시 빼기
                    availableButtons.Dequeue();
                }
            }
            else
            {
                Debug.LogWarning("StageButtonPool 최대 크기에 도달했습니다!");
                return null;
            }
            
            if (button != null)
            {
                // 사용 중 리스트에 추가
                usedButtons.Add(button);
                button.gameObject.SetActive(true);
            }
            
            return button;
        }
        
        /// <summary>
        /// 버튼을 풀에 반환
        /// </summary>
        public void ReturnButton(StageButton button)
        {
            if (button == null) return;
            
            // 사용 중 리스트에서 제거
            if (usedButtons.Remove(button))
            {
                // 풀로 반환
                button.gameObject.SetActive(false);
                availableButtons.Enqueue(button);
            }
            else
            {
                Debug.LogWarning("반환하려는 버튼이 사용 중 리스트에 없습니다!");
            }
        }
        
        /// <summary>
        /// 사용 중인 모든 버튼 반환
        /// </summary>
        public void ReturnAllButtons()
        {
            var buttonsToReturn = new List<StageButton>(usedButtons);
            
            foreach (var button in buttonsToReturn)
            {
                ReturnButton(button);
            }
            
            Debug.Log($"{buttonsToReturn.Count}개 버튼을 풀에 반환했습니다.");
        }
        
        /// <summary>
        /// 특정 범위의 버튼들을 일괄 가져오기
        /// </summary>
        public List<StageButton> GetButtons(int count)
        {
            var buttons = new List<StageButton>();
            
            for (int i = 0; i < count; i++)
            {
                StageButton button = GetButton();
                if (button != null)
                {
                    buttons.Add(button);
                }
                else
                {
                    break; // 더 이상 버튼을 생성할 수 없음
                }
            }
            
            return buttons;
        }
        
        /// <summary>
        /// 여러 버튼을 일괄 반환
        /// </summary>
        public void ReturnButtons(List<StageButton> buttons)
        {
            foreach (var button in buttons)
            {
                ReturnButton(button);
            }
        }
        
        // ========================================
        // 정보 조회 함수들
        // ========================================
        
        /// <summary>
        /// 사용 가능한 버튼 개수
        /// </summary>
        public int GetAvailableButtonCount()
        {
            return availableButtons.Count;
        }
        
        /// <summary>
        /// 사용 중인 버튼 개수
        /// </summary>
        public int GetUsedButtonCount()
        {
            return usedButtons.Count;
        }
        
        /// <summary>
        /// 전체 버튼 개수
        /// </summary>
        public int GetTotalButtonCount()
        {
            return availableButtons.Count + usedButtons.Count;
        }
        
        /// <summary>
        /// 풀 상태 정보
        /// </summary>
        public string GetPoolStatus()
        {
            return $"풀 상태 - 사용가능: {GetAvailableButtonCount()}, " +
                   $"사용중: {GetUsedButtonCount()}, " +
                   $"총합: {GetTotalButtonCount()}/{maxPoolSize}";
        }
        
        /// <summary>
        /// 특정 스테이지 번호에 해당하는 사용 중인 버튼 찾기
        /// </summary>
        public StageButton FindUsedButton(int stageNumber)
        {
            foreach (var button in usedButtons)
            {
                if (button != null && button.StageNumber == stageNumber)
                {
                    return button;
                }
            }
            return null;
        }
        
        /// <summary>
        /// 현재 사용 중인 모든 버튼 리스트 반환
        /// </summary>
        public List<StageButton> GetUsedButtons()
        {
            return new List<StageButton>(usedButtons);
        }
        
        // ========================================
        // 디버그용 함수들
        // ========================================
        
        #if UNITY_EDITOR
        [ContextMenu("풀 상태 출력")]
        public void LogPoolStatus()
        {
            Debug.Log(GetPoolStatus());
        }
        #endif
    }
}