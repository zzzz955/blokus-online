using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Shared.Models;
using Features.Single.Gameplay;

namespace Features.Multi.UI
{
    /// <summary>
    /// 멀티플레이어 블록 팔레트 - Single 버전 기반으로 멀티플레이어 기능 추가
    /// 턴 기반 상호작용, 사용 블록 추적, 서버 동기화
    /// </summary>
    public class MyBlockPalette : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private RectTransform blockContainer; // ScrollView/Viewport/Content
        [SerializeField] private GameObject blockButtonPrefab; // 선택: 없으면 코드로 기본 생성

        [Header("Cell Sprite System")]
        [SerializeField] private CellSpriteProvider cellSpriteProvider; // 셀 스프라이트 제공자

        // 멀티플레이어 전용
        [Header("Multiplayer Settings")]
        [SerializeField] private PlayerColor myPlayerColor = PlayerColor.Blue;
        [SerializeField] private bool isInteractable = false; // 턴 기반 상호작용

        public event Action<Block> OnBlockSelected;

        private readonly Dictionary<BlockType, BlockButton> _buttons = new();
        private PlayerColor _player = PlayerColor.Blue;

        private BlockType? _selectedType;
        private Block _selectedBlock;
        private BlockButton _currentSelectedButton; // 현재 선택된 버튼 참조

        // 멀티플레이어 상태
        private bool isMyTurn = false;
        private readonly HashSet<BlockType> usedBlocks = new(); // 사용된 블록 추적

        private void Awake()
        {
            if (blockContainer == null)
            {
                Debug.LogError("[MultiBlockPalette] blockContainer(Content)가 연결되지 않았습니다.");
                return;
            }

            // 이미 GridLayoutGroup이 있으면, 아무 레이아웃도 추가/수정하지 않는다.
            var grid = blockContainer.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                Debug.Log("[MultiBlockPalette] GridLayoutGroup 감지됨 → 자동 레이아웃 보정 생략");
                return;
            }

            // Grid가 없을 때만, 기존 방식(Horizontal + Fitter) 보정 수행
            var hl = blockContainer.GetComponent<HorizontalLayoutGroup>();
            if (hl == null) hl = blockContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.UpperLeft;
            hl.spacing = 8f;
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;

            var fitter = blockContainer.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = blockContainer.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        /// <summary>
        /// 멀티플레이어 팔레트 초기화
        /// </summary>
        public void InitializePalette(PlayerColor player)
        {
            _player = player;
            myPlayerColor = player;
            Debug.Log($"[MultiBlockPalette] InitializePalette - Player: {player}, CellSpriteProvider: {cellSpriteProvider != null}");
            
            Clear();
            
            // 모든 21개 블록 타입으로 초기화
            List<BlockType> allBlockTypes = Shared.Models.BlockFactory.GetAllBlockTypes();
            
            foreach (var type in allBlockTypes)
            {
                var btn = CreateButton(type, player);
                _buttons[type] = btn;
            }

            // 초기 프레임 레이아웃 타이밍 이슈 방지
            LayoutRebuilder.ForceRebuildLayoutImmediate(blockContainer);

            Debug.Log($"[MultiBlockPalette] 초기화 완료: {allBlockTypes.Count}개 블록, Player={player}");
        }

