using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// A luz de uma lanterna jogada no chão. O WorldItem cria este componente sozinho para todo item do tipo Lanterna (ItemEquip.kind):
    /// se ela foi largada acesa (flag "ligada" na instância), continua acesa no chão, gastando a bateria, e apaga quando a bateria acaba.
    /// O feixe sai pelo mesmo eixo que a lanterna aponta na mão (ItemEquip.heldEuler): se o modelo do mundo (worldPrefab) estiver
    /// orientado diferente do modelo da mão, ajuste o heldEuler. Ao ser pega, o WorldItem apaga a lanterna e destrói este componente junto.
    /// </summary>
    [AddComponentMenu("")]
    public class WorldItemLight : MonoBehaviour
    {
        ItemInstance inst;
        Light beam;
        float acc, seed;

        public Light Beam { get { return beam; } }

        public void Init(ItemInstance instance)
        {
            if (instance == null || instance.data == null || instance.data.equip == null) return;
            inst = instance;
            seed = Random.value * 50f;
            EquipState.Ensure(inst);
            ItemEquip eq = inst.data.equip;

            // eixo do feixe no espaço do modelo: na mão ele é o +Z da raiz e o modelo está girado por heldEuler
            Vector3 axis = Quaternion.Inverse(Quaternion.Euler(eq.heldEuler)) * Vector3.forward;

            // centro do modelo e quanto sobra até a ponta, para a luz nascer FORA da malha (dentro dela a sombra taparia tudo)
            Vector3 localCenter = Vector3.zero;
            float reach = 0.05f;
            Renderer[] rs = GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
                localCenter = transform.InverseTransformPoint(b.center);
                Vector3 worldAxis = transform.TransformDirection(axis);
                reach = Mathf.Abs(worldAxis.x) * b.extents.x + Mathf.Abs(worldAxis.y) * b.extents.y + Mathf.Abs(worldAxis.z) * b.extents.z + 0.03f;
            }

            GameObject lg = new GameObject("DroppedFlashlight");
            lg.transform.SetParent(transform, false);
            lg.transform.localRotation = Quaternion.FromToRotation(Vector3.forward, axis);
            lg.transform.localPosition = localCenter + axis * (reach / Mathf.Max(0.01f, transform.lossyScale.x));
            beam = lg.AddComponent<Light>();
            beam.type = LightType.Spot;
            beam.range = eq.lightRange;
            beam.spotAngle = eq.lightAngle;
            beam.innerSpotAngle = eq.lightAngle * 0.55f;
            beam.intensity = eq.lightIntensity;
            beam.color = eq.lightColor;
            beam.shadows = LightShadows.Soft;
            beam.enabled = Powered();
        }

        bool Powered()
        {
            if (inst == null || !inst.HasFlag(EquipState.Lit)) return false;
            ItemEquip eq = inst.data.equip;
            return eq.batterySeconds <= 0 || EquipState.BatteryOf(inst) > 0;
        }

        void Update()
        {
            if (inst == null || beam == null) return;
            bool on = Powered();
            if (!on && inst.HasFlag(EquipState.Lit)) inst.SetFlag(EquipState.Lit, false);       // acabou a bateria no chão
            if (beam.enabled != on) beam.enabled = on;
            if (!on) return;

            ItemEquip eq = inst.data.equip;
            float frac = 1f;
            if (eq.batterySeconds > 0)
            {
                acc += Time.deltaTime;
                if (acc >= 1f)
                {
                    int s = Mathf.FloorToInt(acc);
                    acc -= s;
                    int left = inst.AddCounter(EquipState.Battery, -s);
                    if (left <= 0)
                    {
                        inst.SetCounter(EquipState.Battery, 0);
                        inst.SetFlag(EquipState.Lit, false);
                        beam.enabled = false;
                        return;
                    }
                }
                frac = Mathf.Clamp01(inst.GetCounter(EquipState.Battery) / (float)eq.batterySeconds);
            }

            // a mesma falha de quando está na mão: a luz enfraquece e pisca no fim da bateria
            float k = Mathf.InverseLerp(0.2f, 0f, frac);
            float intensity = eq.lightIntensity * Mathf.Lerp(1f, 0.55f, k);
            if (k > 0f && Mathf.PerlinNoise(Time.time * 15f, seed) < 0.15f + 0.45f * k) intensity *= 0.12f;
            beam.intensity = intensity;
        }
    }
}
