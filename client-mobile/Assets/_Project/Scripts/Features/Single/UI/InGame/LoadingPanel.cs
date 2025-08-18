using UnityEngine;

namespace BlokusUnity.UI
{
    public class LoadingPanel : BlokusUnity.UI.PanelBase
    {
        protected override void Start()
        {
            base.Start();
            Debug.Log("LoadingPanel 초기화 완료");
        }
    }
}