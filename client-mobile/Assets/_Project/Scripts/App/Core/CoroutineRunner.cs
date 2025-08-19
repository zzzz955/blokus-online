using UnityEngine;
using System.Collections;

namespace App.Core
{
    public sealed class CoroutineRunner : MonoBehaviour
    {
        static CoroutineRunner _instance;
        public static CoroutineRunner Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("~CoroutineRunner");
                    _instance = go.AddComponent<CoroutineRunner>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        public static Coroutine Run(System.Collections.IEnumerator routine)
        {
            return Instance.StartCoroutine(routine);
        }

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}
