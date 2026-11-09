using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Lista de slots de save, com miniatura, nível, data e tempo de jogo. No modo Save mostra os slots manuais e salva
    /// (pede um segundo clique para sobrescrever); no modo Load mostra também o salvamento automático e carrega via
    /// SceneDirector. Apagar também pede um segundo clique. Funciona com mouse, teclado e controle.
    /// </summary>
    public class SaveSlotsPanel
    {
        public enum Mode
        {
            Save,
            Load
        }

        const float RowHeight = 128f;

        readonly RectTransform content;
        readonly Button backButton;
        readonly List<Texture2D> textures = new List<Texture2D>();
        readonly Button[] primaryButtons = new Button[SaveSystem.SlotCount];
        readonly Button[] deleteButtons = new Button[SaveSystem.SlotCount];

        Mode mode;
        bool visible;
        int armedSlot = -1;
        bool armedDelete;

        public SaveSlotsPanel(RectTransform pageRoot, Action onBack)
        {
            ScrollRect scroll;
            content = UIFactory.CreateScrollList(pageRoot, new Vector2(800f, 400f), out scroll);
            RectTransform scrollRt = (RectTransform)scroll.transform;
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(0f, 90f);
            scrollRt.offsetMax = Vector2.zero;

            RectTransform bottom = UIFactory.Anchored("Buttons", pageRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                Vector2.zero, new Vector2(300f, 70f));
            backButton = UIFactory.CreateButton(bottom, UIText.Back, new Vector2(300f, 64f),
                delegate { if (onBack != null) onBack(); });
        }

        /// <summary>Primeiro botão que dá para usar (para a seleção inicial do teclado/controle).</summary>
        public Selectable FirstSelectable
        {
            get
            {
                for (int i = 0; i < SaveSystem.SlotCount; i++)
                {
                    if (primaryButtons[i] != null && primaryButtons[i].interactable) return primaryButtons[i];
                    if (deleteButtons[i] != null && deleteButtons[i].interactable) return deleteButtons[i];
                }
                return backButton;
            }
        }

        public void Show(Mode newMode)
        {
            mode = newMode;
            visible = true;
            armedSlot = -1;
            armedDelete = false;
            Rebuild(-1, false);
        }

        /// <summary>Esconde a página e libera as miniaturas da memória.</summary>
        public void Release()
        {
            visible = false;
            ClearRows();
        }

        // ------------------------------------------------------------------ linhas

        void ClearRows()
        {
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Transform child = content.GetChild(i);
                child.SetParent(null, false); // sai do layout imediatamente
                UnityEngine.Object.Destroy(child.gameObject);
            }
            for (int i = 0; i < textures.Count; i++)
                if (textures[i] != null) UnityEngine.Object.Destroy(textures[i]);
            textures.Clear();
            Array.Clear(primaryButtons, 0, primaryButtons.Length);
            Array.Clear(deleteButtons, 0, deleteButtons.Length);
        }

        void Rebuild(int selectSlot, bool selectDelete)
        {
            ClearRows();

            int firstSlot = mode == Mode.Save ? SaveSystem.AutosaveSlot + 1 : SaveSystem.AutosaveSlot;
            for (int slot = firstSlot; slot < SaveSystem.SlotCount; slot++)
                AddRow(SaveSystem.GetInfo(slot));

            Selectable target = null;
            if (selectSlot >= 0)
            {
                Button wanted = selectDelete ? deleteButtons[selectSlot] : primaryButtons[selectSlot];
                Button other = selectDelete ? primaryButtons[selectSlot] : deleteButtons[selectSlot];
                target = wanted != null ? wanted : other;
            }
            if (target == null) target = FirstSelectable;
            if (target != null && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        void AddRow(SaveSlotInfo info)
        {
            RectTransform row = UIFactory.NewRect("Slot" + info.slot, content);
            UIFactory.SetLayout(row.gameObject, -1f, RowHeight);
            UIFactory.AddImage(row.gameObject, UISprites.RoundedRect, new Color(1f, 1f, 1f, 0.05f));

            // miniatura
            RectTransform thumb = UIFactory.Anchored("Thumb", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(12f, 0f), new Vector2(128f, 72f));
            UIFactory.AddImage(thumb.gameObject, null, new Color(0f, 0f, 0f, 0.55f));
            if (info.exists && !info.corrupted)
            {
                Texture2D texture = SaveSystem.LoadThumbnail(info.slot);
                if (texture != null)
                {
                    textures.Add(texture);
                    RectTransform imageRt = UIFactory.Stretch("Image", thumb);
                    RawImage raw = imageRt.gameObject.AddComponent<RawImage>();
                    raw.texture = texture;
                    raw.raycastTarget = false;
                }
            }

            // textos
            RectTransform textRt = UIFactory.Stretch("Text", row);
            textRt.offsetMin = new Vector2(152f, 6f);
            textRt.offsetMax = new Vector2(-292f, -6f);
            Text text = UIFactory.AddText(textRt.gameObject, Describe(info), 22, TextAnchor.MiddleLeft, UIFactory.TextColor);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = true;

            // botões
            RectTransform buttons = UIFactory.Anchored("Buttons", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-12f, 0f), new Vector2(268f, 56f));
            HorizontalLayoutGroup h = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 8f;
            h.childAlignment = TextAnchor.MiddleRight;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = true;

            SaveSlotInfo captured = info;
            if (mode == Mode.Save)
            {
                bool armed = armedSlot == info.slot && !armedDelete;
                Button save = AddRowButton(buttons, armed ? UIText.ConfirmOverwrite : UIText.SlotSave, 150f, armed,
                    delegate { OnPrimary(captured); });
                save.interactable = SaveSystem.CanSave;
                primaryButtons[info.slot] = save;
            }
            else if (info.exists && !info.corrupted)
            {
                primaryButtons[info.slot] = AddRowButton(buttons, UIText.SlotLoad, 150f, false,
                    delegate { OnPrimary(captured); });
            }

            if (info.exists)
            {
                bool armed = armedSlot == info.slot && armedDelete;
                deleteButtons[info.slot] = AddRowButton(buttons, armed ? UIText.ConfirmDelete : UIText.SlotDelete, 110f, armed,
                    delegate { OnDelete(captured); });
            }
        }

        static Button AddRowButton(RectTransform parent, string label, float width, bool armed, UnityAction onClick)
        {
            Text labelText;
            Button b = UIFactory.CreateButton(parent, label, new Vector2(width, 56f), onClick, out labelText);
            UIFactory.SetLayout(b.gameObject, width, 56f);
            labelText.fontSize = armed ? 18 : 22;
            if (armed) labelText.color = UIFactory.AccentColor;
            return b;
        }

        // ------------------------------------------------------------------ ações

        void OnPrimary(SaveSlotInfo info)
        {
            if (mode == Mode.Load)
            {
                SceneDirector.LoadGame(info.slot);
                return;
            }

            // salvar: sobrescrever um slot ocupado pede confirmação
            bool armedHere = armedSlot == info.slot && !armedDelete;
            if (info.exists && !armedHere)
            {
                armedSlot = info.slot;
                armedDelete = false;
                Rebuild(info.slot, false);
                return;
            }

            armedSlot = -1;
            armedDelete = false;
            int slot = info.slot;
            SaveSystem.SaveToSlot(slot, delegate (bool ok)
            {
                if (visible) Rebuild(slot, false);
            });
        }

        void OnDelete(SaveSlotInfo info)
        {
            bool armedHere = armedSlot == info.slot && armedDelete;
            if (!armedHere)
            {
                armedSlot = info.slot;
                armedDelete = true;
                Rebuild(info.slot, true);
                return;
            }

            armedSlot = -1;
            armedDelete = false;
            SaveSystem.Delete(info.slot);
            Rebuild(-1, false);
        }

        // ------------------------------------------------------------------ texto

        static string Describe(SaveSlotInfo info)
        {
            string title = info.IsAutosave ? UIText.AutosaveSlotName : string.Format(UIText.SlotName, info.slot);
            string head = "<size=26><b>" + title + "</b></size>";
            string dim = ColorUtility.ToHtmlStringRGB(UIFactory.DimTextColor);

            if (!info.exists) return head + "\n<color=#" + dim + ">" + UIText.EmptySlot + "</color>";
            if (info.corrupted)
                return head + "\n<color=#" + ColorUtility.ToHtmlStringRGB(UIFactory.AccentColor) + ">" + UIText.CorruptSlot + "</color>";

            SaveHeader h = info.header;
            string level = string.IsNullOrEmpty(h.levelName) ? h.levelId : h.levelName;
            if (h.players > 1) level += "  ·  " + string.Format(UIText.PlayersCount, h.players);
            string details = h.savedAt + "  ·  " + string.Format(UIText.PlayTime, FormatTime(h.playTime));
            return head + "\n" + level + "\n<size=19><color=#" + dim + ">" + details + "</color></size>";
        }

        static string FormatTime(float seconds)
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return string.Format("{0:00}:{1:00}:{2:00}", total / 3600, (total / 60) % 60, total % 60);
        }
    }
}
