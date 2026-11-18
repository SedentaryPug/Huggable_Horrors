using System;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Página "Como jogar" do menu de pausa: todas as dicas de comandos, com os botões do jogador que abriu o menu (no dispositivo que ele
    /// está usando). É onde o jogador relê o que a tela já mostrou uma vez. Tem o botão para ligar/desligar as dicas na tela e para
    /// vê-las de novo.
    /// </summary>
    public class HowToPlayPanel : InfoPanelBase
    {
        const string DimHex = "#B5B0A8";

        readonly Text tipsLabel;

        public HowToPlayPanel(RectTransform pageRoot, Action onBack) : base(pageRoot)
        {
            AddBottomButton(TipsText(), delegate { ToggleTips(); }, 400f, out tipsLabel);
            Text unused;
            AddBottomButton(UIText.TipsReplay, delegate { ReplayTips(); }, 200f, out unused);
            AddBackButton(onBack);
        }

        static string TipsText()
        {
            if (!GameSettings.TipsEnabled) return UIText.TipsOff;
            return GameSettings.TipsPause ? UIText.TipsOnPause : UIText.TipsOn;
        }

        /// <summary>Desligado → Ligado → Ligado e pausa → Desligado.</summary>
        void ToggleTips()
        {
            if (!GameSettings.TipsEnabled) { GameSettings.TipsPause = false; GameSettings.TipsEnabled = true; }
            else if (!GameSettings.TipsPause) GameSettings.TipsPause = true;
            else GameSettings.TipsEnabled = false;
            tipsLabel.text = TipsText();
        }

        void ReplayTips()
        {
            Tutorials.ResetSeen();
            statusLabel.text = UIText.TipsReplayDone;
        }

        /// <summary>Remonta a lista com os botões do jogador (chame ao abrir a página).</summary>
        public void Refresh(PlayerContext player)
        {
            ClearEntries();
            statusLabel.text = string.Empty;
            tipsLabel.text = TipsText();

            bool any = false;
            System.Collections.Generic.IList<TutorialEntry> all = Tutorials.All;
            for (int i = 0; i < all.Count; i++)
            {
                TutorialEntry e = all[i];
                if (!e.enabled || !e.inMenu) continue;
                any = true;
                string title = string.IsNullOrEmpty(e.title) ? e.id : e.title;
                AddEntry("<b>" + title + "</b>\n<size=21><color=" + DimHex + ">" + TutorialText.Resolve(e.text, player) + "</color></size>");
            }
            if (!any) AddMessage(UIText.HowToPlayEmpty);
        }
    }
}
