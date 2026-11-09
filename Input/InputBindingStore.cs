using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorFPS
{
    /// <summary>
    /// Persiste os remapeamentos (binding overrides) em PlayerPrefs, por ID de binding.
    /// Como os IDs são iguais em todas as cópias do asset, todos os jogadores locais compartilham o mesmo perfil:
    /// o teclado do jogador 1 e o controle do jogador 2 são independentes porque cada binding pertence a um esquema.
    /// </summary>
    public static class InputBindingStore
    {
        const string Key = "HorrorFPS.BindingOverrides";

        /// <summary>Disparado depois de salvar / resetar. Os PlayerInputHandler recarregam suas cópias do asset.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Changed = null; }

        /// <summary>Remove todos os overrides do asset (limpa o override de cada binding).</summary>
        static void ClearOverrides(InputActionAsset asset)
        {
            foreach (InputAction action in asset)
            {
                var bindings = action.bindings;
                for (int i = 0; i < bindings.Count; i++)
                    if (!string.IsNullOrEmpty(bindings[i].overridePath))
                        action.ApplyBindingOverride(i, (string)null);
            }
        }

        /// <summary>Aplica o que está salvo, descartando overrides em memória (evita "vazar" entre sessões no Editor).</summary>
        public static void Reload(InputActionAsset asset)
        {
            if (asset == null) return;
            ClearOverrides(asset);
            string json = PlayerPrefs.GetString(Key, string.Empty);
            if (!string.IsNullOrEmpty(json))
                asset.LoadBindingOverridesFromJson(json);
        }

        public static void Save(InputActionAsset asset)
        {
            if (asset == null) return;
            PlayerPrefs.SetString(Key, asset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void ResetAll(InputActionAsset asset)
        {
            if (asset != null) ClearOverrides(asset);
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
