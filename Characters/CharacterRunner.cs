using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HorrorFPS
{
    /// <summary>
    /// Executa a troca de personagem (corrotina) e mantém o PlayerInputManager com o prefab certo para o próximo jogador.
    /// Criado sozinho pelo CharacterManager; não precisa estar em cena.
    /// </summary>
    public class CharacterRunner : MonoBehaviour
    {
        static CharacterRunner instance;
        readonly HashSet<int> busy = new HashSet<int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { instance = null; }

        public static CharacterRunner Ensure()
        {
            if (instance == null)
            {
                GameObject go = new GameObject("[HorrorFPS] Characters");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<CharacterRunner>();
            }
            return instance;
        }

        public bool IsBusy(int playerIndex) { return busy.Contains(playerIndex); }

        void Update()
        {
            if (!CharacterManager.Enabled) return;
            // o próximo jogador a entrar já nasce com o prefab do personagem escolhido para ele
            PlayerInputManager pim = PlayerInputManager.instance;
            if (pim == null) return;
            CharacterDefinition d = CharacterManager.GetSelected(pim.playerCount);
            if (d != null && d.prefab != null && pim.playerPrefab != d.prefab) pim.playerPrefab = d.prefab;
        }

        /// <summary>Começa a troca. 'delayFrames' espera alguns quadros antes; 'allowCarry' deixa o personagem de mochila "Carry" herdar os itens do anterior.</summary>
        public bool Begin(PlayerContext player, CharacterDefinition def, int delayFrames, bool allowCarry)
        {
            if (player == null || def == null) return false;
            int index = player.PlayerIndex;
            if (!busy.Add(index)) return false;
            StartCoroutine(Swap(player, def, delayFrames, allowCarry, index));
            return true;
        }

        IEnumerator Swap(PlayerContext old, CharacterDefinition def, int delayFrames, bool allowCarry, int index)
        {
            for (int i = 0; i < delayFrames; i++) yield return null;
            if (old == null || !old.isActiveAndEnabled)
            {
                busy.Remove(index);
                yield break;
            }

            // ---- foto do jogador atual
            PlayerInput oldInput = PlayerRef.InputOf(old);
            Vector3 position = old.transform.position;
            float yaw = old.transform.eulerAngles.y;
            string scheme = oldInput != null ? oldInput.currentControlScheme : null;
            int split = oldInput != null ? oldInput.splitScreenIndex : -1;
            InputDevice[] devices = DevicesOf(oldInput);
            CharacterIdentity oldWho = old.GetComponent<CharacterIdentity>();
            InventoryOwner oldInv = old.GetComponentInChildren<InventoryOwner>(true);

            string carry = null;
            if (oldInv != null)
            {
                if (oldWho != null) oldInv.SaveToSession();        // cada personagem guarda a própria mochila
                if (allowCarry && def.inventory == InventoryTransfer.Carry) carry = oldInv.ExportTransfer();
            }

            if (old.Climber != null && old.Climber.IsClimbing) old.Climber.Release();
            if (old.Dragger != null && old.Dragger.IsDragging) old.Dragger.Release();

            // ---- libera os dispositivos e a vaga, e cria o novo jogador com o mesmo índice e os mesmos dispositivos
            GameObject oldObject = old.gameObject;
            if (oldInput != null && oldInput.user.valid) oldInput.user.UnpairDevices();
            oldObject.SetActive(false);

            PlayerInput created = Spawn(def, index, scheme, split, devices);
            if (created == null)
            {
                // não deu: devolve o jogador antigo
                oldObject.SetActive(true);
                if (oldInput != null && devices.Length > 0)
                {
                    try { oldInput.SwitchCurrentControlScheme(devices); }
                    catch (System.Exception ex) { Debug.LogException(ex); }
                }
                busy.Remove(index);
                yield break;
            }

            PlayerContext fresh = created.GetComponentInParent<PlayerContext>();
            if (fresh == null) fresh = created.GetComponentInChildren<PlayerContext>(true);
            if (fresh == null)
            {
                Debug.LogWarning("[HorrorFPS] O prefab de '" + def.name + "' não criou um PlayerContext.");
                Destroy(created.gameObject);
                oldObject.SetActive(true);
                busy.Remove(index);
                yield break;
            }

            CharacterManager.Tag(fresh, def);                  // antes do Start: o inventário já usa a mochila deste personagem
            if (PlayerModules.AutoInstall) PlayerModules.Install(fresh);
            Destroy(oldObject);

            // espera os Start dos módulos novos e então põe o jogador no lugar do anterior
            yield return null;
            yield return null;
            if (fresh == null)
            {
                busy.Remove(index);
                yield break;
            }

            fresh.TeleportTo(position, yaw);
            if (carry != null)
            {
                InventoryOwner inv = fresh.GetComponentInChildren<InventoryOwner>(true);
                if (inv != null) inv.ImportTransfer(carry, true);
            }

            busy.Remove(index);
            CharacterManager.RaiseChanged(fresh, def);
        }

        static PlayerInput Spawn(CharacterDefinition def, int index, string scheme, int split, InputDevice[] devices)
        {
            try
            {
                if (string.IsNullOrEmpty(scheme)) scheme = null;
                return PlayerInput.Instantiate(def.prefab, index, scheme, split, devices);
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex);
                return null;
            }
        }

        static InputDevice[] DevicesOf(PlayerInput input)
        {
            if (input == null) return new InputDevice[0];
            var list = input.devices;
            InputDevice[] result = new InputDevice[list.Count];
            for (int i = 0; i < result.Length; i++) result[i] = list[i];
            return result;
        }
    }
}
