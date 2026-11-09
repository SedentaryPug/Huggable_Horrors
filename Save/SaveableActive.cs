using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Salva se o objeto está ligado ou desligado (item escondido, luz apagada, inimigo desativado).</summary>
    [AddComponentMenu("HorrorFPS/Save/Saveable Active")]
    public class SaveableActive : MonoBehaviour, ISaveable
    {
        public string SaveKey { get { return "active"; } }

        public string CaptureState() { return gameObject.activeSelf ? "1" : "0"; }

        public void RestoreState(string json) { gameObject.SetActive(json == "1"); }
    }
}
