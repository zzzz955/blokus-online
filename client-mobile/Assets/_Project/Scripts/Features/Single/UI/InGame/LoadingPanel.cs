using UnityEngine;

namespace BlokusUnity.UI
{
    public class LoadingPanel : BaseUIPanel
    {
        protected override void Start()
        {
            base.Start();
            Debug.Log("LoadingPanel 초기화 완료");
        }
    }
}