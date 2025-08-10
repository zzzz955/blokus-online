// Assets/Scripts/UI/SceneLoader.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using BlokusUnity.Common;
using BlokusUnity.Game;

namespace BlokusUnity.UI
{
    public sealed class SceneLoader : MonoBehaviour
    {
        [SerializeField] private string gameplaySceneName = "SingleGameplayScene";

        // MainScene의 StageSelect 버튼에서 호출:
        public void LoadSingle(int stageNumber = 1)
        {
            // (선택) 컨텍스트 저장: StageDataManager가 있다면 같이 넘겨둠
            var sdm = FindObjectOfType<BlokusUnity.Data.StageDataManager>();
            SingleGameManager.SetStageContext(stageNumber, sdm);

            // 실제로는 StagePayload를 네트워크/리포지토리에서 받아오고,
            // Gameplay 씬에서 SingleGameManager.Init(payload) 호출
            SceneManager.LoadScene(gameplaySceneName, LoadSceneMode.Single);
        }

        public void BackToMain() => SceneManager.LoadScene("MainScene", LoadSceneMode.Single);
    }
}
