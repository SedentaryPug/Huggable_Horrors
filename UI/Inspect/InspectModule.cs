using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Base das telas de inspeção 3D por jogador (item novo ao pegar, objeto do cenário): janela no meio da tela com o modelo girando
    /// (InspectStudio), cursor do mouse, rotação (arrastar, analógico direito, setas), zoom, trava do jogador enquanto aberta e fechamento
    /// seguro (dano, morte, cutscene, troca de fase). As filhas decidem o que a janela mostra e o que os botões fazem.
    /// </summary>
    public abstract class InspectModule : MonoBehaviour
    {
        protected const float PanelW = 920f;
        protected const float PanelH = 560f;
        protected const float ViewSize = 520f;

        protected PlayerOverlay overlay;
        protected PlayerContext player;
        protected ItemInput input;
        protected InspectStudio studio;

        protected RectTransform layer, panel, imageRect;
        protected Text titleText, nameText, descText, messageText, hintText, hoverText;
        protected Vector2 cursor;
        protected bool open;
        protected float openedAt;
        /// <summary>O clique que começou sobre a imagem terminou agora sem arrastar (ativa o ponto sob o cursor).</summary>
        protected bool clicked;
        /// <summary>Está arrastando o modelo com o mouse (mais que um clique).</summary>
        protected bool dragging;

        bool built, lockHeld, pointerOwned, prevVisible, damageHooked;
        CursorLockMode prevLock;
        int releaseFrame;
        bool pressing;
        float dragDistance;
        RawImage raw;

        protected abstract string LayerName { get; }
        protected virtual bool ArrowsRotate { get { return true; } }

        public bool IsOpen { get { return open; } }

        protected bool Pad { get { return player != null && player.Input != null && player.Input.IsGamepad; } }
        protected bool RealPointer { get { return input != null && input.UsesMouse && Mouse.current != null; } }

        // ------------------------------------------------------------------ construção
        /// <summary>Monta a janela (uma vez) e o estúdio. Falso se a tela do jogador ainda não existe.</summary>
        protected bool Build()
        {
            if (built) return true;
            overlay = PlayerRef.Find<PlayerOverlay>(this);
            if (overlay == null) return false;
            player = PlayerRef.ContextOf(this);
            input = PlayerRef.Find<ItemInput>(this);
            if (player == null || input == null) return false;
            built = true;

            studio = new InspectStudio(PlayerRef.IndexOf(this));
            layer = overlay.NewLayer(LayerName);
            Image dim = InvUI.Img(layer, "dim", new Color(0f, 0f, 0f, 0.62f));
            InvUI.Stretch(dim.rectTransform);

            panel = InvUI.Rect(layer, "panel");
            InvUI.Center(panel, PanelW, PanelH);
            Image bg = InvUI.Img(panel, "bg", InvUI.Panel);
            InvUI.Stretch(bg.rectTransform);

            imageRect = InvUI.Rect(panel, "view");
            InvUI.TopLeft(imageRect, 20f, 20f, ViewSize, ViewSize);
            GameObject rg = new GameObject("raw", typeof(RectTransform));
            rg.transform.SetParent(imageRect, false);
            raw = rg.AddComponent<RawImage>();
            raw.raycastTarget = false;
            InvUI.Stretch(raw.rectTransform);

            titleText = InvUI.Label(panel, "title", "", 11, TextAnchor.UpperLeft, InvUI.Accent);
            InvUI.TopLeft(titleText.rectTransform, 560f, 20f, 340f, 18f);
            nameText = InvUI.Label(panel, "name", "", 20, TextAnchor.UpperLeft, InvUI.TextColor);
            InvUI.TopLeft(nameText.rectTransform, 560f, 40f, 340f, 56f);
            descText = InvUI.Label(panel, "desc", "", 14, TextAnchor.UpperLeft, InvUI.TextColor);
            InvUI.TopLeft(descText.rectTransform, 560f, 104f, 340f, 250f);
            messageText = InvUI.Label(panel, "msg", "", 13, TextAnchor.UpperLeft, new Color(0.85f, 0.8f, 0.6f, 1f));
            InvUI.TopLeft(messageText.rectTransform, 560f, 360f, 340f, 80f);
            hintText = InvUI.Label(panel, "hint", "", 11, TextAnchor.LowerLeft, InvUI.TextDim);
            InvUI.TopLeft(hintText.rectTransform, 560f, 446f, 340f, 100f);

            hoverText = InvUI.Label(panel, "hover", "", 13, TextAnchor.MiddleCenter, Color.white);
            hoverText.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.95f);
            InvUI.Center(hoverText.rectTransform, 260f, 24f);
            hoverText.gameObject.SetActive(false);

            BuildExtra();
            layer.gameObject.SetActive(false);
            return true;
        }

        /// <summary>Gancho para a filha criar botões e outros elementos dentro de 'panel'.</summary>
        protected virtual void BuildExtra() { }

        // ------------------------------------------------------------------ abrir / fechar
        /// <summary>Chamado pela filha DEPOIS de mostrar o modelo no estúdio e de preencher os textos.</summary>
        protected void BeginWindow()
        {
            float scale = Mathf.Min(1f, (layer.rect.width - 16f) / PanelW, (layer.rect.height - 16f) / PanelH);
            if (scale <= 0f) scale = 1f;
            panel.localScale = new Vector3(scale, scale, 1f);
            raw.texture = studio.Texture;
            hoverText.gameObject.SetActive(false);
            messageText.text = "";
            pressing = false; dragging = false; clicked = false; dragDistance = 0f;

            prevLock = Cursor.lockState;
            prevVisible = Cursor.visible;
            if (!lockHeld) { player.AddControlLock(this); lockHeld = true; }
            if (player.Stats != null && !damageHooked) { player.Stats.Damaged += OnDamaged; damageHooked = true; }
            open = true;
            openedAt = Time.unscaledTime;
            layer.gameObject.SetActive(true);
            if (RealPointer) cursor = Mouse.current.position.ReadValue();
        }

        /// <summary>Fecha a janela. O jogador é solto dois quadros depois (o mesmo aperto que fecha não pode acionar o que está à frente).</summary>
        protected void EndWindow()
        {
            if (!open) return;
            open = false;
            if (damageHooked && player != null && player.Stats != null) { player.Stats.Damaged -= OnDamaged; damageHooked = false; }
            if (studio != null) studio.Hide();
            if (layer != null) layer.gameObject.SetActive(false);
            ReleasePointer();
            releaseFrame = Time.frameCount + 2;
            OnWindowClosed();
        }

        /// <summary>Chamado depois que a janela fechou, por qualquer motivo.</summary>
        protected virtual void OnWindowClosed() { }

        /// <summary>Fecha por culpa de fora (dano, morte, cutscene, troca de fase).</summary>
        protected virtual void CloseForced() { EndWindow(); }

        void OnDamaged(StatsComponent sc, DamageInfo info, float applied)
        {
            if (open && applied > 0f) CloseForced();
        }

        void OnDestroy()
        {
            if (damageHooked && player != null && player.Stats != null) player.Stats.Damaged -= OnDamaged;
            if (studio != null) studio.Hide();
            ReleasePointer();
            if (lockHeld && player != null) player.RemoveControlLock(this);
        }

        // ------------------------------------------------------------------ ponteiro
        void LateUpdate()
        {
            if (!open) return;
            if (RealPointer)
            {
                pointerOwned = true;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else ReleasePointer();
        }

        void ReleasePointer()
        {
            if (!pointerOwned) return;
            pointerOwned = false;
            Cursor.lockState = prevLock;
            Cursor.visible = prevVisible;
        }

        // ------------------------------------------------------------------ quadro a quadro
        void Update()
        {
            if (lockHeld && !open && Time.frameCount >= releaseFrame)
            {
                lockHeld = false;
                if (player != null) player.RemoveControlLock(this);
            }
            if (!open) return;

            if (input == null || Time.timeScale <= 0f || player == null || player.IsDead || SceneDirector.IsBusy || CutsceneDirector.IsPlaying)
            {
                CloseForced();
                return;
            }

            float dt = Time.unscaledDeltaTime;
            if (RealPointer) cursor = Mouse.current.position.ReadValue();
            HandleView(dt);
            OnTick(dt);
        }

        /// <summary>Roda enquanto a janela está aberta (depois de girar/zoom).</summary>
        protected abstract void OnTick(float dt);

        /// <summary>Passou tempo suficiente desde que abriu para um aperto de botão contar como "fechar" (o aperto que abriu não fecha).</summary>
        protected bool PastOpenGuard { get { return Time.unscaledTime - openedAt > 0.3f; } }

        // ------------------------------------------------------------------ girar / aproximar
        protected bool OverView { get { return RealPointer && InvUI.Hit(imageRect, cursor); } }

        /// <summary>Posição do cursor na imagem, de 0 a 1 (origem embaixo à esquerda, como a câmera do estúdio espera).</summary>
        protected Vector2 CursorInView()
        {
            Vector2 p;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(imageRect, cursor, null, out p);
            Rect r = imageRect.rect;
            return new Vector2((p.x - r.xMin) / r.width, (p.y - r.yMin) / r.height);
        }

        void HandleView(float dt)
        {
            clicked = false;
            bool over = OverView;
            Vector2 delta = RealPointer ? ItemInput.Vec(input.CursorDelta) : Vector2.zero;

            if (RealPointer)
            {
                if (ItemInput.Pressed(input.Select) && over) { pressing = true; dragDistance = 0f; }
                if (pressing && ItemInput.Held(input.Select))
                {
                    dragDistance += delta.magnitude;
                    if (dragDistance > 3f) studio.Rotate(-delta.x * 0.4f, delta.y * 0.4f);
                }
                dragging = pressing && dragDistance > 6f;
                if (ItemInput.Released(input.Select))
                {
                    clicked = pressing && dragDistance <= 6f;
                    pressing = false;
                    dragging = false;
                }
            }

            Vector2 rs = ItemInput.Vec(input.RightStick);
            if (rs.sqrMagnitude > 0.04f) studio.Rotate(-rs.x * 170f * dt, rs.y * 170f * dt);
            if (ArrowsRotate)
            {
                Vector2 nav = new Vector2((ItemInput.Held(input.NavRight) ? 1f : 0f) - (ItemInput.Held(input.NavLeft) ? 1f : 0f),
                                          (ItemInput.Held(input.NavUp) ? 1f : 0f) - (ItemInput.Held(input.NavDown) ? 1f : 0f));
                if (nav.sqrMagnitude > 0f) studio.Rotate(-nav.x * 170f * dt, nav.y * 170f * dt);
            }

            float scroll = ItemInput.Vec(input.Scroll).y;
            if (Mathf.Abs(scroll) > 0.01f) studio.Zoom(Mathf.Sign(scroll) * 0.08f);
            if (ItemInput.Held(input.Rotate)) studio.Zoom(0.9f * dt);
            if (ItemInput.Held(input.RotateBack)) studio.Zoom(-0.9f * dt);
        }

        /// <summary>Texto da tecla/botão de uma action para o dispositivo atual.</summary>
        protected string Glyph(InputAction action)
        {
            return InputGlyphs.GetBindingTextForDevice(action, Pad);
        }

        /// <summary>Coloca o texto de dica ao lado do cursor (mouse).</summary>
        protected void ShowHover(string text)
        {
            hoverText.gameObject.SetActive(true);
            hoverText.text = text;
            Vector2 lp;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)hoverText.rectTransform.parent, cursor, null, out lp);
            hoverText.rectTransform.anchoredPosition = lp + new Vector2(0f, 26f);
        }

        protected void HideHover() { hoverText.gameObject.SetActive(false); }
    }
}
