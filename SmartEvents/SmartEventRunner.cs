using UnityEngine;

namespace SmartEvents
{
    /// <summary>
    /// Executa os delays dos SmartEvents. É criado sob demanda (objeto oculto, persiste entre cenas),
    /// então você nunca precisa colocá-lo na cena.
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class SmartEventRunner : MonoBehaviour
    {
        private static SmartEventRunner _instance;
        private static bool _quitting;

        public static SmartEventRunner Instance
        {
            get
            {
                if (_instance == null && !_quitting && Application.isPlaying)
                {
                    GameObject host = new GameObject("[SmartEventRunner]");
                    host.hideFlags = HideFlags.HideInHierarchy;
                    DontDestroyOnLoad(host);
                    _instance = host.AddComponent<SmartEventRunner>();
                }
                return _instance;
            }
        }

        private void OnApplicationQuit()
        {
            _quitting = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _quitting = false;
        }
    }
}
