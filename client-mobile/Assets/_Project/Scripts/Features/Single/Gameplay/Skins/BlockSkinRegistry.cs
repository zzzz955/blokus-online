using System.Collections.Generic;
using UnityEngine;
using App.UI;
using Shared.Models;
namespace Features.Single.Gameplay.Skins{
    /// <summary>
    /// Block skin registry manager
    /// Migration Plan: DB에서 상수값(enum) 수신 → BlockSkinId 매핑 → 내부 리소스 할당(텍스처/머티리얼/프리팹 경로)
    /// 리소스 로드: 우선 Resources 또는 사전 캐시
    /// </summary>
    public class BlockSkinRegistry : MonoBehaviour
    {
        [Header("Default Skin")]
        [SerializeField] private BlockSkin defaultSkin;

        [Header("Available Skins")]
        [SerializeField] private BlockSkin[] availableSkins;

        // 싱글톤 인스턴스
        public static BlockSkinRegistry Instance { get; private set; }

        // 스킨 캐시 (BlockSkinId -> BlockSkin)
        private Dictionary<BlockSkinId, BlockSkin> skinCache = new Dictionary<BlockSkinId, BlockSkin>();

        // 초기화 상태
        private bool isInitialized = false;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                InitializeRegistry();
            }
            else
            {
                Debug.LogWarning("[BlockSkinRegistry] Duplicate instance destroyed");
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Registry 초기화
        /// </summary>
        private void InitializeRegistry()
        {
            // 기본 스킨 검증
            if (defaultSkin == null)
            {
                Debug.LogError("[BlockSkinRegistry] Default skin is not assigned!");
                return;
            }

            // 기본 스킨을 캐시에 추가
            skinCache[BlockSkinId.Default] = defaultSkin;

            // 사용 가능한 스킨들을 캐시에 추가
            if (availableSkins != null)
            {
                foreach (var skin in availableSkins)
                {
                    if (skin != null && skin.IsValid())
                    {
                        skinCache[skin.skinId] = skin;
                        Debug.Log($"[BlockSkinRegistry] Registered skin: {skin.skinId} ({skin.GetDisplayName()})");
                    }
                    else if (skin != null)
                    {
                        Debug.LogWarning($"[BlockSkinRegistry] Invalid skin detected: {skin.skinId}");
                    }
                }
            }

            // Resources 폴더에서 추가 스킨 로드
            LoadSkinsFromResources();

            isInitialized = true;
            Debug.Log($"[BlockSkinRegistry] Initialized with {skinCache.Count} skins");
        }

        /// <summary>
        /// Resources 폴더에서 BlockSkin 에셋 로드
        /// </summary>
        private void LoadSkinsFromResources()
        {
            try
            {
                // Resources/Skins 폴더에서 모든 BlockSkin 에셋 로드
                BlockSkin[] resourceSkins = Resources.LoadAll<BlockSkin>("Skins");
                
                foreach (var skin in resourceSkins)
                {
                    if (skin != null && skin.IsValid() && !skinCache.ContainsKey(skin.skinId))
                    {
                        skinCache[skin.skinId] = skin;
                        Debug.Log($"[BlockSkinRegistry] Loaded from Resources: {skin.skinId}");
                    }
                }

                Debug.Log($"[BlockSkinRegistry] Loaded {resourceSkins.Length} skins from Resources");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[BlockSkinRegistry] Failed to load skins from Resources: {ex.Message}");
            }
        }

        /// <summary>
        /// 스킨 ID로 스킨 가져오기
        /// Migration Plan: 잘못된 enum 수신 시 디폴트 스킨 + 오류 토스트
        /// </summary>
        public BlockSkin Get(BlockSkinId skinId)
        {
            if (!isInitialized)
            {
                Debug.LogWarning("[BlockSkinRegistry] Registry not initialized, returning default skin");
                return defaultSkin;
            }

            // 캐시에서 스킨 찾기
            if (skinCache.TryGetValue(skinId, out BlockSkin skin) && skin != null && skin.IsValid())
            {
                return skin;
            }

            // 스킨을 찾을 수 없는 경우 오류 처리
            Debug.LogWarning($"[BlockSkinRegistry] Skin not found or invalid: {skinId}, falling back to default");
            
            // 오류 토스트 표시 (Migration Plan 요구사항)
            if (SystemMessageManager.Instance != null)
            {
                SystemMessageManager.ShowToast($"스킨 '{skinId}'를 찾을 수 없어 기본 스킨을 사용합니다.");
            }

            return defaultSkin;
        }

        /// <summary>
        /// 사용 가능한 모든 스킨 ID 목록 반환
        /// </summary>
        public BlockSkinId[] GetAvailableSkinIds()
        {
            if (!isInitialized)
                return new BlockSkinId[] { BlockSkinId.Default };

            var availableIds = new List<BlockSkinId>();
            foreach (var kvp in skinCache)
            {
                if (kvp.Value != null && kvp.Value.IsValid())
                {
                    availableIds.Add(kvp.Key);
                }
            }

            return availableIds.ToArray();
        }

        /// <summary>
        /// 사용 가능한 모든 스킨 반환
        /// </summary>
        public BlockSkin[] GetAvailableSkins()
        {
            if (!isInitialized)
                return new BlockSkin[] { defaultSkin };

            var availableSkinsResult = new List<BlockSkin>();
            foreach (var kvp in skinCache)
            {
                if (kvp.Value != null && kvp.Value.IsValid())
                {
                    availableSkinsResult.Add(kvp.Value);
                }
            }

            return availableSkinsResult.ToArray();
        }

        /// <summary>
        /// 스킨이 사용 가능한지 확인
        /// </summary>
        public bool IsAvailable(BlockSkinId skinId)
        {
            return isInitialized && 
                   skinCache.TryGetValue(skinId, out BlockSkin skin) && 
                   skin != null && 
                   skin.IsValid();
        }

        /// <summary>
        /// 기본 스킨 반환
        /// </summary>
        public BlockSkin GetDefaultSkin()
        {
            return defaultSkin;
        }

        /// <summary>
        /// 스킨 동적 등록 (런타임 추가)
        /// </summary>
        public void RegisterSkin(BlockSkin skin)
        {
            if (skin == null || !skin.IsValid())
            {
                Debug.LogWarning("[BlockSkinRegistry] Cannot register invalid skin");
                return;
            }

            skinCache[skin.skinId] = skin;
            Debug.Log($"[BlockSkinRegistry] Dynamically registered skin: {skin.skinId}");
        }

        /// <summary>
        /// 스킨 등록 해제
        /// </summary>
        public void UnregisterSkin(BlockSkinId skinId)
        {
            if (skinId == BlockSkinId.Default)
            {
                Debug.LogWarning("[BlockSkinRegistry] Cannot unregister default skin");
                return;
            }

            if (skinCache.ContainsKey(skinId))
            {
                skinCache.Remove(skinId);
                Debug.Log($"[BlockSkinRegistry] Unregistered skin: {skinId}");
            }
        }

        /// <summary>
        /// 레지스트리 상태 정보 반환
        /// </summary>
        public string GetStatusInfo()
        {
            if (!isInitialized)
                return "Not initialized";

            int validSkins = 0;
            int invalidSkins = 0;

            foreach (var kvp in skinCache)
            {
                if (kvp.Value != null && kvp.Value.IsValid())
                    validSkins++;
                else
                    invalidSkins++;
            }

            return $"Initialized: {isInitialized}, Valid skins: {validSkins}, Invalid skins: {invalidSkins}";
        }

        /// <summary>
        /// Phase 2 준비: Addressables 지원 추가 예정
        /// </summary>
        private void PrepareAddressablesSupport()
        {
            // Phase 2에서 Addressables를 통한 동적 스킨 로딩 구현 예정
            // addressableKey를 통한 비동기 로딩 및 캐싱
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터에서 스킨 검증
        /// </summary>
        [ContextMenu("Validate All Skins")]
        private void ValidateAllSkins()
        {
            Debug.Log("[BlockSkinRegistry] === Skin Validation ===");
            
            if (defaultSkin == null)
            {
                Debug.LogError("Default skin is missing!");
            }
            else if (!defaultSkin.IsValid())
            {
                Debug.LogError($"Default skin is invalid: {defaultSkin.name}");
            }
            else
            {
                Debug.Log($"✓ Default skin is valid: {defaultSkin.GetDisplayName()}");
            }

            if (availableSkins != null)
            {
                for (int i = 0; i < availableSkins.Length; i++)
                {
                    var skin = availableSkins[i];
                    if (skin == null)
                    {
                        Debug.LogWarning($"Available skin at index {i} is null");
                    }
                    else if (!skin.IsValid())
                    {
                        Debug.LogWarning($"Available skin at index {i} is invalid: {skin.name}");
                    }
                    else
                    {
                        Debug.Log($"✓ Available skin {i} is valid: {skin.GetDisplayName()} ({skin.skinId})");
                    }
                }
            }
            else
            {
                Debug.LogWarning("Available skins array is null");
            }

            Debug.Log("[BlockSkinRegistry] === Validation Complete ===");
        }
#endif
    }
}