// Assets/Scripts/Features/Single/UI/Scene/GameStartRouter.cs
using UnityEngine;
using Features.Single.Gameplay;
using Shared.Models;

namespace Features.Single.UI.Scene
{
    public class GameStartRouter : MonoBehaviour
    {
        public void StartByNumber(int stageNumber)
        {
            var gm = SingleGameManager.Instance ?? FindObjectOfType<SingleGameManager>(true);
            if (!gm) { Debug.LogError("[GameStartRouter] SingleGameManager not found"); return; }
            gm.RequestStartByNumber(stageNumber);
        }

        public void StartByData(StageData data)
        {
            var gm = SingleGameManager.Instance ?? FindObjectOfType<SingleGameManager>(true);
            if (!gm) { Debug.LogError("[GameStartRouter] SingleGameManager not found"); return; }
            gm.ApplyStageData(data);
        }
    }
}
