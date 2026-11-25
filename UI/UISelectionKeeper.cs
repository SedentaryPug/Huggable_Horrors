using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Garante que sempre haja um elemento de UI selecionado enquanto existir um menu na tela, para o teclado e o controle nunca ficarem
    /// "sem navegação". A seleção se perde fácil: clicar num lugar vazio com o mouse, o botão selecionado ficar desabilitado ou escondido,
    /// uma página que troca. Quando isso acontece, volta para o ÚLTIMO elemento que estava selecionado (se ainda vale); senão vai para o
    /// primeiro botão utilizável do menu que está por cima (maior ordem de canvas, depois o mais alto e mais à esquerda na tela).
    /// Fica no EventSystem (UIFactory.EnsureEventSystem o cria). Sem nenhum Selectable ativo (em jogo) não faz nada.
    /// </summary>
    public class UISelectionKeeper : MonoBehaviour
    {
        static GameObject last;

        /// <summary>O elemento pode receber seleção agora (ativo, ligado, interagível e com navegação)?</summary>
        public static bool IsUsable(GameObject go)
        {
            if (go == null || !go.activeInHierarchy) return false;
            Selectable s = go.GetComponent<Selectable>();
            return s != null && s.enabled && s.IsInteractable() && s.navigation.mode != Navigation.Mode.None;
        }

        /// <summary>Volta para o último elemento selecionado, se ele ainda vale. Devolve false se não deu (o menu escolhe o seu primeiro).</summary>
        public static bool RestoreLast()
        {
            EventSystem es = EventSystem.current;
            if (es == null || !IsUsable(last)) return false;
            es.SetSelectedGameObject(last);
            return true;
        }

        void LateUpdate()
        {
            EventSystem es = EventSystem.current;
            if (es == null) return;

            GameObject cur = es.currentSelectedGameObject;
            if (IsUsable(cur)) { last = cur; return; }

            if (RestoreLast()) return;

            Selectable best = BestSelectable();
            if (best != null) es.SetSelectedGameObject(best.gameObject);
        }

        static Selectable BestSelectable()
        {
            Selectable best = null;
            int bestOrder = int.MinValue;
            float bestY = float.MinValue, bestX = float.MaxValue;
            Selectable[] all = Selectable.allSelectablesArray;
            for (int i = 0; i < all.Length; i++)
            {
                Selectable s = all[i];
                if (s == null || !IsUsable(s.gameObject)) continue;
                Canvas c = s.GetComponentInParent<Canvas>();
                Canvas root = c != null ? c.rootCanvas : null;
                int order = root != null ? root.sortingOrder : 0;
                Vector3 p = s.transform.position;
                bool better = order > bestOrder
                    || (order == bestOrder && (p.y > bestY + 1f || (Mathf.Abs(p.y - bestY) <= 1f && p.x < bestX)));
                if (!better) continue;
                best = s; bestOrder = order; bestY = p.y; bestX = p.x;
            }
            return best;
        }
    }
}
