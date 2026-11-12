using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    // Parte "animações e sons" da tela de inventário.
    // Animações: encaixou (flash nas células, anel, eco assentando, "+N" ao juntar pilhas), não coube (flash vermelho, peça balançando),
    // cancelou (a peça volta voando ao lugar), atalho (a peça voa até o número e ele pulsa), jogar fora, bandeja, organizar, janela abrindo.
    // Sons: cada momento chama Play(); o evento SoundPlayed sai SEMPRE e, se houver clipe no campo "Sounds", ele toca (2D).
    public partial class InventoryScreen
    {
        [Header("Animações")]
        [Tooltip("Liga as animações do inventário (encaixou, não coube, voltar ao lugar, menu, janela). Desligado = tudo instantâneo.")]
        [SerializeField] bool animations = true;
        [Tooltip("Velocidade das animações: 1 = normal, 2 = o dobro (mais rápido), 0.5 = metade.")]
        [SerializeField, Range(0.5f, 3f)] float animationSpeed = 1f;
        [Tooltip("Quantos pixels a peça (ou o atalho) balança de um lado ao outro quando NÃO cabe. 0 = não balança.")]
        [SerializeField, Range(0f, 24f)] float shakePixels = 9f;

        [Header("Sons (opcional)")]
        [Tooltip("Um clipe por momento (todos opcionais). O evento SoundPlayed avisa mesmo sem clipe: dá para ligar FMOD, Wwise ou o seu áudio.")]
        [SerializeField] InventorySounds sounds = new InventorySounds();

        static readonly Color FxGood = new Color(0.45f, 0.95f, 0.5f, 1f);
        static readonly Color FxMerge = new Color(0.55f, 0.82f, 1f, 1f);
        static readonly Color FxSwap = new Color(1f, 0.82f, 0.3f, 1f);
        static readonly Color FxBad = new Color(1f, 0.3f, 0.25f, 1f);

        RectTransform fxLayer;
        InvFx fx;
        CanvasGroup windowGroup;
        Image dimImage;
        float openT = 1f;                       // 0..1: janela abrindo (fade)
        float shakeT = 1f;                      // 0..1: peça na mão balançando porque não coube (1 = parada)
        bool lastDropRejected;                  // a última tentativa de soltar foi recusada
        AudioSource sfx;

        readonly List<Rect> fxCells = new List<Rect>();     // onde a peça cairia (calculado ANTES de soltar)
        Rect fxBox;
        bool fxHaveBox;

        /// <summary>Avisa cada som do inventário (mesmo sem clipe configurado). Ligue aqui o seu sistema de áudio.</summary>
        public event Action<InvSound> SoundPlayed;

        bool FxOn { get { return animations && fx != null && open; } }

        static Color WithAlpha(Color c, float a) { return new Color(c.r, c.g, c.b, a); }

        // ------------------------------------------------------------------ som
        void Play(InvSound s)
        {
            Action<InvSound> h = SoundPlayed;
            if (h != null)
            {
                try { h(s); }
                catch (Exception e) { Debug.LogException(e); }       // um ouvinte com erro não pode travar o inventário
            }
            if (sounds == null || !isActiveAndEnabled) return;
            AudioClip clip = sounds.Get(s);
            if (clip == null) return;
            if (sfx == null)
            {
                sfx = gameObject.AddComponent<AudioSource>();
                sfx.playOnAwake = false;
                sfx.spatialBlend = 0f;
            }
            sfx.PlayOneShot(clip, sounds.volume);
        }

        // ------------------------------------------------------------------ camada de efeitos
        void BuildFx()
        {
            fxLayer = InvUI.Rect(layer, "fx");
            InvUI.Stretch(fxLayer);
            fx = new InvFx(fxLayer);
            fx.Speed = animationSpeed;
            windowGroup = window.gameObject.AddComponent<CanvasGroup>();
            dimImage = dimObject != null ? dimObject.GetComponent<Image>() : null;
            openT = animations ? 0f : 1f;
            ApplyOpenFade();
        }

        void DestroyFx()
        {
            if (fx != null) fx.Clear();
            if (fxLayer != null) Destroy(fxLayer.gameObject);
            fx = null; fxLayer = null; windowGroup = null; dimImage = null;
            fxCells.Clear();
            fxHaveBox = false;
            shakeT = 1f;
        }

        void ApplyOpenFade()
        {
            float e = InvFx.EaseOut(Mathf.Clamp01(openT));
            if (windowGroup != null) windowGroup.alpha = e;
            if (dimImage != null) dimImage.color = new Color(0f, 0f, 0f, 0.55f * e);
        }

        /// <summary>Todo quadro com a tela aberta: janela abrindo, efeitos em andamento e animação do menu.</summary>
        void TickFx(float dt)
        {
            if (fx != null)
            {
                fx.Speed = animationSpeed;
                fx.Tick(dt);
            }
            menu.Animated = animations;
            menu.Speed = animationSpeed;
            menu.Animate(dt);
            if (openT < 1f)
            {
                openT = animations ? Mathf.Min(1f, openT + dt * animationSpeed / 0.14f) : 1f;
                ApplyOpenFade();
            }
        }

        // ------------------------------------------------------------------ ajudantes
        ContainerView ViewOf(ItemContainer c)
        {
            if (c == null) return null;
            for (int i = 0; i < views.Count; i++) if (views[i].Container == c) return views[i];
            return null;
        }

        /// <summary>Centro, escala e giro da peça na mão, na camada de efeitos (as cópias saem de onde ela está).</summary>
        bool GhostPose(out Vector2 center, out float scale, out float rotZ)
        {
            center = Vector2.zero; scale = 1f; rotZ = 0f;
            if (ghost == null || fxLayer == null) return false;
            Vector3 w = ghost.TransformPoint(ghost.rect.center);
            InvUI.ToTopLeftLocal(fxLayer, new Vector2(w.x, w.y), out center);
            scale = ghost.localScale.x;
            rotZ = Mathf.DeltaAngle(0f, ghost.localEulerAngles.z);
            return true;
        }

        /// <summary>Faz uma cópia da peça na mão voar até 'target' (centro), chegando com ~'targetSize' px no lado maior, e sumir.</summary>
        void FxFlyHeld(Vector2 target, float targetSize, float dur, float alphaEnd)
        {
            Vector2 c; float sc, rz;
            if (!FxOn || held == null || !GhostPose(out c, out sc, out rz)) return;
            Vector2Int size = held.data.shape.GetSize(heldRot);
            float biggest = Mathf.Max(size.x, size.y) * ghostCell;
            float toScale = Mathf.Clamp(targetSize / Mathf.Max(1f, biggest), 0.12f, 1.2f);
            fx.Fly(held, heldRot, ghostCell, c, sc, rz, target, toScale, 0.85f, alphaEnd, 0f, dur);
        }

        // ------------------------------------------------------------------ soltou na grade / lista / bandeja
        /// <summary>Guarda ONDE a peça cairia (mesma conta da prévia). Chamar antes de soltar: depois a grade já foi redesenhada.</summary>
        void FxCapture(ContainerView hv)
        {
            fxCells.Clear();
            fxHaveBox = false;
            fxBox = new Rect();
            if (!FxOn || hv == null || held == null) return;
            fxHaveBox = hv.GetDropFootprint(held, heldRot, cursor, fxLayer, fxCells, out fxBox);
        }

        /// <summary>Encaixou (ou juntou, ou trocou): flash verde nas células, anel e a peça "assentando" por cima.</summary>
        void FxPlaced(ContainerView hv, ItemInstance it, int rot, TransferResult r, int stackBefore)
        {
            if (!FxOn || it == null || it.data == null) return;
            Color c = r == TransferResult.Merged ? FxMerge : (r == TransferResult.Swapped ? FxSwap : FxGood);
            if (fxHaveBox)
            {
                for (int i = 0; i < fxCells.Count; i++) fx.Flash(fxCells[i], WithAlpha(c, 0.6f), 0.32f, 0f);
                fx.Frame(fxBox, c, 2f, 0.36f, 9f);
                if (r == TransferResult.Merged)
                {
                    int moved = stackBefore - Mathf.Max(0, it.stack);
                    if (moved > 0) fx.Float(fxBox.center, "+" + moved, c, 26f, 0.7f);
                }
                else
                {
                    Vector2Int size = it.data.shape.GetSize(rot);
                    float cell = size.x > 0 ? fxBox.width / size.x : 0f;
                    if (cell > 0f) fx.Settle(it, rot, fxBox.center, cell, 1.22f, 0.85f, 0.26f);
                }
            }
            else if (hv != null && hv.Root != null && !(hv is TrayView))
            {
                fx.Flash(InvUI.RectInLayer(fxLayer, hv.Root), WithAlpha(c, 0.16f), 0.28f, 0f);
            }
        }

        /// <summary>Soltou e NÃO coube: flash vermelho onde tentou, anel e a peça balança. Marca a tentativa como recusada.</summary>
        void FxRejected(ContainerView hv)
        {
            lastDropRejected = true;
            Play(InvSound.DropReject);
            if (!FxOn) return;
            if (fxHaveBox)
            {
                for (int i = 0; i < fxCells.Count; i++) fx.Flash(fxCells[i], WithAlpha(FxBad, 0.7f), 0.38f, 0f);
                fx.Frame(fxBox, FxBad, 2f, 0.32f, 5f);
            }
            else if (hv != null && hv.Root != null)
            {
                fx.Flash(InvUI.RectInLayer(fxLayer, hv.Root), WithAlpha(FxBad, 0.2f), 0.3f, 0f);
            }
            shakeT = 0f;
        }

        /// <summary>A peça na mão balança (e o som de "não") sem desenhar nada no destino.</summary>
        void FxShakeHeld()
        {
            lastDropRejected = true;
            Play(InvSound.DropReject);
            if (FxOn) shakeT = 0f;
        }

        /// <summary>Largou a peça logo após recusar (vai para a bandeja): uma cópia balança e some no lugar onde a peça estava.</summary>
        void FxRejectEcho()
        {
            Vector2 c; float sc, rz;
            if (!FxOn || held == null || !GhostPose(out c, out sc, out rz)) return;
            fx.ShakeOut(held, heldRot, ghostCell, c, sc, rz, shakePixels, 0.34f);
        }

        // ------------------------------------------------------------------ atalho, jogar fora, bandeja, voltar
        void FxSlot(int slot, bool good)
        {
            if (!FxOn || slot < 0 || slot >= slotRects.Length || slotRects[slot] == null) return;
            RectTransform rt = slotRects[slot];
            Rect r = InvUI.RectInLayer(fxLayer, rt);                 // antes do pulso: a escala ainda é 1
            Color c = good ? InvUI.Accent : FxBad;
            fx.Flash(r, WithAlpha(c, 0.6f), 0.34f, 0f);
            fx.Frame(r, c, 2f, 0.34f, good ? 8f : 4f);
            if (good) fx.Punch(rt, 1.3f, 0.3f);
            else fx.Shake(rt, shakePixels, 0.32f);
        }

        void FxFlyHeldToSlot(int slot)
        {
            if (!FxOn || slot < 0 || slot >= slotRects.Length || slotRects[slot] == null) return;
            Rect r = InvUI.RectInLayer(fxLayer, slotRects[slot]);
            FxFlyHeld(r.center, Mathf.Min(r.width, r.height) * 0.8f, 0.2f, 0f);
        }

        void FxZone(bool accepted)
        {
            if (!FxOn || dropZone == null) return;
            Rect r = InvUI.RectInLayer(fxLayer, dropZone);
            fx.Flash(r, WithAlpha(FxBad, accepted ? 0.5f : 0.35f), 0.35f, 0f);
            if (!accepted) fx.Shake(dropZone, shakePixels, 0.32f);
        }

        void FxFlyHeldToZone()
        {
            if (!FxOn || dropZone == null) return;
            Rect r = InvUI.RectInLayer(fxLayer, dropZone);
            FxFlyHeld(r.center, r.height * 0.6f, 0.24f, 0f);
        }

        /// <summary>A peça voa até a bandeja (soltou fora de qualquer contêiner): entra pelo ponto mais próximo, menor, como as peças de lá.</summary>
        void FxFlyHeldToTray()
        {
            Vector2 c; float sc, rz;
            if (!FxOn || tray == null || held == null || !GhostPose(out c, out sc, out rz)) return;
            Rect tr = InvUI.RectInLayer(fxLayer, tray.Root);
            Vector2 target = new Vector2(Mathf.Clamp(c.x, tr.xMin, tr.xMax), Mathf.Clamp(c.y, tr.yMin, tr.yMax));
            float trayCell = tray.CellSize * (window != null ? window.localScale.x : 1f);
            float toScale = Mathf.Clamp(trayCell / Mathf.Max(1f, ghostCell), 0.2f, 1.2f);
            fx.Fly(held, heldRot, ghostCell, c, sc, rz, target, toScale, 0.85f, 0f, 0f, 0.16f);
        }

        /// <summary>Cancelou o arrasto: a peça na mão volta voando ao lugar de onde saiu (e o lugar dela "acende" de novo).</summary>
        void ReturnHeld()
        {
            if (held != null)
            {
                Play(InvSound.ReturnToOrigin);
                Vector2 c; float sc, rz; Rect box;
                ContainerView origin = ViewOf(held.container);
                if (FxOn && origin != null && GhostPose(out c, out sc, out rz) && origin.GetItemBox(held, fxLayer, out box))
                    fx.Fly(held, heldRot, ghostCell, c, sc, rz, box.center, 1f, 0.85f, 0f, 0f, 0.18f);
            }
            ClearHold();
        }

        // ------------------------------------------------------------------ organizar
        /// <summary>Organizou a grade: cada peça pulsa em sequência (de leve), mostrando que foi reposicionada.</summary>
        void FxOrganized(GridContainer grid)
        {
            Play(InvSound.Organize);
            if (!FxOn || grid == null) return;
            IReadOnlyList<ItemInstance> items = grid.Items;
            int n = Mathf.Min(items.Count, 24);
            for (int i = 0; i < n; i++)
            {
                ItemInstance it = items[i];
                fx.After(0.06f + 0.025f * i, () => FxPulseItem(it));
            }
        }

        void FxPulseItem(ItemInstance it)
        {
            if (!FxOn || it == null || it.container == null) return;
            ContainerView v = ViewOf(it.container);
            Rect box;
            if (v == null || !v.GetItemBox(it, fxLayer, out box)) return;
            fx.Flash(box, WithAlpha(FxGood, 0.3f), 0.3f, 0f);
            fx.Frame(box, FxGood, 1.5f, 0.28f, 4f);
        }
    }
}
