using UnityEngine;
using System;
using System.Collections.Generic;

namespace HandTracking.Utils
{
    /// <summary>
    /// Диспетчер для выполнения кода в главном потоке Unity
    /// (MediaPipe callbacks приходят из другого потока)
    /// </summary>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private const string MAIN_THREAD_DISPATCHER = "MainThreadDispatcher";
        private static UnityMainThreadDispatcher instance;
        public static UnityMainThreadDispatcher Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject(MAIN_THREAD_DISPATCHER);
                    instance = go.AddComponent<UnityMainThreadDispatcher>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        private readonly Queue<Action> executionQueue = new Queue<Action>();
        private readonly object lockObject = new object();

        public void Enqueue(Action action)
        {
            lock (lockObject)
            {
                executionQueue.Enqueue(action);
            }
        }

        private void Update()
        {
            lock (lockObject)
            {
                while (executionQueue.Count > 0)
                {
                    var action = executionQueue.Dequeue();
                    action?.Invoke();
                }
            }
        }
    }
}
