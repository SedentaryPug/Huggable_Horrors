using System;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Níveis de atenção do perseguidor (o valor contínuo fica em StalkerBrain.Attention01).</summary>
    public enum StalkerAttention { Calm = 0, Alert = 1, Hunting = 2, Frenzy = 3 }

    /// <summary>Em que ramo da árvore ele está agora (para debug, HUD e eventos).</summary>
    public enum StalkerMode { Patrol, Investigate, Search, CheckHiding, Chase, Attack }

    /// <summary>De onde veio uma pista.</summary>
    public enum HintSource { Seen, Glimpse, Heard, DirectorPressure, DirectorHiding, Ping, Shared }

    /// <summary>Como um som marcado com StalkerNoiseTag deve ser tratado.</summary>
    public enum NoiseTagMode { Learn, AlwaysInvestigate, NeverInvestigate }

    /// <summary>Um jogador do ponto de vista do perseguidor. Quem preenche os campos de estado é o Diretor.</summary>
    public sealed class StalkerTarget
    {
        public PlayerContext player;
        public Transform root;
        public int index;

        public bool alive = true;
        public bool hidden;
        public float sanity01 = 1f;      // 1 = lúcido
        public float tension;            // 0..1 (intensidade do batimento)
        public float speed;              // m/s (amostrado pelo Diretor)
        public Vector3 velocity;
        public float isolation;          // distância ao jogador vivo mais próximo (m); 0 se só há um
        public float exposure;           // pontuação usada para escolher quem pressionar

        public Vector3 Position { get { return root != null ? root.position : Vector3.zero; } }
        public bool Valid { get { return player != null && root != null; } }
    }

    /// <summary>Um barulho já filtrado pelos ouvidos (volume real depois de distância e paredes).</summary>
    public struct StalkerNoise
    {
        public Vector3 position;
        public float loudness;           // 0..1
        public string kind;              // "Footstep", "Impact"...
        public string key;               // identifica a fonte (aprendizado de iscas)
        public Transform source;
        public StalkerTarget target;     // preenchido quando o barulho é do próprio corpo do jogador
        public bool playerMade;
        public NoiseTagMode tagMode;
        public float time;
    }

    /// <summary>Uma pista entregue ao perseguidor (pelos sentidos dele ou pelo Diretor).</summary>
    public struct StalkerHint
    {
        public Vector3 position;
        public float radius;             // incerteza em metros
        public float confidence;         // 0..1
        public HintSource source;
        public Component spot;           // esconderijo suspeito (HidingSpot), se houver
        public string key;               // fonte do barulho, se for o caso
        public bool playerMade;
        public Vector3 origin;           // onde a pista nasceu (não muda quando a pista é atualizada)
    }
}
