using System.Collections.Generic;
using UnityEngine;

namespace HorrorFPS
{
    /// <summary>Uma "janela" de um contêiner dentro da tela de inventário (grade ou lista).</summary>
    public abstract class ContainerView
    {
        protected readonly InventoryScreen screen;
        public ItemContainer Container { get; protected set; }
        public RectTransform Root { get; protected set; }
        public abstract float Height { get; }

        protected ContainerView(InventoryScreen screen, ItemContainer container)
        {
            this.screen = screen;
            Container = container;
        }

        /// <summary>Refaz o desenho a partir do estado atual do contêiner.</summary>
        public abstract void Rebuild();

        public bool Contains(Vector2 screenPoint) { return InvUI.Hit(Root, screenPoint); }

        /// <summary>Item sob o ponto (ou nulo).</summary>
        public abstract ItemInstance PickAt(Vector2 screenPoint);

        /// <summary>Atualiza o destaque: item sob o cursor, ou a prévia de onde o item da mão cairia.</summary>
        public abstract void UpdateHover(Vector2 screenPoint, ItemInstance held, int heldRotation);

        public abstract void ClearHover();

        /// <summary>Tenta soltar o item da mão no ponto. Devolve o resultado.</summary>
        public abstract TransferResult DropHeld(ItemInstance held, int heldRotation, Vector2 screenPoint);

        /// <summary>Solta UMA unidade da pilha na mão (botão direito, como no Minecraft/Terraria). A pilha continua na mão com o resto.</summary>
        public virtual TransferResult DropOne(ItemInstance held, int heldRotation, Vector2 screenPoint)
        {
            return TransferResult.Invalid;
        }

        /// <summary>
        /// Em qual célula DA PEÇA (na rotação atual dela) o ponto cai. Usado para o arrasto começar exatamente por ali.
        /// Falso se o contêiner não tem células (lista) ou o ponto não está sobre a peça.
        /// </summary>
        public virtual bool TryGetGrabCell(Vector2 screenPoint, ItemInstance item, out Vector2Int cell)
        {
            cell = Vector2Int.zero;
            return false;
        }

        /// <summary>
        /// Onde a peça cairia se fosse solta no ponto (o mesmo cálculo da prévia verde/vermelha): as células e a caixa que as envolve,
        /// em coordenadas da camada (origem no canto superior esquerdo, y para baixo). Alimenta as animações de encaixou / não coube.
        /// Falso se este contêiner não tem células (lista, bandeja).
        /// </summary>
        public virtual bool GetDropFootprint(ItemInstance held, int heldRotation, Vector2 screenPoint, RectTransform layer, List<Rect> cells, out Rect box)
        {
            cells.Clear();
            box = new Rect();
            return false;
        }

        /// <summary>Retângulo atual de um item deste contêiner, em coordenadas da camada (para a peça "voltar ao lugar"). Falso se não está visível.</summary>
        public virtual bool GetItemBox(ItemInstance item, RectTransform layer, out Rect box)
        {
            box = new Rect();
            return false;
        }

        /// <summary>Move o cursor um passo (dir: x -1/+1 esquerda/direita, y -1/+1 cima/baixo). Falso se bateu na borda deste contêiner.</summary>
        public virtual bool Step(Vector2 from, Vector2Int dir, out Vector2 to)
        {
            to = from;
            return false;
        }

        /// <summary>Ponto (tela) deste contêiner mais próximo de 'from': usado para pular de um contêiner para o vizinho.</summary>
        public virtual Vector2 EntryPoint(Vector2 from)
        {
            return from;
        }

        public virtual void Scroll(float amount) { }

        public virtual void Destroy()
        {
            if (Root != null) Object.Destroy(Root.gameObject);
        }
    }
}
