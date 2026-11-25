using UnityEngine;

namespace HorrorFPS
{
    public enum AudioCategory
    {
        Effects,
        Voice,
        Interface
    }

    [AddComponentMenu("")]
    public sealed class AudioCategoryFilter : MonoBehaviour
    {
        static float effectsVolume = 1f;
        static float voiceVolume = 1f;
        static float interfaceVolume = 1f;
        static bool initialized;

        [SerializeField] AudioCategory category = AudioCategory.Effects;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            GameSettings.Changed -= Refresh;
            effectsVolume = 1f;
            voiceVolume = 1f;
            interfaceVolume = 1f;
            initialized = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize() { EnsureInitialized(); }

        void Awake() { EnsureInitialized(); }

        static void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            GameSettings.Changed += Refresh;
            Refresh();
        }

        static void Refresh()
        {
            effectsVolume = GameSettings.EffectsVolume;
            voiceVolume = GameSettings.VoiceVolume;
            interfaceVolume = GameSettings.InterfaceVolume;
        }

        public static void Attach(AudioSource source, AudioCategory category)
        {
            if (source == null) return;
            EnsureInitialized();
            AudioCategoryFilter filter = source.GetComponent<AudioCategoryFilter>();
            if (filter == null) filter = source.gameObject.AddComponent<AudioCategoryFilter>();
            filter.category = category;
        }

        public static AudioSource PlayClipAtPoint(AudioClip clip, Vector3 position, float volume, AudioCategory category,
                                                  float spatialBlend = 1f)
        {
            if (clip == null) return null;

            GameObject go = new GameObject("OneShot_" + clip.name);
            go.transform.position = position;
            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.spatialBlend = Mathf.Clamp01(spatialBlend);
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 1f;
            source.maxDistance = 500f;
            source.dopplerLevel = 0f;
            Attach(source, category);
            source.Play();
            Destroy(go, clip.length + 0.2f);
            return source;
        }

        void OnAudioFilterRead(float[] data, int channels)
        {
            float gain;
            switch (category)
            {
                case AudioCategory.Voice: gain = voiceVolume; break;
                case AudioCategory.Interface: gain = interfaceVolume; break;
                default: gain = effectsVolume; break;
            }

            if (gain >= 0.99999f) return;
            for (int i = 0; i < data.Length; i++) data[i] *= gain;
        }
    }
}
