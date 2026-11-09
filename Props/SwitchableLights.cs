using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace HorrorFPS
{
    /// <summary>
    /// Conjunto de luzes (e lâmpadas emissivas) que liga e desliga: lustre, abajur, luz do corredor.
    /// Quem aciona é outro componente (LightSwitch, um quadro de fusíveis, um monstro, um UnityEvent...).
    /// Pode depender de uma variável de energia da partida (ex.: "energia_ligada"). Ponha SaveableEntity no mesmo objeto para salvar.
    /// </summary>
    [AddComponentMenu("HorrorFPS/Props/Switchable Lights")]
    public class SwitchableLights : MonoBehaviour, ISaveable
    {
        [SerializeField] Light[] lights;
        [SerializeField] Renderer[] emissiveRenderers;
        [SerializeField] Color emissionColor = new Color(1.6f, 1.25f, 0.7f);
        [SerializeField] bool startOn = true;

        [Header("Energia")]
        [Tooltip("Se preenchida, as luzes só acendem enquanto esta variável da partida estiver ligada.")]
        [SerializeField] string powerVariable;

        [Header("Efeitos")]
        [SerializeField] bool flickerOnToggle = true;
        [SerializeField] AudioClip toggleSound;
        [SerializeField] UnityEvent<bool> onChanged;

        [Serializable]
        class State { public bool on; }

        bool on;
        bool ready;
        float[] baseIntensity;
        Material[] materials;
        float level;
        Coroutine flicker;

        /// <summary>Posição do interruptor (independe de haver energia).</summary>
        public bool On { get { Init(); return on; } }
        public bool HasPower { get { return string.IsNullOrEmpty(powerVariable) || PropsBridge.GetBool(powerVariable); } }
        /// <summary>Realmente acesa agora.</summary>
        public bool IsLit { get { return On && HasPower; } }

        void Init()
        {
            if (ready) return;
            ready = true;
            on = startOn;
            baseIntensity = new float[lights != null ? lights.Length : 0];
            for (int i = 0; i < baseIntensity.Length; i++) baseIntensity[i] = lights[i] != null ? lights[i].intensity : 0f;
            int n = emissiveRenderers != null ? emissiveRenderers.Length : 0;
            materials = new Material[n];
            for (int i = 0; i < n; i++)
            {
                if (emissiveRenderers[i] == null) continue;
                materials[i] = emissiveRenderers[i].material;
                materials[i].EnableKeyword("_EMISSION");
            }
            Apply(IsLit ? 1f : 0f);
        }

        void Awake() { Init(); }
        void OnEnable() { GameSession.VariableChanged += OnVariable; }
        void OnDisable() { GameSession.VariableChanged -= OnVariable; }

        void OnVariable(string key)
        {
            if (!ready || key != powerVariable) return;
            if (flicker != null) StopCoroutine(flicker);
            flicker = null;
            Apply(IsLit ? 1f : 0f);
        }

        void Apply(float k)
        {
            level = k;
            for (int i = 0; i < baseIntensity.Length; i++)
            {
                if (lights[i] == null) continue;
                lights[i].enabled = k > 0.01f;
                lights[i].intensity = baseIntensity[i] * k;
            }
            for (int i = 0; i < materials.Length; i++)
                if (materials[i] != null) materials[i].SetColor("_EmissionColor", emissionColor * k);
        }

        public void Toggle() { SetOn(!On, false); }

        public void SetOn(bool value, bool instant)
        {
            Init();
            if (on == value && !instant) return;
            on = value;
            if (flicker != null) StopCoroutine(flicker);
            flicker = null;
            if (instant || !isActiveAndEnabled || !flickerOnToggle || !on)
            {
                Apply(IsLit ? 1f : 0f);
            }
            else flicker = StartCoroutine(FlickerRoutine());
            if (!instant)
            {
                PropsBridge.Sfx(toggleSound, PropSfx.Kind.Click, transform.position, 0.6f);
                onChanged.Invoke(on);
            }
        }

        public void SetOn(bool value) { SetOn(value, false); }

        IEnumerator FlickerRoutine()
        {
            int steps = UnityEngine.Random.Range(4, 8);
            for (int i = 0; i < steps; i++)
            {
                Apply(i % 2 == 0 && HasPower ? UnityEngine.Random.Range(0.4f, 1f) : 0f);
                yield return new WaitForSeconds(UnityEngine.Random.Range(0.03f, 0.1f));
            }
            Apply(IsLit ? 1f : 0f);
            flicker = null;
        }

        // ------------------------------------------------------------ ISaveable

        public string SaveKey { get { return "lights"; } }
        public string CaptureState() { return JsonUtility.ToJson(new State { on = On }); }

        public void RestoreState(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            State s = JsonUtility.FromJson<State>(json);
            if (s != null) SetOn(s.on, true);
        }
    }
}
