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

        private void OnValidate()
        {
            // Content 설정 자동 보정 (개발 편의)
            if (blockContainer != null)
            {
                var hl = blockContainer.GetComponent<HorizontalLayoutGroup>();
                if (hl == null) hl = blockContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
                hl.spacing = 16;
                hl.childAlignment = TextAnchor.UpperLeft;
                hl.childControlWidth = true;
                hl.childControlHeight = true;
                hl.childForceExpandWidth = false;
                hl.childForceExpandHeight = false;

                var fitter = blockContainer.GetComponent<ContentSizeFitter>();
                if (fitter == null) fitter = blockContainer.gameObject.AddComponent<ContentSizeFitter>();
                fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.verticalFit   = ContentSizeFitter.FitMode.Unconstrained;
            }
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

            Debug.Log($"[BlockPalette] 초기화 완료: {blocks.Count}개 블록, Player={player}");
        }

        public void MarkBlockAsUsed(BlockType type)
        {
            if (_buttons.TryGetValue(type, out var btn))
                btn.SetInteractable(false);

            if (_selectedType.HasValue && _selectedType.Value.Equals(type))
            {
                _selectedType = null;
                _selectedBlock = null;
            }
        }

        internal void NotifyButtonClicked(BlockType type, PlayerColor player)
        {
            OnBlockButtonClicked(type);
        }

        public void OnBlockButtonClicked(BlockType blockType)
        {
            _selectedType = blockType;
            _selectedBlock = new Block(blockType, _player);
            Debug.Log($"[BlockPalette] Select: {blockType}");
            OnBlockSelected?.Invoke(_selectedBlock);
        }

        public void RotateSelectedBlock(bool clockwise)
        {
            if (_selectedBlock == null) return;
            if (clockwise) _selectedBlock.RotateClockwise();
            else           _selectedBlock.RotateCounterClockwise();
            OnBlockSelected?.Invoke(_selectedBlock);
        }

        public void FlipSelectedBlock(bool vertical)
        {
            if (_selectedBlock == null) return;
            if (vertical) _selectedBlock.FlipVertical();
            else          _selectedBlock.FlipHorizontal();
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
                if (go.GetComponent<Button>() == null)      go.AddComponent<Button>();
                if (go.GetComponent<Image>() == null)       go.AddComponent<Image>();
            }
            else
            {
                go = new GameObject($"Block_{type}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(BlockButton));
                var rt = (RectTransform)go.transform;
                rt.SetParent(blockContainer, false);
                rt.sizeDelta = new Vector2(160f, 160f);

                var img = go.GetComponent<Image>();
                img.color = Color.white;

                var btn = go.GetComponent<Button>();
                btn.transition = Selectable.Transition.ColorTint;
            }

            var bb = go.GetComponent<BlockButton>();
            bb.Init(this, type, player, Color.white, type.ToString());

            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.preferredWidth  = 160f;
            le.preferredHeight = 160f;
            le.flexibleWidth   = 0f;
            le.flexibleHeight  = 0f;

            return bb;
        }
    }
}
