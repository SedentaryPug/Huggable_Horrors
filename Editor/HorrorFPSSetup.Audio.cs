using UnityEditor;
using UnityEngine;

namespace HorrorFPS.EditorTools
{
    /// <summary>
    /// Parte do wizard (mesma classe): o áudio da demonstração. Zonas de som por área (música e ambiente gerados por código), pisos que
    /// mudam o passo (tapete, metal, água) e um rádio com duas falas no Salão. Chamado pelas cenas em HorrorFPSSetup.Levels.cs e FullDemo.cs.
    /// </summary>
    public static partial class HorrorFPSSetup
    {
        /// <summary>Cria uma zona de áudio. 'global' = vale para a cena inteira (sem Collider).</summary>
        static AudioZone AddAreaAudio(string name, Vector3 center, Vector3 size, bool global, int priority,
            MusicKind music, float musicVolume, AmbienceKind ambience, float ambienceVolume,
            NoiseSurface.Preset floor, bool occasional, bool stinger)
        {
            GameObject go = new GameObject(name);
            go.transform.position = center;
            if (!global)
            {
                BoxCollider box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = size;
            }
            AudioZone zone = go.AddComponent<AudioZone>();
            if (global)
            {
                Collider extra = go.GetComponent<Collider>();      // o Reset da zona põe uma caixa; a global não usa
                if (extra != null) Object.DestroyImmediate(extra);
            }
            new Props(zone)
                .Int("priority", priority).Bool("global", global).Bool("stingerOnEnter", stinger)
                .Str("area.label", name)
                .Enum("area.generatedMusic", (int)music).Float("area.musicVolume", musicVolume)
                .Enum("area.generatedAmbience", (int)ambience).Float("area.ambienceVolume", ambienceVolume)
                .Bool("area.generatedOccasional", occasional)
                .Enum("area.floor", (int)floor)
                .Apply();
            return zone;
        }

        /// <summary>Piso que muda o som (e o barulho) dos passos: uma caixa baixa deitada no chão.</summary>
        static NoiseSurface AddNoiseSurface(string name, Vector3 center, Vector3 size, NoiseSurface.Preset preset)
        {
            BoxCollider box = MakeTriggerBox(name, center, size);
            NoiseSurface surface = box.gameObject.AddComponent<NoiseSurface>();
            new Props(surface).Enum("preset", (int)preset).Float("footstepMultiplier", NoiseSurface.PresetValue(preset)).Apply();
            return surface;
        }

        static void BuildMenuAudio()
        {
            AddAreaAudio("Áudio do menu", Vector3.zero, Vector3.one, true, 0,
                MusicKind.Uneasy, 0.35f, AmbienceKind.Wind, 0.4f, NoiseSurface.Preset.Concrete, false, false);
        }

        static void BuildHallAudio(Material wood, Material metal)
        {
            // o salão inteiro: madeira, rangidos e música calma; o corredor atrás das portas fica mais tenso
            AddAreaAudio("Áudio do salão", Vector3.zero, Vector3.one, true, 0,
                MusicKind.Calm, 0.4f, AmbienceKind.Room, 0.55f, NoiseSurface.Preset.Wood, true, false);
            AddAreaAudio("Áudio do corredor", new Vector3(0f, 1.5f, 12f), new Vector3(30f, 3f, 5.5f), false, 1,
                MusicKind.Uneasy, 0.45f, AmbienceKind.Hum, 0.5f, NoiseSurface.Preset.Concrete, true, false);

            // um tapete no meio do salão: passos macios e quase silenciosos
            AddNoiseSurface("Tapete do salão", new Vector3(0f, 0.1f, -2f), new Vector3(3f, 0.25f, 8f), NoiseSurface.Preset.Carpet);

            // o rádio, numa mesa perto de onde os jogadores aparecem
            Prim(PrimitiveType.Cube, "Mesa do rádio", new Vector3(-6f, 0.45f, -11f), new Vector3(1.6f, 0.9f, 0.9f), wood, null, true);
            GameObject radio = Prim(PrimitiveType.Cube, "Rádio", new Vector3(-6f, 1.05f, -11f), new Vector3(0.5f, 0.3f, 0.2f), metal, null, true);
            RadioSet set = radio.AddComponent<RadioSet>();
            SerializedObject so = new SerializedObject(set);
            SerializedProperty list = so.FindProperty("transmissions");
            list.arraySize = 2;
            SetTransmission(list.GetArrayElementAtIndex(0), "radio_boas_vindas", "Rádio",
                "...tem alguém aí? Ouçam com atenção: a chave do porão está numa mesa do salão. E não façam barulho.", string.Empty);
            SetTransmission(list.GetArrayElementAtIndex(1), "radio_aviso", "Rádio",
                "Vocês pegaram a chave? Não desçam sem lanterna. Ele escuta tudo o que vocês fazem.", KeyVariable);
            so.ApplyModifiedPropertiesWithoutUndo();
            AddSaveable(radio, false);
        }

