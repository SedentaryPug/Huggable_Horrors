using System;
using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    // Tudo aqui é serializável com JsonUtility (sem Dictionary, sem DateTime): listas de pares e strings.

    /// <summary>Resumo de um save: é o que o menu "Carregar" mostra sem precisar ler o mundo inteiro.</summary>
    [Serializable]
    public class SaveHeader
    {
        public int version = 1;
        public int slot;
        public string savedAt;        // data/hora local legível
        public long savedAtTicks;     // UTC, para achar o save mais recente
        public string levelId;
        public string levelName;
        public float playTime;        // segundos
        public int players;
    }

    [Serializable]
    public class VarEntry
    {
        public string key;
        public string value;
    }

    /// <summary>Estado de UM componente salvável (ISaveable) dentro de uma entidade.</summary>
    [Serializable]
    public class ComponentRecord
    {
        public string key;
        public string json;
    }

    [Serializable]
    public class EntityRecord
    {
        public string id;
        public List<ComponentRecord> components = new List<ComponentRecord>();
    }

    /// <summary>
    /// O que mudou em uma cena: estado das entidades que existem e ids das que foram destruídas.
    /// É guardado na sessão ao SAIR da cena e reaplicado ao ENTRAR de novo — por isso as mudanças sobrevivem à troca de cenas.
    /// </summary>
    [Serializable]
    public class SceneRecord
    {
        public string scene;
        public List<EntityRecord> entities = new List<EntityRecord>();
        public List<string> destroyed = new List<string>();
    }

    [Serializable]
    public class StatRecord
    {
        public StatType type;
        public float current;
    }

    [Serializable]
    public class EffectRecord
    {
        public StatusEffectData data = new StatusEffectData();
        public float remaining;
    }

    [Serializable]
    public class PlayerRecord
    {
        public int index;
        public Vector3 position;
        public float yaw;
        public float pitch;
        public bool alive = true;
        public List<StatRecord> stats = new List<StatRecord>();
        public List<EffectRecord> effects = new List<EffectRecord>();
    }

    /// <summary>O arquivo de save inteiro.</summary>
    [Serializable]
    public class GameSave
    {
        public SaveHeader header = new SaveHeader();
        public string levelId;
        public string spawnId;
        public List<string> streamedScenes = new List<string>();
        public List<VarEntry> variables = new List<VarEntry>();
        public List<PlayerRecord> players = new List<PlayerRecord>();
        public List<SceneRecord> scenes = new List<SceneRecord>();
    }

    /// <summary>Só o cabeçalho (JsonUtility ignora o resto do arquivo).</summary>
    [Serializable]
    class SaveHeaderOnly
    {
        public SaveHeader header;
    }
}
