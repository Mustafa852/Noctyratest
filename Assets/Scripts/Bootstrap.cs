using UnityEngine;

namespace Noctyra
{
    /// <summary>
    /// Oyun hangi sahneden açılırsa açılsın arayüzü otomatik kurar.
    /// Sahneye elle bir şey eklemene gerek yok.
    /// </summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (Object.FindFirstObjectByType<GameController>() != null) return;
            var go = new GameObject("Noctyra");
            go.AddComponent<GameController>();
            Object.DontDestroyOnLoad(go);

            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = Color.black;
        }
    }
}
