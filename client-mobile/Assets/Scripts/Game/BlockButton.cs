using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BlokusUnity.Common;

namespace BlokusUnity.Game
{
    /// <summary>
    /// 팔레트 내 개별 블록 버튼(UI). 클릭 시 BlockPalette로 전달.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Button))]
    [RequireComponent(typeof(Image))]
    public sealed class BlockButton : MonoBehaviour
    {
        [Header("Optional")]
        [SerializeField] private TMP_Text label; // 프리팹에 없으면 런타임에 생성

        public BlockType Type { get; private set; }
        public PlayerColor Player { get; private set; }

        private Button _btn;
        private Image _img;
        private BlockPalette _owner;

        private void Awake()
        {
            _btn = GetComponent<Button>();
            _img = GetComponent<Image>();

            _btn.onClick.RemoveAllListeners();
            _btn.onClick.AddListener(OnClick);

            // 라벨 없으면 자동 생성
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
                label.fontSize = 24;
                label.color = Color.black;
                label.raycastTarget = false;
            }
        }

        public void Init(BlockPalette owner, BlockType type, PlayerColor player, Color baseColor, string title = null)
        {
            _owner = owner;
            Type = type;
            Player = player;

            // 버튼 배경색 (플레이어 컬러로 식별감 강화)
            if (_img != null)
            {
                _img.raycastTarget = true;
                _img.color = GetPlayerTint(player);
            }

            if (label != null)
            {
                label.text = string.IsNullOrEmpty(title) ? ShortName(type) : title;
            }

            // 고정 사이즈 (레이아웃 기준)
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(160f, 160f);
        }

        public void SetInteractable(bool on)
        {
            if (_btn != null) _btn.interactable = on;
            if (_img != null)
            {
                var c = _img.color; c.a = on ? 1f : 0.45f; _img.color = c;
            }
        }

        private void OnClick()
        {
            Debug.Log($"[BlockButton] Click: {Type}");
            _owner?.NotifyButtonClicked(Type, Player);
        }

        private static string ShortName(BlockType t) => t.ToString().Replace("BlockType.", "").Replace("Pento_", "P_").Replace("Tetro_", "T_").Replace("Trio", "Tr_");

        private static Color GetPlayerTint(PlayerColor p)
        {
            return p switch
            {
                PlayerColor.Blue   => new Color(0.35f, 0.60f, 1.00f, 1f),
                PlayerColor.Yellow => new Color(1.00f, 0.85f, 0.30f, 1f),
                PlayerColor.Red    => new Color(1.00f, 0.40f, 0.40f, 1f),
                PlayerColor.Green  => new Color(0.40f, 0.90f, 0.55f, 1f),
                _ => Color.white
            };
        }
    }
}
