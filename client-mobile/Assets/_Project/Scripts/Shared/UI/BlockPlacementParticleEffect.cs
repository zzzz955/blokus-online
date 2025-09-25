using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shared.UI
{
    /// <summary>
    /// 블록 배치 시 흙먼지 파티클 효과를 관리하는 컴포넌트
    /// 런타임에서 파티클 시스템을 생성하고 관리
    /// </summary>
    public class BlockPlacementParticleEffect : MonoBehaviour
    {
        [Header("파티클 설정")]
        [SerializeField] private int maxParticles = 50;
        [SerializeField] private float particleLifetime = 1.5f;
        [SerializeField] private float emissionRate = 30f;
        [SerializeField] private Vector2 velocityRange = new Vector2(2f, 8f);
        [SerializeField] private Vector2 sizeRange = new Vector2(10f, 25f); // UI 좌표계에 맞게 크기 대폭 증가

        [Header("흙먼지 색상")]
        [SerializeField] private Color dustColor1 = new Color(0.8f, 0.7f, 0.5f, 0.8f); // 베이지색
        [SerializeField] private Color dustColor2 = new Color(0.6f, 0.5f, 0.3f, 0.6f); // 갈색
        [SerializeField] private Color dustColor3 = new Color(0.9f, 0.8f, 0.6f, 0.4f); // 연한 베이지

        [Header("효과 설정")]
        [SerializeField] private float effectDuration = 0.8f;
        [SerializeField] private float spreadRadius = 1.5f;
        [SerializeField] private AnimationCurve sizeCurve;
        [SerializeField] private AnimationCurve alphaCurve;

        // 내부 상태
        private ParticleSystem dustParticleSystem;
        private bool isInitialized = false;

        private void Awake()
        {
            InitializeAnimationCurves();
            InitializeParticleSystem();
        }

        /// <summary>
        /// AnimationCurve 초기화
        /// </summary>
        private void InitializeAnimationCurves()
        {
            // 크기 곡선 (시작: 0, 끝: 1)
            if (sizeCurve == null || sizeCurve.keys.Length == 0)
            {
                sizeCurve = new AnimationCurve();
                sizeCurve.AddKey(0f, 0f);
                sizeCurve.AddKey(1f, 1f);
            }

            // 알파 곡선 (시작: 1, 끝: 0)
            if (alphaCurve == null || alphaCurve.keys.Length == 0)
            {
                alphaCurve = new AnimationCurve();
                alphaCurve.AddKey(0f, 1f);
                alphaCurve.AddKey(1f, 0f);
            }
        }

        /// <summary>
        /// 파티클 시스템 초기화
        /// </summary>
        private void InitializeParticleSystem()
        {
            // 파티클 시스템 GameObject 생성
            GameObject particleObject = new GameObject("DustParticleSystem");
            particleObject.transform.SetParent(transform, false);
            particleObject.transform.localPosition = Vector3.zero;

            // 파티클 시스템 컴포넌트 추가
            dustParticleSystem = particleObject.AddComponent<ParticleSystem>();

            // 파티클 시스템 설정
            SetupParticleSystem();

            isInitialized = true;
        }

        /// <summary>
        /// 파티클 시스템 상세 설정
        /// </summary>
        private void SetupParticleSystem()
        {
            var main = dustParticleSystem.main;
            main.startLifetime = particleLifetime;
            main.startSpeed = 0f; // 초기 속도는 0 (Velocity over Lifetime에서 제어)
            main.startSize = new ParticleSystem.MinMaxCurve(sizeRange.x, sizeRange.y);
            main.startColor = dustColor1;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; // UI Canvas 좌표계에 맞춤
            main.playOnAwake = false;
            main.loop = false;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy; // 부모 스케일 반영

            // Shape 설정 (원형 영역에서 방출)
            var shape = dustParticleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 10f; // UI 좌표계에 맞게 대폭 증가
            shape.radiusMode = ParticleSystemShapeMultiModeValue.Random;

            // Emission 설정
            var emission = dustParticleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f; // Burst로만 방출
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0.0f, (short)(emissionRate * 0.6f)),
                new ParticleSystem.Burst(0.1f, (short)(emissionRate * 0.3f)),
                new ParticleSystem.Burst(0.2f, (short)(emissionRate * 0.1f))
            });

            // Velocity over Lifetime (방사형으로 퍼져나감)
            var velocityOverLifetime = dustParticleSystem.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
            velocityOverLifetime.radial = new ParticleSystem.MinMaxCurve(velocityRange.x * 10f, velocityRange.y * 10f); // UI 좌표계에 맞게 스케일 조정

            // Size over Lifetime
            var sizeOverLifetime = dustParticleSystem.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Color over Lifetime (페이드 아웃)
            var colorOverLifetime = dustParticleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(dustColor1, 0.0f),
                    new GradientColorKey(dustColor2, 0.5f),
                    new GradientColorKey(dustColor3, 1.0f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.8f, 0.0f),
                    new GradientAlphaKey(0.6f, 0.3f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colorOverLifetime.color = gradient;

            // Gravity (약간 위로 올라갔다가 떨어짐)
            var forceOverLifetime = dustParticleSystem.forceOverLifetime;
            forceOverLifetime.enabled = true;
            forceOverLifetime.y = new ParticleSystem.MinMaxCurve(-2f, -1f);

            // Noise (자연스러운 움직임)
            var noise = dustParticleSystem.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 2f;
            noise.damping = true;

            // Renderer 설정
            var renderer = dustParticleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.material = CreateDustMaterial();
            renderer.sortingLayerName = "Default"; // Default 레이어 사용 (UI 레이어 미존재)
            renderer.sortingOrder = 1000; // 다른 UI 요소들보다 높은 우선순위로 설정

            // 추가 렌더링 설정
            renderer.alignment = ParticleSystemRenderSpace.Facing; // UI에 적합한 정면 방향
            renderer.sortMode = ParticleSystemSortMode.Distance; // 거리에 따라 정렬
        }

        /// <summary>
        /// 흙먼지용 머티리얼 생성
        /// </summary>
        private Material CreateDustMaterial()
        {
            // UI용 Unlit/Transparent 쉐이더 사용 (UI에서 더 잘 보임)
            Shader shader = Shader.Find("UI/Default") ?? Shader.Find("Sprites/Default");
            Material dustMaterial = new Material(shader);

            // 간단한 원형 텍스처 생성
            Texture2D dustTexture = CreateDustTexture(64); // 크기를 64로 증가
            dustMaterial.mainTexture = dustTexture;

            // 블렌딩 설정 (알파 블렌딩) - UI 렌더링에 맞게 조정
            dustMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            dustMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            dustMaterial.SetInt("_ZWrite", 0);
            dustMaterial.DisableKeyword("_ALPHATEST_ON");
            dustMaterial.EnableKeyword("_ALPHABLEND_ON");
            dustMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            dustMaterial.renderQueue = 3000; // UI 렌더 큐

            return dustMaterial;
        }

        /// <summary>
        /// 흙먼지 텍스처 생성 (부드러운 원형)
        /// </summary>
        private Texture2D CreateDustTexture(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];

            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = size * 0.45f; // 반지름을 약간 증가

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(1f - (distance / radius));

                    // 부드러운 그라데이션을 더 강하게
                    alpha = Mathf.SmoothStep(0f, 1f, alpha);
                    alpha = Mathf.SmoothStep(0f, 1f, alpha); // 두 번 적용하여 더 부드럽게

                    // 약간의 노이즈 추가로 자연스럽게
                    alpha *= (0.7f + 0.3f * Mathf.PerlinNoise(x * 0.08f, y * 0.08f));

                    // 알파값을 더 강하게 설정 (가시성 향상)
                    alpha = Mathf.Clamp01(alpha * 1.2f);

                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(colors);
            texture.Apply();
            texture.filterMode = FilterMode.Bilinear; // 부드러운 필터링
            texture.wrapMode = TextureWrapMode.Clamp;

            return texture;
        }

        /// <summary>
        /// 흙먼지 효과 재생
        /// </summary>
        /// <param name="worldPosition">월드 좌표에서의 효과 위치</param>
        public void PlayDustEffect(Vector3 worldPosition)
        {
            if (!isInitialized || dustParticleSystem == null)
            {
                Debug.LogWarning($"[BlockPlacementParticleEffect] 파티클 시스템이 초기화되지 않음: initialized={isInitialized}, system={dustParticleSystem != null}");
                return;
            }

            // UI 좌표계로 변환 - 부모 transform 기준으로 로컬 위치 설정
            Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
            dustParticleSystem.transform.localPosition = localPosition;

            // 파티클 재생
            dustParticleSystem.Play();

            // 상세 디버그 정보
            var renderer = dustParticleSystem.GetComponent<ParticleSystemRenderer>();
            Debug.Log($"[BlockPlacementParticleEffect] 흙먼지 효과 재생: world={worldPosition}, local={localPosition}");
            Debug.Log($"[BlockPlacementParticleEffect] Renderer sortingOrder: {renderer.sortingOrder}, sortingLayer: {renderer.sortingLayerName}");
            Debug.Log($"[BlockPlacementParticleEffect] Particle count: {dustParticleSystem.particleCount}, isPlaying: {dustParticleSystem.isPlaying}");
            Debug.Log($"[BlockPlacementParticleEffect] GameObject active: {dustParticleSystem.gameObject.activeInHierarchy}");
            Debug.Log($"[BlockPlacementParticleEffect] SimulationSpace: {dustParticleSystem.main.simulationSpace}");
        }

        /// <summary>
        /// 블록 모양에 맞춘 다중 위치 효과
        /// </summary>
        /// <param name="blockPositions">블록이 차지하는 모든 셀 위치들</param>
        /// <param name="cellSize">셀 크기</param>
        public void PlayDustEffectForBlock(List<Vector3> blockPositions, float cellSize = 20f)
        {
            if (!isInitialized || dustParticleSystem == null || blockPositions.Count == 0)
                return;

            StartCoroutine(PlaySequentialDustEffect(blockPositions));
        }

        /// <summary>
        /// 순차적으로 흙먼지 효과 재생 (블록의 각 셀마다)
        /// </summary>
        private IEnumerator PlaySequentialDustEffect(List<Vector3> positions)
        {
            float delayBetweenCells = 0.1f;

            foreach (Vector3 position in positions)
            {
                PlayDustEffect(position);
                yield return new WaitForSeconds(delayBetweenCells);
            }
        }

        /// <summary>
        /// 효과 정지
        /// </summary>
        public void StopEffect()
        {
            if (dustParticleSystem != null && dustParticleSystem.isPlaying)
            {
                dustParticleSystem.Stop();
            }
        }

        /// <summary>
        /// 파티클 시스템이 재생 중인지 확인
        /// </summary>
        public bool IsPlaying()
        {
            return dustParticleSystem != null && dustParticleSystem.isPlaying;
        }

        private void OnDestroy()
        {
            StopEffect();
        }

        // Inspector에서 테스트용
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void OnValidate()
        {
            if (Application.isPlaying && isInitialized)
            {
                SetupParticleSystem();
            }
        }

        // 테스트용 메서드
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public void TestEffect()
        {
            if (Application.isPlaying)
            {
                PlayDustEffect(transform.position);
            }
        }
    }
}