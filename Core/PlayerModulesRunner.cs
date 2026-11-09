using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>
    /// Vigia os jogadores que aparecem (o LocalPlayerManager os cria em tempo de execução) e instala neles os módulos que faltam
    /// (inventário, sanidade, corpo). Criado sozinho por PlayerModules; não precisa estar em cena.
    /// </summary>
    public class PlayerModulesRunner : MonoBehaviour
    {
        readonly HashSet<int> done = new HashSet<int>();
        float next;

        void Update()
        {
            if (!PlayerModules.AutoInstall || Time.unscaledTime < next) return;
            next = Time.unscaledTime + 0.5f;

            PlayerContext[] found = HorrorUtil.FindAll<PlayerContext>();
            for (int i = 0; i < found.Length; i++)
            {
                int id = found[i].GetInstanceID();
                if (!done.Add(id)) continue;
                PlayerModules.Install(found[i]);
            }
        }
    }
}
