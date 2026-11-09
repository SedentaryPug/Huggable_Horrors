using System.Collections;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Vira a visão de todos os jogadores para um alvo (o monstro no fim do corredor). Para sequências sem câmera de cutscene.</summary>
    [AddComponentMenu("HorrorFPS/Cutscene/Steps/Player Look")]
    public class PlayerLookStep : CutsceneStep
    {
        [Header("Olhar")]
        [SerializeField] Transform target;
        [SerializeField] float duration = 1.5f;
        [SerializeField] AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public override IEnumerator Run(CutsceneContext ctx)
        {
            if (target == null) yield break;

            PlayerContext[] players = PlayerContext.All.ToArray();
            float[] yaw0 = new float[players.Length];
            float[] pitch0 = new float[players.Length];
            float[] yaw1 = new float[players.Length];
            float[] pitch1 = new float[players.Length];
            for (int i = 0; i < players.Length; i++)
            {
                if (players[i].Controller == null) continue;
                yaw0[i] = players[i].Controller.Yaw;
                pitch0[i] = players[i].Controller.Pitch;
                Aim(players[i], out yaw1[i], out pitch1[i]);
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = curve.Evaluate(Mathf.Clamp01(t / duration));
                for (int i = 0; i < players.Length; i++)
                {
                    if (players[i] == null || players[i].Controller == null) continue;
                    players[i].Controller.SetLook(Mathf.LerpAngle(yaw0[i], yaw1[i], k), Mathf.Lerp(pitch0[i], pitch1[i], k));
                }
                yield return null;
            }

            for (int i = 0; i < players.Length; i++)
            {
                if (players[i] == null || players[i].Controller == null) continue;
                players[i].Controller.SetLook(yaw1[i], pitch1[i]);
            }
        }

        public override void OnSkip(CutsceneContext ctx)
        {
            if (target == null) return;
            for (int i = 0; i < PlayerContext.All.Count; i++)
            {
                PlayerContext p = PlayerContext.All[i];
                if (p.Controller == null) continue;
                float yaw, pitch;
                Aim(p, out yaw, out pitch);
                p.Controller.SetLook(yaw, pitch);
            }
        }

        void Aim(PlayerContext player, out float yaw, out float pitch)
        {
            Vector3 origin = player.Camera != null ? player.Camera.transform.position : player.transform.position;
            Vector3 dir = target.position - origin;
            yaw = FirstPersonController.YawFromDirection(dir);
            float flat = new Vector2(dir.x, dir.z).magnitude;
            pitch = -Mathf.Atan2(dir.y, flat) * Mathf.Rad2Deg; // pitch positivo = olhando para baixo
        }
    }
}
