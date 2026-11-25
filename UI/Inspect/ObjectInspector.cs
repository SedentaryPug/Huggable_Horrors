using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Tela de inspeção de um objeto do cenário (InspectableObject): o objeto gira (arrastar com o mouse, analógico direito, setas) e os
    /// SceneHotspot dele podem ser acionados:
    ///  - mouse: passe o cursor sobre o ponto (aparece o texto) e clique;
    ///  - controle: um marcador aparece em cada ponto que está de frente para a câmera; o mais perto do centro fica em destaque, com
    ///    "[botão] texto" embaixo da imagem, e o botão A (Select) o aciona.
    /// Fecha com Voltar (Backspace / B), Tab, I, botão direito / X, ao levar dano, ao morrer ou ao começar uma cutscene.
    /// De código: ObjectInspector.OpenFor(jogador, objeto).
    /// </summary>
    [AddComponentMenu("HorrorFPS/UI/Object Inspector")]
    [PlayerModule("Interface", 8)]
    [RequireComponent(typeof(PlayerOverlay))]
    public class ObjectInspector : InspectModule
    {
        const float MarkerSize = 12f;

        InspectableObject target;
        Text promptText;
        readonly List<Image> markers = new List<Image>();
        readonly List<SceneHotspot> seen = new List<SceneHotspot>();
        readonly List<Vector2> seenPos = new List<Vector2>();
        string message = "";

        protected override string LayerName { get { return "ObjectInspector"; } }

        public static bool OpenFor(PlayerContext player, InspectableObject obj)
        {
            if (player == null || obj == null) return false;
            ObjectInspector m = player.GetComponentInChildren<ObjectInspector>(true);
            return m != null && m.Show(obj);
        }

        void OnDisable()
        {
            if (open) CloseForced();
        }

        protected override void BuildExtra()
        {
            // a dica do controle fica embaixo da imagem, por cima dela
            promptText = InvUI.Label(imageRect, "prompt", "", 14, TextAnchor.MiddleCenter, Color.white);
            promptText.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.95f);
            InvUI.TopLeft(promptText.rectTransform, 10f, ViewSize - 44f, ViewSize - 20f, 30f);
            promptText.gameObject.SetActive(false);
        }

        bool Show(InspectableObject obj)
        {
            if (open || !Build()) return false;
            if (!studio.ShowObject(obj.ModelSource)) return false;

            target = obj;
            for (int i = 0; i < studio.Hotspots.Count; i++) studio.Hotspots[i].ownerKey = obj.SaveKey;
            message = "";
            titleText.text = UIText.Examine.ToUpperInvariant();
            nameText.text = obj.Title;
            descText.text = obj.Description;
            messageText.text = "";
            hintText.text = string.Format(UIText.InspectHintObject, Glyph(input.Cancel));
            promptText.gameObject.SetActive(false);
            BeginWindow();
            obj.RaiseOpened();
            return true;
        }

        protected override void OnWindowClosed()
        {
            for (int i = 0; i < markers.Count; i++) if (markers[i] != null) markers[i].gameObject.SetActive(false);
            if (promptText != null) promptText.gameObject.SetActive(false);
            InspectableObject t = target;
            target = null;
            if (t != null) t.RaiseClosed();
        }

        protected override void OnTick(float dt)
        {
            // fechar
            if (PastOpenGuard && (ItemInput.Pressed(input.Cancel) || ItemInput.Pressed(input.Toggle) || ItemInput.Pressed(input.ToggleBag)
                                  || ItemInput.Pressed(input.Secondary)))
            {
                EndWindow();
                return;
            }
            if (target == null) { EndWindow(); return; }

            IList<SceneHotspot> all = studio.Hotspots;
            SceneHotspot hovered = null;
            if (RealPointer && OverView && !dragging) hovered = studio.RaycastScene(CursorInView());

            // pontos disponíveis que estão à mostra (de frente, sem nada na frente): ganham um marcador
            seen.Clear();
            seenPos.Clear();
            SceneHotspot aimed = null;
            float aimedDist = float.MaxValue;
            for (int i = 0; i < all.Count; i++)
            {
                SceneHotspot h = all[i];
                Vector2 vp;
                if (h == null || !h.IsAvailable() || !studio.IsVisible(h, out vp)) continue;
                seen.Add(h);
                seenPos.Add(vp);
                // controle: o ponto mais perto do centro (dentro do raio dele) é o "mirado"
                float dist = (vp - new Vector2(0.5f, 0.5f)).magnitude;
                if (dist <= h.aimRadius && dist < aimedDist) { aimedDist = dist; aimed = h; }
            }

            for (int i = 0; i < seen.Count; i++)
            {
                Image m = Marker(i);
                Vector2 vp = seenPos[i];
                bool hot = RealPointer ? seen[i] == hovered : seen[i] == aimed;
                float size = hot ? MarkerSize * 1.5f : MarkerSize;
                InvUI.TopLeft(m.rectTransform, vp.x * ViewSize - size * 0.5f, (1f - vp.y) * ViewSize - size * 0.5f, size, size);
                m.color = hot ? Color.white : new Color(InvUI.Accent.r, InvUI.Accent.g, InvUI.Accent.b, 0.6f);
            }
            for (int i = seen.Count; i < markers.Count; i++) markers[i].gameObject.SetActive(false);

            // texto: mouse = ao lado do cursor; controle = embaixo da imagem
            SceneHotspot active = RealPointer ? hovered : aimed;
            if (RealPointer)
            {
                promptText.gameObject.SetActive(false);
                if (active != null) ShowHover(active.label); else HideHover();
            }
            else
            {
                HideHover();
                if (active != null)
                {
                    promptText.gameObject.SetActive(true);
                    promptText.text = string.Format(UIText.InspectPromptPad, Glyph(input.Select), active.label);
                }
                else promptText.gameObject.SetActive(false);
            }

            // acionar
            bool fire = RealPointer ? (clicked && hovered != null) : (ItemInput.Pressed(input.Select) && PastOpenGuard && aimed != null);
            if (fire)
            {
                string msg = active.Activate(player);
                if (!string.IsNullOrEmpty(msg)) message = msg;
                messageText.text = message;
            }

            // a descrição pode ser trocada por eventos (ex.: depois de abrir a tampa): o texto do objeto é lido de novo
            if (Time.frameCount % 15 == 0 && target != null) descText.text = target.Description;
        }

        Image Marker(int index)
        {
            while (markers.Count <= index)
            {
                Image m = InvUI.Img(imageRect, "marker", InvUI.Accent);
                m.raycastTarget = false;
                markers.Add(m);
            }
            markers[index].gameObject.SetActive(true);
            return markers[index];
        }
    }
}
