using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Shared.UI
{
    /// <summary>
    /// UI Image 기반의 간단한 흙먼지 효과
    /// ParticleSystem 대신 UI 요소로 확실한 가시성 보장
    /// </summary>
    public class SimpleDustEffect : MonoBehaviour
    {
        [Header("효과 설정")]
        [SerializeField] private int dustCount = 20; // 더 많은 먼지로 증가
        [SerializeField] private float effectDuration = 1.5f; // 지속시간 증가
        [SerializeField] private float spreadRadius = 80f; // 확산 반경 증가
        [SerializeField] private Vector2 dustSizeRange = new Vector2(20f, 35f); // 크기 대폭 증가

        [Header("색상 설정")]
        [SerializeField] private Color dustColor = new Color(0.8f, 0.7f, 0.5f, 0.8f);

        private List<GameObject> dustPool = new List<GameObject>();
        private bool isInitialized = false;

        private void Awake()
        {
            InitializeDustPool();
        }

        /// <summary>
        /// 흙먼지 UI 요소 풀 초기화
        /// </summary>
        private void InitializeDustPool()
        {
            // 더 많은 먼지를 위해 풀 크기를 두 배로 증가
            int poolSize = dustCount * 2;

            for (int i = 0; i < poolSize; i++)
            {
                GameObject dustObj = CreateDustElement();
                dustObj.SetActive(false);
                dustPool.Add(dustObj);
            }

            isInitialized = true;
            Debug.Log($"[SimpleDustEffect] UI 기반 흙먼지 풀 초기화 완료: {poolSize}개");
        }

        /// <summary>
        /// 개별 흙먼지 UI 요소 생성
        /// </summary>
        private GameObject CreateDustElement()
        {
            GameObject dustObj = new GameObject("DustElement");
            dustObj.transform.SetParent(transform, false);

            // RectTransform 설정
            RectTransform rectTransform = dustObj.AddComponent<RectTransform>();
            float size = Random.Range(dustSizeRange.x, dustSizeRange.y);
            rectTransform.sizeDelta = new Vector2(size, size);
            rectTransform.anchoredPosition = Vector2.zero;

            // Image 컴포넌트
            Image dustImage = dustObj.AddComponent<Image>();
            dustImage.sprite = CreateDustSprite();
            dustImage.color = dustColor;
            dustImage.raycastTarget = false;

            return dustObj;
        }

        /// <summary>
        /// 간단한 원형 스프라이트 생성
        /// </summary>
        private Sprite CreateDustSprite()
        {
            int size = 32;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color[] colors = new Color[size * size];

            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = size * 0.4f;

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(1f - (distance / radius));
                    alpha = Mathf.SmoothStep(0f, 1f, alpha);
                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(colors);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// 흙먼지 효과 재생
        /// </summary>
        public void PlayDustEffect(Vector3 worldPosition)
        {
            if (!isInitialized) return;

            // 월드 좌표를 UI 로컬 좌표로 변환
            Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
            Vector2 centerPos = new Vector2(localPosition.x, localPosition.y);

            StartCoroutine(PlayDustAnimation(centerPos));

            Debug.Log($"[SimpleDustEffect] UI 기반 흙먼지 효과 재생: world={worldPosition}, local={centerPos}");
        }

        /// <summary>
        /// 흙먼지 애니메이션 코루틴
        /// </summary>
        private IEnumerator PlayDustAnimation(Vector2 centerPosition)
        {
            List<GameObject> activeDusts = new List<GameObject>();

            // 사용 가능한 흙먼지 요소들 활성화 및 위치 설정
            for (int i = 0; i < dustCount; i++)
            {
                GameObject dust = dustPool[i];
                if (!dust.activeInHierarchy)
                {
                    dust.SetActive(true);
                    activeDusts.Add(dust);

                    RectTransform rectTransform = dust.GetComponent<RectTransform>();
                    Image dustImage = dust.GetComponent<Image>();

                    // 랜덤한 방향으로 시작 위치 설정
                    float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                    Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector2 startPos = centerPosition;
                    Vector2 endPos = centerPosition + direction * spreadRadius;

                    rectTransform.anchoredPosition = startPos;
                    dustImage.color = new Color(dustColor.r, dustColor.g, dustColor.b, dustColor.a);

                    // 애니메이션 시작
                    StartCoroutine(AnimateDustElement(dust, startPos, endPos, effectDuration));
                }
            }

            // 효과 지속 시간 대기
            yield return new WaitForSeconds(effectDuration);

            // 활성화된 흙먼지 요소들 비활성화
            foreach (GameObject dust in activeDusts)
            {
                dust.SetActive(false);
            }
        }

        /// <summary>
        /// 개별 흙먼지 요소 애니메이션
        /// </summary>
        private IEnumerator AnimateDustElement(GameObject dust, Vector2 startPos, Vector2 endPos, float duration)
        {
            RectTransform rectTransform = dust.GetComponent<RectTransform>();
            Image dustImage = dust.GetComponent<Image>();

            float elapsed = 0f;
            Color startColor = dustImage.color;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // 위치 애니메이션 (가속도 적용)
                float posT = 1f - (1f - t) * (1f - t); // ease-out
                rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, posT);

                // 페이드 아웃 애니메이션
                float alpha = Mathf.Lerp(startColor.a, 0f, t);
                dustImage.color = new Color(startColor.r, startColor.g, startColor.b, alpha);

                // 크기 애니메이션 (약간 커졌다가 작아짐)
                float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.3f;
                rectTransform.localScale = Vector3.one * scale;

                yield return null;
            }
        }

        /// <summary>
        /// 블록 모양에 맞춘 다중 위치 효과 (모든 셀에서 동시 재생)
        /// </summary>
        public void PlayDustEffectForBlock(List<Vector3> blockPositions)
        {
            if (!isInitialized || blockPositions.Count == 0) return;

            // 순차 재생 대신 모든 위치를 동시에 처리하는 통합 애니메이션
            StartCoroutine(PlayMultipleDustEffect(blockPositions));
        }

        /// <summary>
        /// 모든 셀에서 동시에 흙먼지 효과 재생 (풀링 충돌 방지)
        /// </summary>
        private IEnumerator PlayMultipleDustEffect(List<Vector3> positions)
        {
            List<GameObject> activeDusts = new List<GameObject>();
            List<Vector2> localPositions = new List<Vector2>();

            // 모든 월드 좌표를 로컬 좌표로 변환
            foreach (Vector3 worldPos in positions)
            {
                Vector3 localPos = transform.InverseTransformPoint(worldPos);
                localPositions.Add(new Vector2(localPos.x, localPos.y));
            }

            // 각 위치마다 개별 먼지 효과 생성
            int dustPerPosition = dustCount / positions.Count; // 위치당 먼지 개수
            if (dustPerPosition < 2) dustPerPosition = 2; // 최소 2개씩

            int poolIndex = 0;
            for (int posIndex = 0; posIndex < positions.Count; posIndex++)
            {
                Vector2 centerPos = localPositions[posIndex];

                // 이 위치를 위한 먼지들 생성
                for (int dustIndex = 0; dustIndex < dustPerPosition && poolIndex < dustPool.Count; dustIndex++)
                {
                    GameObject dust = dustPool[poolIndex++];
                    if (!dust.activeInHierarchy)
                    {
                        dust.SetActive(true);
                        activeDusts.Add(dust);

                        RectTransform rectTransform = dust.GetComponent<RectTransform>();
                        Image dustImage = dust.GetComponent<Image>();

                        // 랜덤한 방향으로 확산
                        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        Vector2 startPos = centerPos + direction * Random.Range(0f, 10f); // 약간의 랜덤 시작점
                        Vector2 endPos = centerPos + direction * spreadRadius;

                        rectTransform.anchoredPosition = startPos;
                        dustImage.color = new Color(dustColor.r, dustColor.g, dustColor.b, dustColor.a);

                        // 개별 애니메이션 시작
                        StartCoroutine(AnimateDustElement(dust, startPos, endPos, effectDuration));
                    }
                }
            }

            // 블록 중앙에 추가 큰 효과
            if (positions.Count > 1)
            {
                Vector3 centerWorldPos = CalculateCenterPosition(positions);
                yield return new WaitForSeconds(0.1f);
                PlayLargeDustEffect(centerWorldPos);
            }

            Debug.Log($"[SimpleDustEffect] 멀티 셀 흙먼지 효과 재생: {positions.Count}개 위치, {activeDusts.Count}개 먼지");

            // 효과 지속 시간 대기
            yield return new WaitForSeconds(effectDuration);

            // 사용된 먼지들 비활성화
            foreach (GameObject dust in activeDusts)
            {
                if (dust != null) dust.SetActive(false);
            }
        }

        /// <summary>
        /// 모든 셀에서 동시에 흙먼지 효과 재생 (더 눈에 띄게)
        /// </summary>
        private IEnumerator PlaySequentialDustEffect(List<Vector3> positions)
        {
            // 모든 위치에서 거의 동시에 재생 (아주 짧은 딜레이로 시각적 효과 증대)
            float delayBetweenCells = 0.02f; // 딜레이를 대폭 단축

            foreach (Vector3 position in positions)
            {
                PlayDustEffect(position);
                yield return new WaitForSeconds(delayBetweenCells);
            }

            // 추가로 블록 전체 영역 중앙에서도 큰 효과 하나 더
            if (positions.Count > 1)
            {
                Vector3 centerPosition = CalculateCenterPosition(positions);
                yield return new WaitForSeconds(0.1f);
                PlayLargeDustEffect(centerPosition);
            }
        }

        /// <summary>
        /// 블록 중앙 위치 계산
        /// </summary>
        private Vector3 CalculateCenterPosition(List<Vector3> positions)
        {
            Vector3 sum = Vector3.zero;
            foreach (Vector3 pos in positions)
            {
                sum += pos;
            }
            return sum / positions.Count;
        }

        /// <summary>
        /// 큰 중앙 효과 재생
        /// </summary>
        private void PlayLargeDustEffect(Vector3 worldPosition)
        {
            Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
            Vector2 centerPos = new Vector2(localPosition.x, localPosition.y);

            StartCoroutine(PlayLargeDustAnimation(centerPos));
        }

        /// <summary>
        /// 큰 중앙 흙먼지 애니메이션 (더 많은 먼지로)
        /// </summary>
        private IEnumerator PlayLargeDustAnimation(Vector2 centerPosition)
        {
            List<GameObject> activeDusts = new List<GameObject>();
            int largeDustCount = Mathf.Min(dustCount, 8); // 최대 8개까지 큰 효과

            for (int i = 0; i < largeDustCount; i++)
            {
                GameObject dust = dustPool[i];
                if (!dust.activeInHierarchy)
                {
                    dust.SetActive(true);
                    activeDusts.Add(dust);

                    RectTransform rectTransform = dust.GetComponent<RectTransform>();
                    Image dustImage = dust.GetComponent<Image>();

                    // 더 큰 크기 적용
                    float largeSize = Random.Range(dustSizeRange.y, dustSizeRange.y * 1.5f);
                    rectTransform.sizeDelta = new Vector2(largeSize, largeSize);

                    // 더 넓은 확산
                    float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                    Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector2 startPos = centerPosition;
                    Vector2 endPos = centerPosition + direction * (spreadRadius * 1.3f);

                    rectTransform.anchoredPosition = startPos;
                    dustImage.color = new Color(dustColor.r, dustColor.g, dustColor.b, dustColor.a * 1.2f);

                    StartCoroutine(AnimateDustElement(dust, startPos, endPos, effectDuration * 1.2f));
                }
            }

            yield return new WaitForSeconds(effectDuration * 1.2f);

            foreach (GameObject dust in activeDusts)
            {
                dust.SetActive(false);
            }
        }
    }
}