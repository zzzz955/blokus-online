using UnityEngine;

namespace Shared.UI
{
    /// <summary>
    /// 런타임에서 간단한 아이콘 스프라이트를 생성하는 유틸리티
    /// </summary>
    public static class IconSpriteGenerator
    {
        private static Texture2D CreateIconTexture(int size, Color backgroundColor)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];

            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = backgroundColor;
            }

            texture.SetPixels(colors);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// 체크마크(✓) 아이콘 생성
        /// </summary>
        public static Sprite CreateCheckIcon(int size = 16, Color color = default)
        {
            if (color == default) color = Color.green;

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];

            // 배경을 투명하게
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = Color.clear;
            }

            // 체크마크 그리기 (간단한 픽셀 패턴)
            int center = size / 2;

            // 체크마크의 첫 번째 선 (왼쪽 아래에서 중간으로)
            for (int i = 0; i < center - 2; i++)
            {
                int x = center - 4 + i;
                int y = center - 2 + i;
                if (x >= 0 && x < size && y >= 0 && y < size)
                {
                    colors[y * size + x] = color;
                    // 두께를 위해 인접 픽셀도 칠하기
                    if (x + 1 < size) colors[y * size + (x + 1)] = color;
                    if (y + 1 < size) colors[(y + 1) * size + x] = color;
                }
            }

            // 체크마크의 두 번째 선 (중간에서 오른쪽 위로)
            for (int i = 0; i < center + 2; i++)
            {
                int x = center + i;
                int y = center + 2 - i;
                if (x >= 0 && x < size && y >= 0 && y < size)
                {
                    colors[y * size + x] = color;
                    // 두께를 위해 인접 픽셀도 칠하기
                    if (x + 1 < size) colors[y * size + (x + 1)] = color;
                    if (y + 1 < size) colors[(y + 1) * size + x] = color;
                }
            }

            texture.SetPixels(colors);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// X 아이콘 생성
        /// </summary>
        public static Sprite CreateCrossIcon(int size = 16, Color color = default)
        {
            if (color == default) color = Color.red;

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];

            // 배경을 투명하게
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = Color.clear;
            }

            // X 그리기 (대각선 두 개)
            int padding = 2;

            // 왼쪽 위에서 오른쪽 아래로
            for (int i = padding; i < size - padding; i++)
            {
                int x = i;
                int y = i;
                colors[y * size + x] = color;
                // 두께를 위해 인접 픽셀도 칠하기
                if (x + 1 < size) colors[y * size + (x + 1)] = color;
                if (y + 1 < size) colors[(y + 1) * size + x] = color;
            }

            // 오른쪽 위에서 왼쪽 아래로
            for (int i = padding; i < size - padding; i++)
            {
                int x = size - 1 - i;
                int y = i;
                colors[y * size + x] = color;
                // 두께를 위해 인접 픽셀도 칠하기
                if (x - 1 >= 0) colors[y * size + (x - 1)] = color;
                if (y + 1 < size) colors[(y + 1) * size + x] = color;
            }

            texture.SetPixels(colors);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// 금지(🚫) 아이콘 생성 (원 + 사선)
        /// </summary>
        public static Sprite CreateBlockedIcon(int size = 16, Color color = default)
        {
            if (color == default) color = Color.gray;

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];

            // 배경을 투명하게
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = Color.clear;
            }

            int center = size / 2;
            int radius = center - 2;

            // 원 그리기
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));

                    // 원의 테두리
                    if (distance >= radius - 1 && distance <= radius + 1)
                    {
                        colors[y * size + x] = color;
                    }
                }
            }

            // 사선 그리기 (왼쪽 위에서 오른쪽 아래로)
            for (int i = 0; i < size; i++)
            {
                int x = i;
                int y = i;
                if (x >= 0 && x < size && y >= 0 && y < size)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    if (distance <= radius)
                    {
                        colors[y * size + x] = color;
                        // 두께를 위해 인접 픽셀도 칠하기
                        if (x + 1 < size) colors[y * size + (x + 1)] = color;
                        if (y + 1 < size) colors[(y + 1) * size + x] = color;
                    }
                }
            }

            texture.SetPixels(colors);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// 테두리용 9-slice 스프라이트 생성
        /// </summary>
        public static Sprite CreateBorderSprite(int size = 32, int borderWidth = 4, Color borderColor = default)
        {
            if (borderColor == default) borderColor = Color.white;

            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    // 테두리 영역인지 확인
                    bool isBorder = x < borderWidth || x >= size - borderWidth ||
                                   y < borderWidth || y >= size - borderWidth;

                    colors[y * size + x] = isBorder ? borderColor : Color.clear;
                }
            }

            texture.SetPixels(colors);
            texture.Apply();

            // 9-slice로 생성
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(borderWidth, borderWidth, borderWidth, borderWidth));
        }
    }
}