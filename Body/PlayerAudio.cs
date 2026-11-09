using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Como o som de um jogador chega aos ouvidos.
    /// ATENÇÃO (split-screen): o framework base mantém UM AudioListener ativo (o do jogador 1), então não existe áudio "só do jogador 2".
    ///   Local2D = toca sem posição: todo mundo ouve (bom para um jogador só).
    ///   World3D = toca na cabeça do jogador: ouve-se de perto/de longe como um som do mundo (bom para co-op: o batimento do
    ///             parceiro assustado só aparece quando ele está perto — e quem está ao lado dele escuta).
    ///   Auto    = Local2D com um jogador, World3D com dois ou mais.
    /// </summary>
    public enum AudioMode { Auto, Local2D, World3D }

    public static class PlayerAudio
    {
        static int cachedCount = 1;
        static float cachedAt = -10f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { cachedCount = 1; cachedAt = -10f; }

        /// <summary>Quantos jogadores existem agora (atualizado no máximo uma vez por segundo).</summary>
        public static int PlayerCount
        {
            get
            {
                if (Time.unscaledTime - cachedAt > 1f)
                {
                    cachedAt = Time.unscaledTime;
                    cachedCount = Mathf.Max(1, HorrorUtil.FindAll<PlayerContext>().Length);
                }
                return cachedCount;
            }
        }

        public static float SpatialBlend(AudioMode mode)
        {
            switch (mode)
            {
                case AudioMode.Local2D: return 0f;
                case AudioMode.World3D: return 1f;
                default: return PlayerCount > 1 ? 1f : 0f;
            }
        }

        /// <summary>Cria uma AudioSource filha de 'parent' pronta para sons curtos do corpo.</summary>
        public static AudioSource Create(Transform parent, string name, float maxDistance = 16f)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            AudioSource src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 1.2f;
            src.maxDistance = maxDistance;
            src.dopplerLevel = 0f;
            return src;
        }
    }
}
