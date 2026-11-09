using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Efeitos visuais gerados por código (partículas e luz). Sem assets: troque por prefabs nos componentes quando quiser.</summary>
    public static class PropFx
    {
        static Material puffMaterial;
        static Texture2D puffTexture;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() { puffMaterial = null; puffTexture = null; }

        static Material PuffMaterial()
        {
            if (puffMaterial != null) return puffMaterial;
            if (puffTexture == null)
            {
                puffTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
                puffTexture.wrapMode = TextureWrapMode.Clamp;
                for (int y = 0; y < 32; y++)
                {
                    for (int x = 0; x < 32; x++)
                    {
                        float r = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) / 16f;
                        float a = Mathf.Clamp01(1f - r);
                        puffTexture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                    }
                }
                puffTexture.Apply();
            }
            string[] names =
            {
                "Universal Render Pipeline/Particles/Unlit",
                "Particles/Standard Unlit",
                "Legacy Shaders/Particles/Alpha Blended",
                "Sprites/Default"
            };
            Shader shader = null;
            for (int i = 0; i < names.Length && shader == null; i++) shader = Shader.Find(names[i]);
            if (shader == null)
            {
                Debug.LogWarning("[Props] Nenhum shader de partícula encontrado para o efeito de explosão. Atribua 'Explosion Prefab' no barril.");
                return null;
            }
            puffMaterial = new Material(shader);
            puffMaterial.mainTexture = puffTexture;
            if (puffMaterial.HasProperty("_BaseMap")) puffMaterial.SetTexture("_BaseMap", puffTexture);
            if (puffMaterial.HasProperty("_Surface")) puffMaterial.SetFloat("_Surface", 1f);
            return puffMaterial;
        }

        static Gradient Fade(Color a, Color b, Color c)
        {
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 0.35f), new GradientColorKey(c, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        static ParticleSystem Make(Transform parent, string name, bool loop, float duration, Vector2 life, Vector2 speed,
                                   Vector2 size, int burst, float rate, float shapeRadius, Gradient color, float gravity, float grow)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.duration = duration;
            main.loop = loop;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(20, burst * 2 + 40);

            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = rate;
            if (burst > 0) em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst) });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = shapeRadius;

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(color);

            if (grow != 1f)
            {
                ParticleSystem.SizeOverLifetimeModule sz = ps.sizeOverLifetime;
                sz.enabled = true;
                sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, grow));
            }

            ParticleSystemRenderer rend = go.GetComponent<ParticleSystemRenderer>();
            Material mat = PuffMaterial();
            if (mat != null) rend.sharedMaterial = mat;
            ps.Play();
            return ps;
        }

        /// <summary>Bola de fogo, fumaça, faíscas e um clarão de luz. Some sozinha em poucos segundos.</summary>
        public static void Explosion(Vector3 position, float scale)
        {
            GameObject root = new GameObject("ExplosionFX");
            root.transform.position = position;
            Object.Destroy(root, 5f);

            Make(root.transform, "Fire", false, 0.2f, new Vector2(0.5f, 0.9f), new Vector2(2f, 6f) * scale,
                 new Vector2(1.4f, 2.8f) * scale, 28, 0f, 0.3f * scale,
                 Fade(new Color(1f, 0.95f, 0.6f), new Color(1f, 0.45f, 0.1f), new Color(0.2f, 0.05f, 0f)), -0.05f, 1.4f);
            Make(root.transform, "Smoke", false, 0.3f, new Vector2(2f, 3.5f), new Vector2(0.5f, 2f) * scale,
                 new Vector2(2f, 4f) * scale, 16, 0f, 0.5f * scale,
                 Fade(new Color(0.25f, 0.22f, 0.2f), new Color(0.15f, 0.14f, 0.14f), new Color(0.05f, 0.05f, 0.05f)), -0.04f, 1.6f);
            ParticleSystem sparks = Make(root.transform, "Sparks", false, 0.15f, new Vector2(0.6f, 1.3f), new Vector2(6f, 14f) * scale,
                 new Vector2(0.05f, 0.1f), 36, 0f, 0.2f,
                 Fade(new Color(1f, 0.9f, 0.4f), new Color(1f, 0.5f, 0.1f), new Color(0.3f, 0.1f, 0f)), 1f, 1f);
            ParticleSystemRenderer sr = sparks.GetComponent<ParticleSystemRenderer>();
            sr.renderMode = ParticleSystemRenderMode.Stretch;
            sr.velocityScale = 0.06f;

            GameObject flash = new GameObject("Flash");
            flash.transform.SetParent(root.transform, false);
            Light light = flash.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.25f);
            light.range = 16f * scale;
            light.intensity = 7f * scale;
            light.shadows = LightShadows.None;
            Object.Destroy(flash, 0.16f);
        }

        /// <summary>Chama em loop. Quem chamou controla a vida (Stop / Destroy).</summary>
        public static ParticleSystem Fire(Transform parent, Vector3 localPosition, float scale)
        {
            ParticleSystem ps = Make(parent, "Fire", true, 1f, new Vector2(0.4f, 0.8f), new Vector2(0.6f, 1.6f) * scale,
                 new Vector2(0.3f, 0.55f) * scale, 0, 28f, 0.12f * scale,
                 Fade(new Color(1f, 0.85f, 0.35f), new Color(1f, 0.4f, 0.05f), new Color(0.15f, 0.03f, 0f)), -0.2f, 0.3f);
            ps.transform.localPosition = localPosition;
            return ps;
        }
    }
}
