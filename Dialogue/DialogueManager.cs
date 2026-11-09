using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Executa as conversas. É criado sozinho ("[HorrorFPS] Dialogue"). Cada jogador pode ter uma conversa por vez;
    /// várias conversas de jogadores diferentes rodam ao mesmo tempo (split-screen).
    ///
    ///   DialogueManager.Play(grafo, jogador, npc);                       // simples
    ///   DialogueManager.Play(grafo, jogador, new DialogueOptions { ... });
    ///   DialogueManager.Stop(sessao);  DialogueManager.StopAll();
    /// </summary>
    public sealed class DialogueManager : MonoBehaviour
    {
        public static event Action<DialogueSession> Started;
        public static event Action<DialogueSession> Ended;
        public static event Action<DialogueSession, DialogueNode> NodeEntered;
        public static event Action<DialogueSession, DialogueNode, DialogueChoice> ChoiceMade;

        static DialogueManager instance;

        static DialogueManager Instance
        {
            get
            {
                if (instance == null && Application.isPlaying)
                {
                    GameObject go = new GameObject("[HorrorFPS] Dialogue");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<DialogueManager>();
                }
                return instance;
            }
        }

        readonly List<DialogueSession> active = new List<DialogueSession>();
        readonly Dictionary<PlayerContext, DialogueSession> byPlayer = new Dictionary<PlayerContext, DialogueSession>();
        readonly Dictionary<PlayerContext, DialogueView> views = new Dictionary<PlayerContext, DialogueView>();

        // ------------------------------------------------------------------ API
        public static bool AnyActive { get { return instance != null && instance.active.Count > 0; } }

        public static bool IsInDialogue(PlayerContext p)
        {
            return instance != null && p != null && instance.byPlayer.ContainsKey(p);
        }

        public static DialogueSession SessionOf(PlayerContext p)
        {
            DialogueSession s;
            if (instance != null && p != null && instance.byPlayer.TryGetValue(p, out s)) return s;
            return null;
        }

        public static void Stop(DialogueSession s)
        {
            if (instance != null && s != null) instance.End(s, true);
        }

        public static void StopAll()
        {
            if (instance == null) return;
            DialogueSession[] all = instance.active.ToArray();
            for (int i = 0; i < all.Length; i++) instance.End(all[i], true);
        }

        public static DialogueSession Play(DialogueGraph graph, PlayerContext player)
        {
            return Play(graph, player, new DialogueOptions());
        }

        public static DialogueSession Play(DialogueGraph graph, PlayerContext player, DialogueActor speaker)
        {
            DialogueOptions o = new DialogueOptions();
            o.speaker = speaker;
            return Play(graph, player, o);
        }

        public static DialogueSession Play(DialogueGraph graph, PlayerContext player, DialogueOptions options)
        {
            if (graph == null || player == null) return null;
            DialogueManager m = Instance;
            if (m == null) return null;
            return m.Begin(graph, player, options ?? new DialogueOptions());
        }

        /// <summary>Procura um ator: vazio = quem está falando agora; "@player"/"@narrator" = nenhum.</summary>
        public static DialogueActor ResolveActor(DialogueSession s, string id)
        {
            if (string.IsNullOrEmpty(id)) return s.CurrentSpeaker != null ? s.CurrentSpeaker : s.Actor;
            if (id == DialogueIds.Player || id == DialogueIds.Narrator) return null;
            return DialogueActor.Find(id);
        }

        // ------------------------------------------------------------------ ciclo de vida
        void OnDestroy()
        {
            DialogueSession[] all = active.ToArray();
            for (int i = 0; i < all.Length; i++) { all[i].afterEnd.Clear(); End(all[i], true); }
            if (instance == this) instance = null;
        }

        void Update()
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (i >= active.Count) continue;
                DialogueSession s = active[i];
                if (s.Finished) { active.RemoveAt(i); continue; }
                bool playerGone = s.Player == null || !s.Player.gameObject.activeInHierarchy;
                if (playerGone || DialogueBridge.IsSceneBusy) End(s, true);
            }
            UpdateSuppression();
        }

        /// <summary>Se a câmera do jogador está desligada (cutscene em tela cheia), só uma view usa a tela inteira.</summary>
        void UpdateSuppression()
        {
            bool fullscreenTaken = false;
            for (int i = 0; i < active.Count; i++)
            {
                List<DialogueView> vs = active[i].Views;
                for (int k = 0; k < vs.Count; k++)
                {
                    DialogueView v = vs[k];
                    if (v == null) continue;
                    if (v.HasOwnViewport) { v.Suppressed = false; continue; }
                    v.Suppressed = fullscreenTaken;
                    fullscreenTaken = true;
                }
            }
        }

        DialogueView GetView(PlayerContext p)
        {
            DialogueView v;
            if (views.TryGetValue(p, out v) && v != null) return v;
            GameObject go = new GameObject("DialogueView_P" + DialogueBridge.PlayerIndex(p), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            v = go.AddComponent<DialogueView>();
            v.Bind(p);
            views[p] = v;
            return v;
        }

        // ------------------------------------------------------------------ início e fim
        DialogueSession Begin(DialogueGraph graph, PlayerContext player, DialogueOptions opt)
        {
            if (DialogueBridge.IsSceneBusy) return null;
            bool full = graph.presentation == DialoguePresentation.Full;

            DialogueSession existing;
            if (byPlayer.TryGetValue(player, out existing))
            {
                bool replace = opt.interrupt || (full && !existing.IsFull);
                if (!replace) return null;
                End(existing, true);
            }
            if (full && opt.speaker != null && opt.speaker.IsBusy) return null;

            DialogueSession s = new DialogueSession(graph, player, opt, full);
            s.StartNode = opt.startNode;
            s.Viewers.Add(player);
            if (full && opt.audience == DialogueAudience.AllPlayers)
            {
                List<PlayerContext> all = DialogueBridge.Players();
                for (int i = 0; i < all.Count; i++)
                    if (all[i] != player && !byPlayer.ContainsKey(all[i])) s.Viewers.Add(all[i]);
            }
            for (int i = 0; i < s.Viewers.Count; i++)
            {
                byPlayer[s.Viewers[i]] = s;
                if (full) DialogueBridge.SetLock(s.Viewers[i], true);
            }

            if (full && opt.speaker != null)
            {
                opt.speaker.IsBusy = true;
                if (opt.faceSpeaker) opt.speaker.BeginFacing(player.transform);
            }
            if (full && opt.faceSpeaker && DialogueStyle.LookAtSpeaker)
            {
                Camera cam = DialogueBridge.CameraOf(player);
                if (cam != null)
                {
                    GameObject go = new GameObject("DialogueCameraLook");
                    go.transform.SetParent(transform, false);
                    DialogueCameraLook look = go.AddComponent<DialogueCameraLook>();
                    look.Init(cam);
                    look.SetActor(opt.speaker);
                    s.look = look;
                }
            }

            active.Add(s);
            s.routine = StartCoroutine(RunSession(s));
            if (Started != null) Started(s);
            return s;
        }

        void End(DialogueSession s, bool aborted)
        {
            if (s == null || s.Finished) return;
            s.Finished = true;
            s.Aborted = aborted;
            if (s.routine != null) StopCoroutine(s.routine);
            for (int i = 0; i < s.children.Count; i++) if (s.children[i] != null) StopCoroutine(s.children[i]);
            s.children.Clear();
            if (s.voiceSource != null && s.voiceSource.isPlaying) s.voiceSource.Stop();

            for (int i = 0; i < s.Viewers.Count; i++)
            {
                PlayerContext v = s.Viewers[i];
                if (v == null) continue;
                DialogueSession cur;
                if (byPlayer.TryGetValue(v, out cur) && cur == s) byPlayer.Remove(v);
                if (s.IsFull) DialogueBridge.SetLock(v, false);
            }
            for (int i = 0; i < s.Views.Count; i++) if (s.Views[i] != null) s.Views[i].Close();
            if (s.look != null) s.look.Release();
            if (s.Actor != null) { s.Actor.IsBusy = false; s.Actor.SetTalking(false); s.Actor.EndFacing(); }
            if (s.CurrentSpeaker != null) s.CurrentSpeaker.SetTalking(false);
            active.Remove(s);

            if (s.Completed && !aborted)
            {
                DialogueBridge.AddInt(DialogueKeys.Done(s.StartGraph.Id), 1);
                if (s.Graph != s.StartGraph) DialogueBridge.AddInt(DialogueKeys.Done(s.Graph.Id), 1);
            }

            s.RaiseEnded();
            if (Ended != null) Ended(s);
            if (s.Options.onEnded != null) s.Options.onEnded(s);

            for (int i = 0; i < s.afterEnd.Count; i++)
            {
                try { s.afterEnd[i](); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
            s.afterEnd.Clear();
        }

        // ------------------------------------------------------------------ execução
        IEnumerator RunSession(DialogueSession s)
        {
            for (int i = 0; i < s.Viewers.Count; i++)
            {
                DialogueView v = GetView(s.Viewers[i]);
                v.Open(s, i == 0);
                s.Views.Add(v);
            }
            yield return null;   // um quadro: o "E" que abriu a conversa não conta como "avançar"

            string nodeId = !string.IsNullOrEmpty(s.StartNode) ? s.StartNode : s.Graph.EntryNodeId;
            int hops = 0;
            while (!string.IsNullOrEmpty(nodeId) && !s.Finished)
            {
                DialogueNode node = s.Graph.Find(nodeId);
                if (node == null)
                {
                    Debug.LogWarning("[Dialogue] Nó '" + nodeId + "' não existe em '" + s.Graph.name + "'. Conversa encerrada.");
                    break;
                }
                s.CurrentNode = node;
                DialogueBridge.SetBool(DialogueKeys.Visited(s.Graph.Id, node.id), true);
                if (NodeEntered != null) NodeEntered(s, node);
                s.NextNode = null;

                if (node.kind == DialogueNodeKind.Line) hops = 0;
                else if (++hops > 200)
                {
                    Debug.LogWarning("[Dialogue] Laço sem nenhuma fala em '" + s.Graph.name + "' (nó '" + DialogueGraph.LabelOf(node) + "'). Conversa encerrada.");
                    break;
                }

                IEnumerator it = null;
                if (node.kind == DialogueNodeKind.Line) it = RunLine(s, node);
                else if (node.kind == DialogueNodeKind.Action) it = RunActionNode(s, node);
                else s.NextNode = Route(s, node);

                if (it != null)
                    while (it.MoveNext()) yield return it.Current;

                nodeId = s.NextNode;
            }

            if (!s.Finished)
            {
                s.Completed = true;
                End(s, false);
            }
        }

        IEnumerator RunActionNode(DialogueSession s, DialogueNode n)
        {
            IEnumerator it = RunActions(s, n.actions);
            while (it.MoveNext()) yield return it.Current;
            s.NextNode = n.next;
        }

        IEnumerator RunActions(DialogueSession s, List<DialogueAction> list)
        {
            if (list == null) yield break;
            for (int i = 0; i < list.Count; i++)
            {
                if (s.Finished) yield break;
                DialogueAction a = list[i];
                if (a == null) continue;
                if (!DialogueConditions.All(a.conditions, s.Context)) continue;
                IEnumerator it = DialogueActions.Execute(a, s);
                while (it.MoveNext()) yield return it.Current;
            }
        }

        /// <summary>Nós sem espera: Branch, RandomPick, Jump, End. Devolve o id do próximo.</summary>
        string Route(DialogueSession s, DialogueNode n)
        {
            switch (n.kind)
            {
                case DialogueNodeKind.Branch:
                    for (int i = 0; i < n.routes.Count; i++)
                    {
                        DialogueRoute r = n.routes[i];
                        if (r != null && DialogueConditions.All(r.conditions, s.Context)) return r.next;
                    }
                    return n.next;

                case DialogueNodeKind.RandomPick:
                    {
                        List<DialogueRandomOption> ok = new List<DialogueRandomOption>();
                        float total = 0f;
                        for (int i = 0; i < n.options.Count; i++)
                        {
                            DialogueRandomOption o = n.options[i];
                            if (o == null || o.weight <= 0f) continue;
                            if (!DialogueConditions.All(o.conditions, s.Context)) continue;
                            ok.Add(o);
                            total += o.weight;
                        }
                        if (ok.Count == 0) return n.next;
                        float roll = UnityEngine.Random.value * total;
                        for (int i = 0; i < ok.Count; i++)
                        {
                            roll -= ok[i].weight;
                            if (roll <= 0f) return ok[i].next;
                        }
                        return ok[ok.Count - 1].next;
                    }

                case DialogueNodeKind.Jump:
                    if (n.jumpGraph != null)
                    {
                        s.Graph = n.jumpGraph;
                        return !string.IsNullOrEmpty(n.jumpNode) ? n.jumpNode : n.jumpGraph.EntryNodeId;
                    }
                    return n.next;

                case DialogueNodeKind.End:
                    return null;
            }
            return n.next;
        }

        // ------------------------------------------------------------------ uma fala
        IEnumerator RunLine(DialogueSession s, DialogueNode n)
        {
            bool full = s.IsFull;
            DialogueActor speaker = ResolveSpeaker(s, n.speakerId);
            s.CurrentSpeaker = speaker;
            if (s.look != null) s.look.SetActor(speaker);
            string name = SpeakerName(n, speaker);
            Color color = speaker != null ? speaker.nameColor
                : (n.speakerId == DialogueIds.Player ? DialogueStyle.PlayerColor : DialogueStyle.NarratorColor);
            Sprite portrait = n.portrait != null ? n.portrait : (speaker != null ? speaker.portrait : null);

            if (n.onStart != null && n.onStart.Count > 0) s.children.Add(StartCoroutine(RunActions(s, n.onStart)));
            if (n.animations != null)
                for (int i = 0; i < n.animations.Count; i++)
                {
                    DialogueAnimation a = n.animations[i];
                    DialogueActor target = ResolveActor(s, a != null ? a.actorId : null);
                    if (target != null && a != null) target.PlayAnimation(a);
                }
            if (speaker != null) speaker.SetTalking(true);

            float cps = n.charsPerSecond > 0f ? n.charsPerSecond : DialogueStyle.CharsPerSecond;
            string leadText = "";
            for (int k = 0; k < s.Views.Count; k++)
            {
                DialogueContext vc = s.ContextFor(s.Viewers[k]);
                string t = ResolveText(n, vc, name);
                if (k == 0) leadText = t;
                s.Views[k].BeginLine(name, color, portrait, t, cps, speaker);
            }
            float voiceLen = PlayVoice(s, n, speaker);
            float dur = n.duration > 0f ? n.duration : AutoDuration(leadText, voiceLen);
            DialogueView lead = s.Views[0];

            // 1) o texto aparecendo
            float elapsed = 0f;
            bool skipped = false;
            while (AnyTyping(s))
            {
                if (s.Finished) yield break;
                if (full && lead.ConfirmPressed) { CompleteTyping(s); skipped = true; break; }
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (skipped) yield return null;   // o mesmo clique não pode avançar duas vezes
            if (speaker != null && voiceLen <= 0f) speaker.SetTalking(false);

            // 2) escolhas, espera automática ou espera por "avançar"
            List<DialogueChoiceItem> items = null;
            List<int> map = null;
            bool anyEnabled = false;
            if (full && n.choices != null && n.choices.Count > 0)
            {
                items = new List<DialogueChoiceItem>();
                map = BuildChoices(s, n, s.Context, name, items);
                for (int i = 0; i < items.Count; i++) if (items[i].enabled) anyEnabled = true;
            }

            int picked = -1;
            if (anyEnabled)
            {
                lead.ShowChoices(items);
                for (int k = 1; k < s.Views.Count; k++) s.Views[k].ShowSpectating(DialogueBridge.PlayerIndex(s.Player) + 1);

                float limit = n.choiceTimeLimit;
                float waited = 0f;
                bool timedOut = false;
                int pickedView = -1;
                while (true)
                {
                    if (s.Finished) yield break;
                    pickedView = lead.PollChoice();
                    if (pickedView >= 0) break;
                    if (limit > 0f)
                    {
                        waited += Time.deltaTime;
                        lead.SetTimer(1f - waited / limit);
                        if (waited >= limit) { timedOut = true; break; }
                    }
                    yield return null;
                }
                lead.HideChoices();
                for (int k = 1; k < s.Views.Count; k++) s.Views[k].HideChoices();

                if (timedOut)
                {
                    int t = n.timeoutChoice;
                    int vi = map.IndexOf(t);
                    if (t >= 0 && vi >= 0 && items[vi].enabled) picked = t;
                }
                else picked = map[pickedView];
            }
            else if (full && !n.autoAdvance)
            {
                lead.SetIndicator(true);
                while (!lead.ConfirmPressed)
                {
                    if (s.Finished) yield break;
                    yield return null;
                }
                lead.SetIndicator(false);
            }
            else
            {
                float wait = Mathf.Max(0.45f, dur - elapsed);
                float w = 0f;
                while (w < wait)
                {
                    if (s.Finished) yield break;
                    if (full && lead.ConfirmPressed) break;
                    w += Time.deltaTime;
                    yield return null;
                }
            }

            if (s.voiceSource != null && s.voiceSource.isPlaying) s.voiceSource.Stop();
            if (speaker != null) speaker.SetTalking(false);

            // 3) consequências
            string next = n.next;
            if (picked >= 0)
            {
                DialogueChoice ch = n.choices[picked];
                string cid = string.IsNullOrEmpty(ch.id) ? picked.ToString() : ch.id;
                DialogueBridge.SetBool(DialogueKeys.Chosen(s.Graph.Id, n.id, cid), true);
                if (ChoiceMade != null) ChoiceMade(s, n, ch);
                IEnumerator ca = RunActions(s, ch.actions);
                while (ca.MoveNext()) yield return ca.Current;
                if (!string.IsNullOrEmpty(ch.next)) next = ch.next;
            }
            IEnumerator ea = RunActions(s, n.onEnd);
            while (ea.MoveNext()) yield return ea.Current;
            s.NextNode = next;
        }

        // ------------------------------------------------------------------ auxiliares de fala
        DialogueActor ResolveSpeaker(DialogueSession s, string id)
        {
            if (string.IsNullOrEmpty(id)) return s.Actor;
            if (id == DialogueIds.Player || id == DialogueIds.Narrator) return null;
            DialogueActor a = DialogueActor.Find(id);
            if (a == null) Debug.LogWarning("[Dialogue] Ator '" + id + "' não está na cena (DialogueActor com esse Actor Id). Usando o id como nome.");
            return a;
        }

        static string SpeakerName(DialogueNode n, DialogueActor speaker)
        {
            if (!string.IsNullOrEmpty(n.speakerName)) return n.speakerName;
            if (n.speakerId == DialogueIds.Narrator) return "";
            if (n.speakerId == DialogueIds.Player) return DialogueStyle.PlayerName;
            if (speaker != null) return speaker.displayName;
            return string.IsNullOrEmpty(n.speakerId) ? "" : n.speakerId;
        }

        static string ResolveText(DialogueNode n, DialogueContext vctx, string name)
        {
            string t = n.text;
            DialogueTextVariant pick = null;
            for (int i = 0; i < n.variants.Count; i++)
            {
                DialogueTextVariant v = n.variants[i];
                if (v == null) continue;
                if (pick != null && v.priority < pick.priority) continue;
                if (DialogueConditions.All(v.conditions, vctx)) pick = v;
            }
            if (pick != null) t = pick.text;
            t = DialogueText.Expand(t, vctx, name);
            if (n.glitchAtLowSanity) t = DialogueText.Glitch(t, DialogueBridge.Sanity01(vctx.Player));
            return t;
        }

        static float AutoDuration(string text, float voiceLen)
        {
            if (voiceLen > 0f) return voiceLen + 0.35f;
            int len = DialogueText.VisibleLength(text);
            return Mathf.Max(1.6f, len / 14f + 0.9f);
        }

        static float PlayVoice(DialogueSession s, DialogueNode n, DialogueActor speaker)
        {
            if (n.voice == null) return 0f;
            AudioSource src = speaker != null ? speaker.GetAudioSource() : null;
            if (src == null && s.Views.Count > 0 && s.Views[0] != null) src = s.Views[0].VoiceSource;
            if (src == null) return n.voice.length;
            src.Stop();
            src.clip = n.voice;
            src.Play();
            s.voiceSource = src;
            return n.voice.length;
        }

        static bool AnyTyping(DialogueSession s)
        {
            for (int i = 0; i < s.Views.Count; i++) if (s.Views[i] != null && s.Views[i].IsTyping) return true;
            return false;
        }

        static void CompleteTyping(DialogueSession s)
        {
            for (int i = 0; i < s.Views.Count; i++) if (s.Views[i] != null) s.Views[i].CompleteTyping();
        }

        /// <summary>Monta a lista visível de escolhas. 'map' liga cada item visível ao índice original em n.choices.</summary>
        static List<int> BuildChoices(DialogueSession s, DialogueNode n, DialogueContext ctx, string name, List<DialogueChoiceItem> items)
        {
            List<int> map = new List<int>();
            for (int i = 0; i < n.choices.Count && map.Count < DialogueView.MaxChoices; i++)
            {
                DialogueChoice c = n.choices[i];
                if (c == null) continue;
                string cid = string.IsNullOrEmpty(c.id) ? i.ToString() : c.id;
                if (c.once && DialogueBridge.GetBool(DialogueKeys.Chosen(s.Graph.Id, n.id, cid))) continue;
                bool ok = DialogueConditions.All(c.conditions, ctx);
                if (!ok && c.whenLocked == DialogueLockedChoice.Hide) continue;
                DialogueChoiceItem it = new DialogueChoiceItem();
                it.text = DialogueText.Expand(c.text, ctx, name);
                it.enabled = ok;
                it.hint = c.lockedHint;
                items.Add(it);
                map.Add(i);
            }
            return map;
        }
    }
}