        static void SetTransmission(SerializedProperty e, string id, string speaker, string text, string requiresVariable)
        {
            e.FindPropertyRelative("id").stringValue = id;
            e.FindPropertyRelative("speaker").stringValue = speaker;
            e.FindPropertyRelative("text").stringValue = text;
            e.FindPropertyRelative("once").boolValue = true;
            e.FindPropertyRelative("requiresVariable").stringValue = requiresVariable;
        }

        static void BuildCellarAudio()
        {
            AddAreaAudio("Áudio do porão", Vector3.zero, Vector3.one, true, 0,
                MusicKind.Dread, 0.5f, AmbienceKind.Drips, 0.6f, NoiseSurface.Preset.Concrete, true, true);
            AddNoiseSurface("Poça do porão", new Vector3(3f, 0.1f, 2f), new Vector3(2.5f, 0.25f, 2.5f), NoiseSurface.Preset.Water);
        }

        static void BuildAnnexAudio()
        {
            // a cena do Anexo é carregada por streaming ao lado do Salão: a zona cobre só o interior dele
            AddAreaAudio("Áudio do anexo", new Vector3(21.5f, 1.5f, 0f), new Vector3(13f, 3f, 14f), false, 2,
                MusicKind.Uneasy, 0.45f, AmbienceKind.Rumble, 0.55f, NoiseSurface.Preset.Concrete, true, false);
            AddNoiseSurface("Grade de metal do anexo", new Vector3(21.5f, 0.1f, -3f), new Vector3(3f, 0.25f, 3f), NoiseSurface.Preset.Metal);
        }

        static void BuildTunnelsAudio(GridMap g)
        {
            // os túneis: gotas e música sombria; a poça do laço e o piso de metal junto da porta de serviço mudam o passo
            AddAreaAudio("Áudio dos túneis", Vector3.zero, Vector3.one, true, 0,
                MusicKind.Dread, 0.45f, AmbienceKind.Drips, 0.6f, NoiseSurface.Preset.Concrete, true, false);
            AddNoiseSurface("Poça do túnel", g.Center(7, 5) + new Vector3(0f, 0.1f, 0f), new Vector3(g.cell * 2f, 0.25f, g.cell), NoiseSurface.Preset.Water);
            AddNoiseSurface("Grade do túnel", g.Center(4, 3) + new Vector3(0f, 0.1f, 0f), new Vector3(g.cell, 0.25f, g.cell), NoiseSurface.Preset.Metal);
        }

        static void BuildPowerAudio()
        {
            // a casa de força: zumbido elétrico e música inquieta; o piso todo é grade de metal
            AddAreaAudio("Áudio da casa de força", Vector3.zero, Vector3.one, true, 0,
                MusicKind.Uneasy, 0.45f, AmbienceKind.Hum, 0.6f, NoiseSurface.Preset.Metal, true, false);
            AddNoiseSurface("Piso de metal da casa de força", new Vector3(0f, 0.1f, 0f), new Vector3(16f, 0.25f, 12f), NoiseSurface.Preset.Metal);
        }
    }
}
