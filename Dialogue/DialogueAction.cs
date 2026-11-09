using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    public enum DialogueActionKind
    {
        /// <summary>Liga/desliga uma variável de jogo (bool).</summary>
        SetVariable,
        /// <summary>Define uma variável numérica.</summary>
        SetNumber,
        /// <summary>Soma (ou subtrai) de uma variável numérica.</summary>
        AddNumber,
        /// <summary>Guarda um texto numa variável.</summary>
        SetText,
        /// <summary>Entrega item(ns) ao inventário de quem fala.</summary>
        GiveItem,
        /// <summary>Tira item(ns) do inventário de quem fala.</summary>
        RemoveItem,
        /// <summary>Dispara um evento por id (DialogueEventRelay na cena, DialogueEvents.On no código).</summary>
        Event,
        /// <summary>Mexe no Animator de um DialogueActor.</summary>
        Animation,
        /// <summary>Toca um som (na posição de um ator ou 2D).</summary>
        PlaySound,
        /// <summary>Soma/subtrai sanidade de quem fala.</summary>
        Sanity,
        /// <summary>Soma/subtrai sanidade de todos os jogadores.</summary>
        SanityAll,
        /// <summary>Causa dano físico a quem fala.</summary>
        Damage,
        /// <summary>Treme a câmera de quem fala.</summary>
        CameraShake,
        /// <summary>Dispara o coração (susto) em quem estiver perto do ator.</summary>
        Scare,
        /// <summary>Espera N segundos (só faz sentido em ações sequenciais).</summary>
        Wait,
        /// <summary>Troca de nível depois que o diálogo terminar.</summary>
        GoToLevel,
        /// <summary>Começa outro diálogo depois que este terminar.</summary>
        StartDialogue,
        /// <summary>Salva no slot automático depois que o diálogo terminar.</summary>
        Autosave,
        /// <summary>Função registrada por código: DialogueActions.Register(id, ...).</summary>
        Custom
    }

    [Serializable]
    public class DialogueAction
    {
        public DialogueActionKind kind;
        public string key;
        public string text;
        public float number = 1f;
        public bool flag = true;
        public AudioClip clip;
        public DialogueAnimation animation = new DialogueAnimation();
        public DialogueGraph graph;
        public string actorId;
        [Tooltip("Se preenchida, a ação só roda quando TODAS as condições passam.")]
        public List<DialogueCondition> conditions = new List<DialogueCondition>();
    }

    public static class DialogueActions
    {
        static readonly Dictionary<string, Action<DialogueContext, string>> custom = new Dictionary<string, Action<DialogueContext, string>>();

        /// <summary>Registra uma ação própria (tipo Custom). O 2º argumento é o campo "Argumento".</summary>
        public static void Register(string id, Action<DialogueContext, string> fn) { if (!string.IsNullOrEmpty(id)) custom[id] = fn; }
        public static void Unregister(string id) { custom.Remove(id); }

        /// <summary>Executa uma ação. É um iterador para que 'Wait' funcione dentro de listas sequenciais.</summary>
        public static IEnumerator Execute(DialogueAction a, DialogueSession s)
        {
            PlayerContext player = s.Player;
            string reason = "dialogue:" + s.Graph.Id;
            switch (a.kind)
            {
                case DialogueActionKind.SetVariable:
                    DialogueBridge.SetBool(a.key, a.flag);
                    break;
                case DialogueActionKind.SetNumber:
                    DialogueBridge.SetInt(a.key, Mathf.RoundToInt(a.number));
                    break;
                case DialogueActionKind.AddNumber:
                    DialogueBridge.AddInt(a.key, Mathf.RoundToInt(a.number));
                    break;
                case DialogueActionKind.SetText:
                    DialogueBridge.SetString(a.key, a.text ?? "");
                    break;
                case DialogueActionKind.GiveItem:
                    {
                        int want = Mathf.Max(1, Mathf.RoundToInt(a.number));
                        int got = DialogueBridge.GiveItem(player, a.key, want);
                        if (got < want) Debug.LogWarning("[Dialogue] Só " + got + "/" + want + " de '" + a.key + "' couberam no inventário.");
                        if (!string.IsNullOrEmpty(a.text)) s.Notify(DialogueText.Expand(a.text, s.Context, ""));
                        break;
                    }
                case DialogueActionKind.RemoveItem:
                    {
                        DialogueBridge.RemoveItem(player, a.key, Mathf.Max(1, Mathf.RoundToInt(a.number)));
                        if (!string.IsNullOrEmpty(a.text)) s.Notify(DialogueText.Expand(a.text, s.Context, ""));
                        break;
                    }
                case DialogueActionKind.Event:
                    DialogueEvents.Raise(a.key, a.text, s.Context);
                    break;
                case DialogueActionKind.Animation:
                    {
                        DialogueActor actor = DialogueManager.ResolveActor(s, a.animation != null ? a.animation.actorId : null);
                        if (actor != null && a.animation != null) actor.PlayAnimation(a.animation);
                        break;
                    }
                case DialogueActionKind.PlaySound:
                    {
                        if (a.clip == null) break;
                        float vol = a.number > 0f ? a.number : 1f;
                        DialogueActor actor = DialogueManager.ResolveActor(s, a.actorId);
                        if (actor != null) AudioSource.PlayClipAtPoint(a.clip, actor.transform.position, vol);
                        else s.PlayLocalSound(a.clip, vol);
                        break;
                    }
                case DialogueActionKind.Sanity:
                    DialogueBridge.AddSanity(player, a.number, reason);
                    break;
                case DialogueActionKind.SanityAll:
                    DialogueBridge.AddSanityAll(a.number, reason);
                    break;
                case DialogueActionKind.Damage:
                    DialogueBridge.Damage(player, a.number);
                    break;
                case DialogueActionKind.CameraShake:
                    DialogueBridge.Shake(player, a.number);
                    break;
                case DialogueActionKind.Scare:
                    {
                        DialogueActor actor = DialogueManager.ResolveActor(s, a.actorId);
                        Vector3 pos = actor != null ? actor.transform.position : (player != null ? player.transform.position : Vector3.zero);
                        DialogueBridge.Alert(pos, Mathf.Clamp01(a.number), a.flag);
                        break;
                    }
                case DialogueActionKind.Wait:
                    yield return new WaitForSeconds(Mathf.Max(0f, a.number));
                    break;
                case DialogueActionKind.GoToLevel:
                    {
                        string level = a.key; string spawn = a.text;
                        s.afterEnd.Add(delegate { DialogueBridge.GoToLevel(level, spawn); });
                        break;
                    }
                case DialogueActionKind.StartDialogue:
                    {
                        if (a.graph == null) break;
                        DialogueGraph g = a.graph; PlayerContext p = s.Player; DialogueActor spk = s.Actor;
                        s.afterEnd.Add(delegate
                        {
                            DialogueOptions o = new DialogueOptions();
                            o.speaker = spk;
                            DialogueManager.Play(g, p, o);
                        });
                        break;
                    }
                case DialogueActionKind.Autosave:
                    s.afterEnd.Add(delegate { DialogueBridge.Autosave(); });
                    break;
                case DialogueActionKind.Custom:
                    {
                        Action<DialogueContext, string> fn;
                        if (custom.TryGetValue(a.key ?? "", out fn))
                        {
                            try { fn(s.Context, a.text); }
                            catch (Exception ex) { Debug.LogException(ex); }
                        }
                        else DialogueEvents.Raise(a.key, a.text, s.Context);   // sem função registrada: vira evento
                        break;
                    }
            }
            yield break;
        }
    }
}
