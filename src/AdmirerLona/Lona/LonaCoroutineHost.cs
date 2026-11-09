using System.Collections;
using UnityEngine;

namespace AdmirerLona
{
    /// <summary>
    /// 模组自己的 MonoBehaviour，只用来跑协程。
    /// 新版扫描器允许在“自己的组件”上使用 this.StartCoroutine；
    /// 加载器停止模组时也会销毁这个组件，因此不会再触发 threads 能力。
    /// </summary>
    public sealed class LonaCoroutineHost : MonoBehaviour
    {
        private static LonaCoroutineHost instance;

        public static void Ensure()
        {
            if (instance != null) return;

            GameObject host = new GameObject("AdmirerLona Coroutine Host");
            UnityEngine.Object.DontDestroyOnLoad(host);
            instance = host.AddComponent<LonaCoroutineHost>();
        }

        public static Coroutine Run(IEnumerator routine)
        {
            if (routine == null) return null;
            Ensure();
            return instance != null ? instance.RunInternal(routine) : null;
        }

        public static void DestroyHost()
        {
            if (instance == null) return;
            UnityEngine.Object.Destroy(instance.gameObject);
            instance = null;
        }

        private Coroutine RunInternal(IEnumerator routine)
        {
            return StartCoroutine(routine);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
