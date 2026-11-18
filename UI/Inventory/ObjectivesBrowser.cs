using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// A aba Objetivos do inventário: o que está em andamento (com descrição e progresso), o que já foi concluído e o que falhou, num texto
    /// só com rolagem (roda do mouse, analógico direito ou cima/baixo). Somente leitura; a lista é a mesma para todos os jogadores e se
    /// atualiza sozinha quando um objetivo muda com a aba aberta.
    /// </summary>
    public class ObjectivesBrowser
    {
        const string DimHex = "#A8A39B";
        const string DoneHex = "#7FD98A";
        const string FailHex = "#E0604F";
        const string HeadHex = "#E8C04A";

        readonly RecordReader reader;
        readonly List<ObjectiveEntry> scratch = new List<ObjectiveEntry>();
        bool dirty = true, resetScroll = true;

        public ObjectivesBrowser(RectTransform parent, float x, float y, float w, float h)
        {
            reader = new RecordReader(parent, x, y, w, h);
            Objectives.Changed += OnChanged;
            Refresh();
        }

        void OnChanged() { dirty = true; }

        public void Destroy()
        {
            Objectives.Changed -= OnChanged;
            if (reader != null) reader.Destroy();
        }

        /// <summary>A aba Objetivos existe? (o jogo tem pelo menos um objetivo cadastrado.)</summary>
        public static bool HasAny() { return Objectives.All.Count > 0; }

        /// <summary>Quantos objetivos estão em andamento agora (vai no rótulo da aba).</summary>
        public static int ActiveCount()
        {
            List<ObjectiveEntry> tmp = new List<ObjectiveEntry>();
            Objectives.Collect(ObjectiveState.Active, tmp);
            return tmp.Count;
        }

        // ------------------------------------------------------------------ texto
        void Refresh()
        {
            dirty = false;
            StringBuilder sb = new StringBuilder();

            scratch.Clear();
            Objectives.Collect(ObjectiveState.Active, scratch);
            int active = scratch.Count;
            if (active > 0)
            {
                AppendHeader(sb, UIText.ObjectivesActive);
                for (int i = 0; i < scratch.Count; i++) AppendActive(sb, scratch[i]);
            }

            scratch.Clear();
            Objectives.Collect(ObjectiveState.Done, scratch);
            int done = scratch.Count;
            if (done > 0)
            {
                AppendHeader(sb, UIText.ObjectivesDone);
                for (int i = 0; i < scratch.Count; i++)
                    sb.Append("<color=").Append(DoneHex).Append(">").Append(Title(scratch[i])).Append("</color>\n");
            }

            scratch.Clear();
            Objectives.Collect(ObjectiveState.Failed, scratch);
            int failed = scratch.Count;
            if (failed > 0)
            {
                AppendHeader(sb, UIText.ObjectivesFailedSection);
                for (int i = 0; i < scratch.Count; i++)
                    sb.Append("<color=").Append(FailHex).Append(">").Append(Title(scratch[i])).Append("</color>\n");
            }

            if (active + done + failed == 0) { reader.ShowEmpty(UIText.ObjectivesEmpty); return; }
            reader.Show(UIText.ObjectivesTitle, string.Format(UIText.ObjectivesSummary, active, done, failed), sb.ToString().TrimEnd(), resetScroll);
            resetScroll = false;
        }

        static void AppendHeader(StringBuilder sb, string text)
        {
            if (sb.Length > 0) sb.Append("\n");
            sb.Append("<color=").Append(HeadHex).Append("><b>").Append(text).Append("</b></color>\n\n");
        }

        static void AppendActive(StringBuilder sb, ObjectiveEntry e)
        {
            sb.Append("<b>").Append(e.optional ? UIText.ObjectiveOptionalTag : string.Empty).Append(Objectives.Label(e)).Append("</b>\n");
            if (!string.IsNullOrEmpty(e.description))
                sb.Append("<size=13><color=").Append(DimHex).Append(">").Append(e.description).Append("</color></size>\n");
            sb.Append("\n");
        }

        static string Title(ObjectiveEntry e) { return string.IsNullOrEmpty(e.title) ? e.id : e.title; }

        // ------------------------------------------------------------------ interação
        /// <summary>Teclado e D-pad: cima/baixo rolam o texto. Não move o cursor.</summary>
        public bool Navigate(Vector2Int dir, Vector2 cursor, out Vector2 to)
        {
            to = cursor;
            if (dir.y != 0) reader.Scroll(dir.y < 0 ? 1f : -1f);
            return false;
        }

        /// <summary>A cada quadro com a aba aberta: refaz o texto se um objetivo mudou e rola com a roda do mouse / analógico direito.</summary>
        public void Tick(Vector2 cursor, float scrollStep)
        {
            if (dirty) Refresh();
            if (Mathf.Abs(scrollStep) > 0.01f && reader.Contains(cursor)) reader.Scroll(scrollStep);
        }
    }
}
