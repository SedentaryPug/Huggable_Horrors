using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Página "Objetivos" do menu de pausa: o que está em andamento (com a descrição e o progresso), o que já foi concluído e o que falhou.
    /// Somente leitura; a lista é a mesma para todos os jogadores.
    /// </summary>
    public class ObjectivesPanel : InfoPanelBase
    {
        const string DimHex = "#A8A39B";
        const string DoneHex = "#7FD98A";
        const string FailHex = "#E0604F";

        readonly List<ObjectiveEntry> scratch = new List<ObjectiveEntry>();

        public ObjectivesPanel(RectTransform pageRoot, Action onBack) : base(pageRoot)
        {
            AddBackButton(onBack);
        }

        /// <summary>Remonta a lista com o estado atual (chame ao abrir a página).</summary>
        public void Refresh()
        {
            ClearEntries();
            statusLabel.text = string.Empty;
            bool any = false;

            scratch.Clear();
            Objectives.Collect(ObjectiveState.Active, scratch);
            if (scratch.Count > 0)
            {
                any = true;
                AddHeader(UIText.ObjectivesActive);
                for (int i = 0; i < scratch.Count; i++) AddEntry(ActiveText(scratch[i]));
            }

            scratch.Clear();
            Objectives.Collect(ObjectiveState.Done, scratch);
            if (scratch.Count > 0)
            {
                any = true;
                AddHeader(UIText.ObjectivesDone);
                for (int i = 0; i < scratch.Count; i++)
                    AddEntry("<color=" + DoneHex + ">" + Title(scratch[i]) + "</color>");
            }

            scratch.Clear();
            Objectives.Collect(ObjectiveState.Failed, scratch);
            if (scratch.Count > 0)
            {
                any = true;
                AddHeader(UIText.ObjectivesFailedSection);
                for (int i = 0; i < scratch.Count; i++)
                    AddEntry("<color=" + FailHex + ">" + Title(scratch[i]) + "</color>");
            }

            if (!any) AddMessage(UIText.ObjectivesEmpty);
        }

        static string Title(ObjectiveEntry e)
        {
            return string.IsNullOrEmpty(e.title) ? e.id : e.title;
        }

        static string ActiveText(ObjectiveEntry e)
        {
            string text = "<b>" + (e.optional ? UIText.ObjectiveOptionalTag : string.Empty) + Objectives.Label(e) + "</b>";
            if (!string.IsNullOrEmpty(e.description))
                text += "\n<size=20><color=" + DimHex + ">" + e.description + "</color></size>";
            return text;
        }
    }
}
