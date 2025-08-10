using UnityEngine;
using BlokusUnity.Common;

namespace BlokusUnity.Game
{
    public sealed class SingleGameBootstrap : MonoBehaviour
    {
        [SerializeField] private SingleGameManager gameManager;

        private void Start()
        {
            if (gameManager == null) gameManager = FindObjectOfType<SingleGameManager>();
            if (gameManager == null) return;

            if (!gameManager.IsInitialized)
            {
                var payload = new StagePayload
                {
                    StageName = "Local Debug",
                    BoardSize = 20,
                    AvailableBlocks = null, // null이면 기본 풀세트 적용
                    ParScore = 0
                };
                gameManager.Init(payload);
            }
        }
    }
}
