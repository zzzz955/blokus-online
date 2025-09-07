using System;
using System.Collections.Generic;
using UnityEngine;

namespace Features.Multi.Net
{
    /// <summary>
    /// Unity 메인스레드에서 작업을 실행하기 위한 디스패처
    /// 네트워크 수신 스레드에서 UI 업데이트 작업을 안전하게 메인스레드로 전달
    /// </summary>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static readonly Queue<Action> ExecutionQueue = new Queue<Action>();
        private static readonly object QueueLock = new object();
        
        public static UnityMainThreadDispatcher Instance { get; private set; }

        void Awake()
        {
            // 싱글톤 패턴 구현
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Update()
        {
            // 메인스레드에서 큐에 있는 모든 작업을 실행
            ProcessQueue();
        }

        /// <summary>
        /// 액션을 메인스레드 실행 큐에 추가
        /// </summary>
        /// <param name="action">메인스레드에서 실행할 액션</param>
        public static void Enqueue(Action action)
        {
            if (action == null) return;

            lock (QueueLock)
            {
                ExecutionQueue.Enqueue(action);
            }
        }

        /// <summary>
        /// 큐에 있는 모든 액션을 실행
        /// </summary>
        private static void ProcessQueue()
        {
            lock (QueueLock)
            {
                while (ExecutionQueue.Count > 0)
                {
                    try
                    {
                        var action = ExecutionQueue.Dequeue();
                        action.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[UnityMainThreadDispatcher] 메인스레드 액션 실행 중 오류: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 현재 실행 큐의 크기 반환 (디버깅용)
        /// </summary>
        public static int GetQueueSize()
        {
            lock (QueueLock)
            {
                return ExecutionQueue.Count;
            }
        }

        /// <summary>
        /// 큐 초기화 (필요시)
        /// </summary>
        public static void ClearQueue()
        {
            lock (QueueLock)
            {
                ExecutionQueue.Clear();
            }
        }

        void OnDestroy()
        {
            ClearQueue();
        }
    }
}