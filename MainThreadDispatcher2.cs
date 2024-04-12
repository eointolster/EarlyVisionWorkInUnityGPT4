using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MainThreadDispatcher2 : MonoBehaviour
{
    private static readonly Queue<System.Action> _executionQueue = new Queue<System.Action>();

    public void Update()
    {
        lock (_executionQueue)
        {
            while (_executionQueue.Count > 0)
            {
                _executionQueue.Dequeue().Invoke();
            }
        }
    }

    public static void Enqueue(System.Action action)
    {
        lock (_executionQueue)
        {
            _executionQueue.Enqueue(action);
        }
    }

    private static MainThreadDispatcher2 _instance = null;
    public static MainThreadDispatcher2 Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("MainThreadDispatcher2");
                _instance = go.AddComponent<MainThreadDispatcher2>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
}
