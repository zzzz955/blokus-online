using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BlokusUnity.Common;

namespace BlokusUnity.Game
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Button))]
    [RequireComponent(typeof(Image))]
    public sealed class BlockButton : MonoBehaviour
    {
        [Header("Label (optional)")]
        [SerializeField] private TMP_Text label;
        [SerializeField] private bool showLabel = false;   // ← 기본적으로 비표시

        [Header("Palette Preview")]
        [SerializeField] private float previewCellSize = 28f;  // 셀 픽셀 크기(전 블록 공통)
        [SerializeField] private bool useSpriteThumbnails = false; // ← 기본 off(그리드로 그림)

        [Header("Skin (optional)")]
        [SerializeField] private BlockSkin skin; // 없으면 기본 색

        public BlockType Type { get; private set; }
        public PlayerColor Player { get; private set; }

        private Button _btn;
        private Image _img;
        private BlockPalette _owner;
        
        // 선택 상태 표시를 위한 필드들
        private bool _isSelected = false;
        private Color _originalBackgroundColor;
        private readonly Color _selectedColor = new Color(1f, 1f, 0.7f, 0.8f); // 연한 레몬색 (투명도 80%)

        // 1x1 white sprite (UI Image는 Source Sprite가 있어야 색이 보임)
        private static Sprite sWhiteSprite;
        private static Sprite White1x1()
        {
            if (sWhiteSprite != null) return sWhiteSprite;
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, Color.white); t.Apply();
            sWhiteSprite = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
            return sWhiteSprite;
        }

        // 플레이어 컬러 (GameBoard와 동일한 색상 사용)
        private Color PlayerTint(PlayerColor p)
        {
            // Debug.Log($"[BlockButton] PlayerTint 호출 - Player: {p}");
            
            if (skin != null)
            {
                Color skinColor = skin.GetTint(p);
                // Debug.Log($"[BlockButton] 스킨 색상 사용: {skinColor}");
                return skinColor;
            }
            
            // Unity 기본 색상 직접 사용 (GameBoard와 동일)
            Color finalColor = p switch
            {
                PlayerColor.Blue => Color.blue,      // (0, 0, 1, 1) - 밝은 파란색
                PlayerColor.Yellow => Color.yellow,  // (1, 1, 0, 1) - 밝은 노란색
                PlayerColor.Red => Color.red,        // (1, 0, 0, 1) - 밝은 빨간색
                PlayerColor.Green => Color.green,    // (0, 1, 0, 1) - 밝은 초록색
                _ => Color.white
            };
            
            // Debug.Log($"[BlockButton] 기본 색상 사용: {finalColor}");
            return finalColor;
        }

        private void Awake()
        {
            _btn = GetComponent<Button>();
            _img = GetComponent<Image>();

            _btn.onClick.RemoveAllListeners();
            _btn.onClick.AddListener(OnClick);

            // 라벨 없으면 자동 생성(하지만 기본은 숨김)
            if (label == null)
            {
                var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
                var rt = (RectTransform)go.transform;
                rt.SetParent(transform, false);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = new Vector2(8, 8);
                rt.offsetMax = new Vector2(-8, -8);
                label = go.GetComponent<TextMeshProUGUI>();
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = 12;
                label.color = Color.black;
                label.raycastTarget = false;
            }
        }

        public void Init(BlockPalette owner, BlockType type, PlayerColor player, Color baseColor, string title = null)
        {
            _owner = owner;
            Type = type;
            Player = player;
            
            // Debug.Log($"[BlockButton] Init - Type: {type}, Player: {player}, PlayerTint: {PlayerTint(player)}");

            // 버튼 루트 이미지: 클릭 영역 + 투명 배경
            if (_img != null)
            {
                _img.sprite = White1x1();
                _img.type = Image.Type.Simple;
                _img.preserveAspect = true;
                _img.raycastTarget = true;
                _originalBackgroundColor = new Color(1, 1, 1, 0); // 투명 배경 색상 저장
                _img.color = _originalBackgroundColor;
            }

            // 라벨 표시 여부
            if (label != null)
            {
                label.gameObject.SetActive(showLabel);
                if (showLabel) label.text = string.IsNullOrEmpty(title) ? ShortName(type) : title;
            }

            CreateBlockVisualization(type, player);

            // 고정 버튼 크기(레이아웃 기준)
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(160f, 160f);
        }

        private void CreateBlockVisualization(BlockType blockType, PlayerColor player)
        {
            // 기존 시각화 전부 제거
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var ch = transform.GetChild(i);
                if (ch != null && ch.name == "BlockVisual")
                    Destroy(ch.gameObject);
            }

            // (옵션) PNG 썸네일 모드 — 기본은 off
            if (useSpriteThumbnails && TryGetSpriteFor(blockType, out var spr) && spr != null)
            {
                // 자식으로 1장만 붙임 (여전히 cellSize에 맞춰서 컨테이너 크기 설정)
                var baseShape = BlokusUnity.Common.Block.GetBaseShape(blockType);
                int minR = int.MaxValue, maxR = int.MinValue, minC = int.MaxValue, maxC = int.MinValue;
                foreach (var p in baseShape)
                {
                    minR = Mathf.Min(minR, p.row); maxR = Mathf.Max(maxR, p.row);
                    minC = Mathf.Min(minC, p.col); maxC = Mathf.Max(maxC, p.col);
                }
                int bw = maxC - minC + 1;
                int bh = maxR - minR + 1;

                var go = new GameObject("BlockVisual", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;

                // 컨테이너 크기를 셀 수 × previewCellSize로 — 셀 크기 유지
                rt.sizeDelta = new Vector2(bw * previewCellSize, bh * previewCellSize);

                var img = go.GetComponent<Image>();
                img.sprite = spr;
                img.type = Image.Type.Simple;
                img.preserveAspect = true;          // 비율 유지
                
                Color spriteColor = PlayerTint(player);
                img.color = spriteColor;    // 흰 PNG면 플레이어 색으로 틴트
                img.raycastTarget = false;
                
                // Debug.Log($"[BlockButton] PNG 썸네일 색상 설정 - 블록색상: {spriteColor}");

                return;
            }

            // 기본: 셀-그리기 (셀 크기 동일)
            var positions = BlokusUnity.Common.Block.GetBaseShape(blockType);
            if (positions == null || positions.Count == 0) return;

            var visualContainer = new GameObject("BlockVisual", typeof(RectTransform));
            visualContainer.transform.SetParent(transform, false);
            var containerRect = visualContainer.GetComponent<RectTransform>();
            containerRect.anchorMin = containerRect.anchorMax = new Vector2(0.5f, 0.5f);
            containerRect.anchoredPosition = Vector2.zero;

            // 경계/크기
            int minRow = int.MaxValue, maxRow = int.MinValue, minCol = int.MaxValue, maxCol = int.MinValue;
            foreach (var p in positions)
            {
                minRow = Mathf.Min(minRow, p.row); maxRow = Mathf.Max(maxRow, p.row);
                minCol = Mathf.Min(minCol, p.col); maxCol = Mathf.Max(maxCol, p.col);
            }
            int blockWidth = maxCol - minCol + 1;
            int blockHeight = maxRow - minRow + 1;

            containerRect.sizeDelta = new Vector2(blockWidth * previewCellSize, blockHeight * previewCellSize);

            Color blockColor = PlayerTint(player);
            // Debug.Log($"[BlockButton] CreateBlockVisualization - Player: {player}, FinalColor: {blockColor}");

            foreach (var pos in positions)
            {
                // 먼저 테두리를 생성 (뒤에 렌더링됨)
                var borderObj = new GameObject($"Border_{pos.row}_{pos.col}", typeof(RectTransform), typeof(Image));
                borderObj.transform.SetParent(visualContainer.transform, false);
                
                var borderRect = borderObj.GetComponent<RectTransform>();
                borderRect.sizeDelta = new Vector2(previewCellSize + 2f, previewCellSize + 2f);
                borderRect.anchoredPosition = new Vector2(
                    (pos.col - minCol - blockWidth * 0.5f + 0.5f) * previewCellSize,
                    (blockHeight * 0.5f - 0.5f - (pos.row - minRow)) * previewCellSize
                );

                var borderImage = borderObj.GetComponent<Image>();
                borderImage.sprite = White1x1();
                borderImage.type = Image.Type.Simple;
                borderImage.color = Color.black; // 검정색 테두리
                borderImage.raycastTarget = false;

                // 그 다음 셀을 생성 (앞에 렌더링되어 테두리 위에 표시됨)
                var cellObj = new GameObject($"Cell_{pos.row}_{pos.col}", typeof(RectTransform), typeof(Image));
                cellObj.transform.SetParent(visualContainer.transform, false);

                var cellRect = cellObj.GetComponent<RectTransform>();
                cellRect.sizeDelta = new Vector2(previewCellSize, previewCellSize);
                cellRect.anchoredPosition = new Vector2(
                    (pos.col - minCol - blockWidth * 0.5f + 0.5f) * previewCellSize,
                    (blockHeight * 0.5f - 0.5f - (pos.row - minRow)) * previewCellSize
                );

                var cellImage = cellObj.GetComponent<Image>();
                cellImage.sprite = White1x1();
                cellImage.type = Image.Type.Simple;
                cellImage.color = blockColor; // 블록 색상이 정상적으로 보임
                cellImage.raycastTarget = false;
            }
        }

        // ---- 리소스 매핑 (PNG 썸네일을 쓰고 싶을 때만 사용됨)
        // enum -> 파일명(키) 고정 매핑 (네 프로젝트 enum에 맞춰 조정)
        private static readonly Dictionary<BlockType, string> SpriteNameByType = new()
        {
            { BlockType.Single,   "MONO"    },
            { BlockType.Domino,   "DUO"     },
            { BlockType.TrioLine, "TRIO_I"  },
            { BlockType.TrioAngle,"TRIO_L"  },
            { BlockType.Tetro_I,  "TETRO_I" },
            { BlockType.Tetro_O,  "TETRO_O" },
            { BlockType.Tetro_L,  "TETRO_L" },
            { BlockType.Tetro_T,  "TETRO_T" },
            { BlockType.Tetro_S,  "TETRO_Z" }, // 의도적으로 Z 사용

            { BlockType.Pento_F,  "PENTO_F" },
            { BlockType.Pento_I,  "PENTO_I" },
            { BlockType.Pento_L,  "PENTO_L" },
            { BlockType.Pento_P,  "PENTO_P" },
            { BlockType.Pento_N,  "PENTO_N" },
            { BlockType.Pento_T,  "PENTO_T" },
            { BlockType.Pento_U,  "PENTO_U" },
            { BlockType.Pento_V,  "PENTO_V" },
            { BlockType.Pento_W,  "PENTO_W" },
            { BlockType.Pento_X,  "PENTO_X" },
            { BlockType.Pento_Y,  "PENTO_Y" },
            { BlockType.Pento_Z,  "PENTO_Z" },
        };

        private static bool TryGetSpriteFor(BlockType type, out Sprite spr)
        {
            spr = null;
            if (!useStaticIndexBuilt) BuildSpriteIndex();
            string fixedName = SpriteNameByType.TryGetValue(type, out var fn) ? fn : null;
            if (string.IsNullOrEmpty(fixedName)) return false;

            // 가장 확실한 경로부터
            string[] candidates =
            {
                $"Blocks/sprite_{fixedName}",
                $"Blocks/{fixedName}",
                $"Blocks/sprite_{type}",
            };
            foreach (var path in candidates)
            {
                var s = Resources.Load<Sprite>(path);
                if (s != null) { spr = s; return true; }
            }
            // 인덱스 보조(안 잡혀도 미진단용으로만 사용)
            var key = Canon($"sprite_{fixedName}");
            if (sSpriteIndex.TryGetValue(key, out spr)) return true;
            return false;
        }

        // 인덱스(느슨 매칭)는 useSpriteThumbnails 진단용
        private static Dictionary<string, Sprite> sSpriteIndex;
        private static bool useStaticIndexBuilt = false;
        private static string Canon(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            s = s.Trim();
            if (s.StartsWith("sprite_", StringComparison.OrdinalIgnoreCase)) s = s.Substring(7);
            var arr = s.Where(char.IsLetterOrDigit).ToArray();
            return new string(arr).ToUpperInvariant();
        }
        private static void BuildSpriteIndex()
        {
            sSpriteIndex = new Dictionary<string, Sprite>();
            var all = Resources.LoadAll<Sprite>("Blocks");
            foreach (var spr in all)
            {
                var k = Canon(spr.name);
                if (!string.IsNullOrEmpty(k)) sSpriteIndex[k] = spr;
            }
            useStaticIndexBuilt = true;
            // Debug.Log("[BlockButton] Loaded sprites: " + (sSpriteIndex?.Count ?? 0));
        }

        public void SetInteractable(bool on)
        {
            if (_btn != null) _btn.interactable = on;
            if (_img != null && !_isSelected) // 선택 상태가 아닐 때만 투명도 조절
            {
                var c = _img.color; c.a = on ? (_originalBackgroundColor.a) : 0.45f; _img.color = c;
            }
        }
        
        /// <summary>
        /// 블록 선택 상태를 설정합니다.
        /// </summary>
        /// <param name="selected">선택 상태 여부</param>
        public void SetSelected(bool selected)
        {
            if (_img == null) return;
            
            _isSelected = selected;
            
            if (selected)
            {
                // 선택 상태: 연한 레몬색 배경
                _img.color = _selectedColor;
                // Debug.Log($"[BlockButton] 블록 {Type} 선택됨 - 배경색: {_selectedColor}");
            }
            else
            {
                // 선택 해제: 원래 투명 배경으로 복원
                _img.color = _originalBackgroundColor;
                // Debug.Log($"[BlockButton] 블록 {Type} 선택 해제됨 - 배경색: {_originalBackgroundColor}");
            }
        }

        private void OnClick()
        {
            if (_owner != null) _owner.NotifyButtonClicked(Type, Player);
        }

        private static string ShortName(BlockType t)
            => t.ToString()
               .Replace("BlockType.", "")
               .Replace("Pento_", "P_")
               .Replace("Tetro_", "T_")
               .Replace("Trio", "Tr_");
    }
}
