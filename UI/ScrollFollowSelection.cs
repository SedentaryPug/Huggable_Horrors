using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>Mantém o item selecionado (teclado/controle) visível dentro de um ScrollRect.</summary>
    [RequireComponent(typeof(ScrollRect))]
    public class ScrollFollowSelection : MonoBehaviour
    {
        ScrollRect scroll;
        GameObject lastSelected;

        void Awake() { scroll = GetComponent<ScrollRect>(); }

        void Update()
        {
            EventSystem es = EventSystem.current;
            if (es == null || scroll.content == null || scroll.viewport == null) return;
            GameObject selected = es.currentSelectedGameObject;
            if (selected == null || selected == lastSelected) return;
            lastSelected = selected;
            if (!selected.transform.IsChildOf(scroll.content)) return;

            RectTransform item = selected.GetComponent<RectTransform>();
            if (item == null) return;

            Canvas.ForceUpdateCanvases();
            float contentHeight = scroll.content.rect.height;
            float viewHeight = scroll.viewport.rect.height;
            if (contentHeight <= viewHeight) return;

            // posição do item em relação ao topo do conteúdo
            float itemTop = -(scroll.content.InverseTransformPoint(item.TransformPoint(new Vector3(0f, item.rect.yMax, 0f))).y);
            float itemBottom = itemTop + item.rect.height;
            float viewTop = (1f - scroll.verticalNormalizedPosition) * (contentHeight - viewHeight);
            float viewBottom = viewTop + viewHeight;

            if (itemTop < viewTop) viewTop = itemTop;
            else if (itemBottom > viewBottom) viewTop = itemBottom - viewHeight;
            else return;

            scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(viewTop / (contentHeight - viewHeight));
        }
    }
}