        /// <summary>
        /// 블록을 사용됨으로 표시 (서버에서 확정된 후 호출)
        /// </summary>
        public void MarkBlockAsUsed(BlockType type)
        {
            if (_buttons.TryGetValue(type, out var btn) && btn != null)
            {
                // 완전히 숨기기 (딕셔너리에서는 제거하지 않음)
                btn.gameObject.SetActive(false);
                usedBlocks.Add(type);

                // 레이아웃 갱신
                if (blockContainer != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(blockContainer);
            }

            // 선택된 블록이 사용된 경우 선택 상태 해제
            if (_selectedType.HasValue && _selectedType.Value.Equals(type))
            {
                ClearSelection();
                Debug.Log($"[MultiBlockPalette] 블록 {type} 사용됨 - 선택 상태 해제");
            }
        }

        /// <summary>
        /// 사용된 블록을 다시 사용 가능하게 복원 (Undo용)
        /// </summary>
        public void RestoreBlock(BlockType type)
        {
            if (_buttons.ContainsKey(type))
            {
                var btn = _buttons[type];
                btn.gameObject.SetActive(true);
                usedBlocks.Remove(type);
                
                // 복원된 블록이 이전에 선택된 상태였다면 선택 상태 유지
                if (_selectedType.HasValue && _selectedType.Value.Equals(type))
                {
                    btn.SetSelected(true);
                    _currentSelectedButton = btn;
                    Debug.Log($"[MultiBlockPalette] 복원된 블록 {type} 선택 상태 유지");
                }
                return;
            }

            // 프리팹으로 복원 (일반적으로는 발생하지 않음)
            Debug.LogWarning($"[MultiBlockPalette] 블록 {type}이 딕셔너리에 없어서 새로 생성");
            var blockButton = CreateButton(type, _player);
            _buttons[type] = blockButton;
            usedBlocks.Remove(type);

            LayoutRebuilder.ForceRebuildLayoutImmediate(blockContainer);
        }

        /// <summary>
        /// 블록 버튼 클릭 처리 (내부 호출)
        /// </summary>
        internal void NotifyButtonClicked(BlockType type, PlayerColor player)
        {
            OnBlockButtonClicked(type);
        }

        /// <summary>
        /// 블록 선택 처리 (내 턴에만 가능)
        /// </summary>
        public void OnBlockButtonClicked(BlockType blockType)
        {
            if (!isMyTurn || !isInteractable)
            {
                Debug.Log($"[MultiBlockPalette] 블록 선택 무시 - 내 턴이 아님 (isMyTurn: {isMyTurn}, isInteractable: {isInteractable})");
                return;
            }

            if (usedBlocks.Contains(blockType))
            {
                Debug.Log($"[MultiBlockPalette] 블록 선택 무시 - 이미 사용됨: {blockType}");
                return;
            }

            // 이전 선택 블록 해제
            if (_currentSelectedButton != null)
            {
                _currentSelectedButton.SetSelected(false);
            }
            
            // 새로운 블록 선택
            _selectedType = blockType;
            _selectedBlock = new Block(blockType, _player);
            
            // 선택된 버튼에 시각적 피드백 적용
            if (_buttons.TryGetValue(blockType, out var selectedButton))
            {
                selectedButton.SetSelected(true);
                _currentSelectedButton = selectedButton;
            }
            
            Debug.Log($"[MultiBlockPalette] 블록 선택: {blockType}");
            OnBlockSelected?.Invoke(_selectedBlock);
        }

        /// <summary>
        /// 선택 상태 해제
        /// </summary>
        public void ClearSelection()
        {
            if (_currentSelectedButton != null)
            {
                _currentSelectedButton.SetSelected(false);
                _currentSelectedButton = null;
            }
            _selectedType = null;
            _selectedBlock = null;
        }

        /// <summary>
        /// 선택된 블록 회전 (내 턴에만 가능)
        /// </summary>
        public void RotateSelectedBlock(bool clockwise)
        {
            if (!isMyTurn || _selectedBlock == null) return;
            
            if (clockwise) _selectedBlock.RotateClockwise();
            else _selectedBlock.RotateCounterClockwise();
            
            Debug.Log($"[MultiBlockPalette] 선택된 블록 {_selectedType} 회전 - {(clockwise ? "시계방향" : "반시계방향")}");
            OnBlockSelected?.Invoke(_selectedBlock);
        }

        /// <summary>
        /// 선택된 블록 뒤집기 (내 턴에만 가능)
        /// </summary>
        public void FlipSelectedBlock(bool vertical)
        {
            if (!isMyTurn || _selectedBlock == null) return;
            
            if (vertical) _selectedBlock.FlipVertical();
            else _selectedBlock.FlipHorizontal();
            
            Debug.Log($"[MultiBlockPalette] 선택된 블록 {_selectedType} 플립 - {(vertical ? "수직" : "수평")}");
            OnBlockSelected?.Invoke(_selectedBlock);
        }

        /// <summary>
        /// 사용 가능한 블록 리스트 반환 (게임 종료 조건 체크용)
        /// </summary>
        public List<BlockType> GetAvailableBlocks()
        {
            var availableBlocks = new List<BlockType>();
            foreach (var kvp in _buttons)
            {
                if (kvp.Value != null && kvp.Value.gameObject.activeInHierarchy && !usedBlocks.Contains(kvp.Key))
                {
                    availableBlocks.Add(kvp.Key);
                }
            }
            return availableBlocks;
        }

        /// <summary>
        /// 사용 가능한 블록이 있는지 확인 (게임 종료 조건 체크용)
        /// </summary>
        public bool HasAvailableBlocks()
        {
            foreach (var kvp in _buttons)
            {
                if (kvp.Value != null && kvp.Value.gameObject.activeInHierarchy && !usedBlocks.Contains(kvp.Key))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 사용 가능한 블록 개수 반환 (디버그용)
        /// </summary>
        public int GetAvailableBlockCount()
        {
            int count = 0;
            foreach (var kvp in _buttons)
            {
                if (kvp.Value != null && kvp.Value.gameObject.activeInHierarchy && !usedBlocks.Contains(kvp.Key))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 현재 선택된 블록 반환
        /// </summary>
        public Block GetSelectedBlock()
        {
            return _selectedBlock;
        }

        private void Clear()
        {
            // 선택 상태 초기화
            ClearSelection();
            
            _buttons.Clear();
            usedBlocks.Clear();

            if (blockContainer == null) return;
            for (int i = blockContainer.childCount - 1; i >= 0; i--)
                Destroy(blockContainer.GetChild(i).gameObject);
        }

        private BlockButton CreateButton(BlockType type, PlayerColor player)
        {
            GameObject go;
            if (blockButtonPrefab != null)
            {
                go = Instantiate(blockButtonPrefab, blockContainer);
                if (go.GetComponent<BlockButton>() == null) go.AddComponent<BlockButton>();
                if (go.GetComponent<Button>() == null) go.AddComponent<Button>();
                if (go.GetComponent<Image>() == null) go.AddComponent<Image>();
            }
            else
            {
                go = new GameObject($"Block_{type}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(BlockButton));
                var rt = (RectTransform)go.transform;
                rt.SetParent(blockContainer, false);
                rt.sizeDelta = new Vector2(160f, 160f);

                var img = go.GetComponent<Image>();
                img.sprite = Resources.FindObjectsOfTypeAll<Sprite>()?.Length > 0 ? img.sprite : null;
                img.color = Color.white;
                img.raycastTarget = true;

                var btn = go.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.transition = Selectable.Transition.ColorTint;
                btn.interactable = true;
            }

            var bb = go.GetComponent<BlockButton>();
            
            // CellSpriteProvider 설정 (있는 경우)
            if (cellSpriteProvider != null)
            {
                bb.SetCellSpriteProvider(cellSpriteProvider);
                Debug.Log($"[MultiBlockPalette] BlockButton({type})에 CellSpriteProvider 설정 완료");
            }
            else
            {
                Debug.LogWarning($"[MultiBlockPalette] cellSpriteProvider가 null - BlockButton({type})에 설정하지 못함");
            }
            
            // BlockButton 초기화 - Init() 대신 필요한 부분만 직접 호출
            try
            {
                // Type과 Player 속성을 reflection으로 설정
                var typeField = typeof(Features.Single.Gameplay.BlockButton).GetField("<Type>k__BackingField", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var playerField = typeof(Features.Single.Gameplay.BlockButton).GetField("<Player>k__BackingField", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (typeField != null) typeField.SetValue(bb, type);
                if (playerField != null) playerField.SetValue(bb, player);
                
                // BlockButton의 기본 이미지 설정 (Init에서 하는 것과 동일)
                var image = bb.GetComponent<Image>();
                if (image != null)
                {
                    // White1x1 스프라이트 생성 (BlockButton의 private static 메서드와 동일)
                    var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    texture.SetPixel(0, 0, Color.white);
                    texture.Apply();
                    var whiteSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
                    
                    image.sprite = whiteSprite;
                    image.type = Image.Type.Simple;
                    image.preserveAspect = true;
                    image.raycastTarget = true;
                    image.color = new Color(1, 1, 1, 0); // 투명 배경
                }
                
                // BlockButton의 CreateBlockVisualization 메서드 직접 호출 (private이므로 reflection 사용)
                var createVisualizationMethod = typeof(Features.Single.Gameplay.BlockButton).GetMethod("CreateBlockVisualization", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (createVisualizationMethod != null)
                {
                    createVisualizationMethod.Invoke(bb, new object[] { type, player });
                    Debug.Log($"[MyBlockPalette] BlockButton({type}) 시각화 생성 완료");
                }
                else
                {
                    Debug.LogWarning($"[MyBlockPalette] CreateBlockVisualization 메서드를 찾을 수 없음");
                }
                
                // 고정 버튼 크기 설정 (Init에서 하는 것과 동일)
                var rectTransform = bb.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.sizeDelta = new Vector2(160f, 160f);
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MyBlockPalette] BlockButton 시각화 생성 실패: {ex.Message}");
            }
            
            // 클릭 이벤트를 우리 메서드로 리디렉트
            var button = bb.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => NotifyButtonClicked(type, player));
            }

            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 160f;
            le.preferredHeight = 160f;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 0f;

            return bb;
        }

        // ========================================
        // Public API (멀티플레이어 전용)
        // ========================================

        /// <summary>
        /// 상호작용 설정 (턴 기반)
        /// </summary>
        public void SetInteractable(bool interactable)
        {
            isInteractable = interactable;
            Debug.Log($"[MultiBlockPalette] 상호작용 설정: {interactable}");
            
            // 모든 버튼의 상호작용 상태 업데이트
            foreach (var kvp in _buttons)
            {
                if (kvp.Value != null && kvp.Value.gameObject.activeInHierarchy)
                {
                    var button = kvp.Value.GetComponent<Button>();
                    if (button != null)
                    {
                        button.interactable = interactable && !usedBlocks.Contains(kvp.Key);
                    }
                }
            }
        }

        /// <summary>
        /// 내 턴 설정
        /// </summary>
        public void SetMyTurn(bool isMyTurn, PlayerColor myColor)
        {
            this.isMyTurn = isMyTurn;
            this.myPlayerColor = myColor;
            
            if (!isMyTurn)
            {
                ClearSelection(); // 내 턴이 아닐 때 선택 해제
            }
            
            Debug.Log($"[MultiBlockPalette] 턴 상태 변경: {(isMyTurn ? "내 턴" : "상대 턴")}, 내 색상: {myColor}");
        }

        /// <summary>
        /// 팔레트 리셋
        /// </summary>
        public void ResetPalette()
        {
            Debug.Log("[MultiBlockPalette] 팔레트 리셋");
            
            usedBlocks.Clear();
            
            // 모든 블록을 사용 가능한 상태로 복원
            foreach (var kvp in _buttons)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.gameObject.SetActive(true);
                }
            }
            
            ClearSelection();
            LayoutRebuilder.ForceRebuildLayoutImmediate(blockContainer);
        }

        /// <summary>
        /// 플레이어 색상 설정
        /// </summary>
        public void SetPlayerColor(PlayerColor color)
        {
            _player = color;
            myPlayerColor = color;
            
            Debug.Log($"[MultiBlockPalette] 플레이어 색상 변경: {color}");
            // 버튼들은 이미 생성 시 색상이 설정되므로 여기서는 재생성이 필요할 수 있음
            // 필요시 InitializePalette를 다시 호출하여 색상 업데이트
        }

        /// <summary>
        /// 블록 사용 상태를 서버 상태와 동기화
        /// </summary>
        public void SyncUsedBlocks(List<BlockType> serverUsedBlocks)
        {
            Debug.Log($"[MultiBlockPalette] 서버와 블록 사용 상태 동기화: {serverUsedBlocks.Count}개");
            
            usedBlocks.Clear();
            
            foreach (var blockType in serverUsedBlocks)
            {
                usedBlocks.Add(blockType);
                if (_buttons.TryGetValue(blockType, out var btn) && btn != null)
                {
                    btn.gameObject.SetActive(false);
                }
            }
            
            LayoutRebuilder.ForceRebuildLayoutImmediate(blockContainer);
        }
    }
}