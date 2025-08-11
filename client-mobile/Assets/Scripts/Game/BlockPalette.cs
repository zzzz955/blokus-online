using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BlokusUnity.Common;

namespace BlokusUnity.Game
{
    /// <summary>
    /// ScrollView Content에 블록 버튼들을 생성/관리.
    /// Content에는 HorizontalLayoutGroup + ContentSizeFitter(가로 Preferred)가 필요합니다.
    /// </summary>
    public sealed class BlockPalette : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private RectTransform blockContainer; // ScrollView/Viewport/Content
        [SerializeField] private GameObject blockButtonPrefab; // 선택: 없으면 코드로 기본 생성

        public event Action<Block> OnBlockSelected;

        private readonly Dictionary<BlockType, BlockButton> _buttons = new();
        private PlayerColor _player = PlayerColor.Blue;

        private BlockType? _selectedType;
        private Block _selectedBlock;

        private void Awake()
        {
            if (blockContainer == null)
            {
                Debug.LogError("[BlockPalette] blockContainer(Content)가 연결되지 않았습니다.");
                return;
            }

            // 이미 GridLayoutGroup이 있으면, 아무 레이아웃도 추가/수정하지 않는다.
            var grid = blockContainer.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                Debug.Log("[BlockPalette] GridLayoutGroup 감지됨 → 자동 레이아웃 보정 생략");
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

        public void InitializePalette(List<BlockType> blocks, PlayerColor player)
        {
            if (blockContainer == null)
            {
                Debug.LogError("[BlockPalette] blockContainer(Content)가 연결되지 않았습니다.");
                return;
            }

            _player = player;
            Clear();

            if (blocks == null || blocks.Count == 0)
            {
                Debug.LogWarning("[BlockPalette] 블록 리스트가 비어있습니다.");
                return;
            }

            foreach (var type in blocks)
            {
                var btn = CreateButton(type, player);
                _buttons[type] = btn;
            }

            // ★ 초기 프레임 레이아웃 타이밍 이슈 방지
            LayoutRebuilder.ForceRebuildLayoutImmediate(blockContainer); // 여기서 생성 완료 직후 리빌드 :contentReference[oaicite:7]{index=7}

            Debug.Log($"[BlockPalette] 초기화 완료: {blocks.Count}개 블록, Player={player}");
        }

        public void MarkBlockAsUsed(BlockType type)
        {
            if (_buttons.TryGetValue(type, out var btn) && btn != null)
            {
                // 완전히 숨기기 (딕셔너리에서는 제거하지 않음)
                btn.gameObject.SetActive(false);

                // 레이아웃 갱신
                if (blockContainer != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(blockContainer);
            }

            if (_selectedType.HasValue && _selectedType.Value.Equals(type))
            {
                _selectedType = null;
                _selectedBlock = null;
            }
        }

        /// <summary>
        /// 사용된 블록을 다시 사용 가능하게 복원 (Undo용)
        /// </summary>
        public void RestoreBlock(BlockType type)
        {
            if (_buttons.ContainsKey(type))
            {
                // 이미 딕셔너리에 있으면 비활성 → 활성만 처리하는 로직이 있다면 여기서 살리면 됨.
                var btn = _buttons[type];
                btn.gameObject.SetActive(true);
                return;
            }

            if (blockContainer == null)
            {
                Debug.LogError("[BlockPalette] blockContainer가 비어 있습니다.");
                return;
            }
            if (blockButtonPrefab == null)
            {
                Debug.LogError("[BlockPalette] blockButtonPrefab이 비어 있습니다. 프리팹을 반드시 연결하세요.");
                return;
            }

            // 프리팹 복원
            var go = Instantiate(blockButtonPrefab, blockContainer);
            var blockButton = go.GetComponent<BlockButton>();
            if (blockButton == null)
            {
                Debug.LogError("[BlockPalette] 프리팹에 BlockButton 컴포넌트가 없습니다.");
                Destroy(go);
                return;
            }

            // 정상 시각화 초기화
            blockButton.Init(this, type, _player, Color.white, type.ToString());

            // 정렬: enum 순으로 삽입 (초기 팔레트와 유사한 순서 보장)
            int insertIndex = blockContainer.childCount - 1;
            for (int i = 0; i < blockContainer.childCount - 1; i++)
            {
                var other = blockContainer.GetChild(i).GetComponent<BlockButton>();
                if (other == null) continue;
                if ((int)other.Type > (int)type)
                {
                    insertIndex = i;
                    break;
                }
            }
            go.transform.SetSiblingIndex(insertIndex);

            _buttons[type] = blockButton;

            LayoutRebuilder.ForceRebuildLayoutImmediate(blockContainer);
            Debug.Log($"[BlockPalette] 블록 복원됨: {type} (index={insertIndex})");
        }

        internal void NotifyButtonClicked(BlockType type, PlayerColor player)
        {
            OnBlockButtonClicked(type);
        }

        public void OnBlockButtonClicked(BlockType blockType)
        {
            _selectedType = blockType;
            _selectedBlock = new Block(blockType, _player);
            // Debug.Log($"[BlockPalette] Select: {blockType}");
            OnBlockSelected?.Invoke(_selectedBlock);
        }

        public void RotateSelectedBlock(bool clockwise)
        {
            if (_selectedBlock == null) return;
            if (clockwise) _selectedBlock.RotateClockwise();
            else _selectedBlock.RotateCounterClockwise();
            OnBlockSelected?.Invoke(_selectedBlock);
        }

        public void FlipSelectedBlock(bool vertical)
        {
            if (_selectedBlock == null) return;
            if (vertical) _selectedBlock.FlipVertical();
            else _selectedBlock.FlipHorizontal();
            OnBlockSelected?.Invoke(_selectedBlock);
        }

        private void Clear()
        {
            _buttons.Clear();
            _selectedType = null;
            _selectedBlock = null;

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
                img.sprite = Resources.FindObjectsOfTypeAll<Sprite>()?.Length > 0 ? img.sprite : null; // 생략 가능
                img.color = Color.white;
                img.raycastTarget = true;

                var btn = go.GetComponent<Button>();
                btn.targetGraphic = img; // targetGraphic 명시적 설정
                btn.transition = Selectable.Transition.ColorTint;
                btn.interactable = true; // 강제 활성화
            }

            var bb = go.GetComponent<BlockButton>();
            bb.Init(this, type, player, Color.white, type.ToString());

            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 160f;
            le.preferredHeight = 160f;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 0f;

            return bb;
        }
    }
}
