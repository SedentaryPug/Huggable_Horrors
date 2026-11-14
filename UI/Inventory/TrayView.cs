using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace HorrorFPS
{
    /// <summary>
    /// Bandeja de remanejo: uma área livre ao lado da mochila onde as peças ficam soltas, com uma física leve (deslizam com inércia,
    /// quicam nas bordas e se empurram). Serve para tirar um item do caminho enquanto você reorganiza a mochila.
    /// As peças são itens de verdade (contêiner "tray" do jogador) e voltam para o inventário ao fechar a tela.
    /// </summary>
    public class TrayView : ContainerView
    {
        const float HeaderH = 22f;
        const float Damping = 3.2f;         // quanto a peça perde de velocidade por segundo (maior = para mais rápido)
        const float WallBounce = 0.4f;
        const float HitBounce = 0.3f;
        const float MaxSpeed = 900f;

        class Body
        {
            public ItemInstance item;
            public Vector2 pos;             // canto superior esquerdo, em unidades locais da bandeja (y para baixo)
            public Vector2 vel;
            public RectTransform rt;
        }

        readonly ListContainer list;
        readonly RectTransform body;
        readonly float width, bodyH, cell;
        readonly Text header;
        readonly Text hint;
        readonly List<Body> bodies = new List<Body>();

        bool hasSpawn;
        Vector2 spawnPos, spawnVel;

        public override float Height { get { return HeaderH + bodyH + 4f; } }

        /// <summary>Tamanho da célula das peças na bandeja (menor que o da mochila).</summary>
        public float CellSize { get { return cell; } }

        public TrayView(InventoryScreen screen, ListContainer list, RectTransform parent, float x, float y, float width, float height, float cell)
            : base(screen, list)
        {
            this.list = list;
            this.width = width;
            this.cell = cell;
            bodyH = Mathf.Max(120f, height - HeaderH - 4f);

            Root = InvUI.Rect(parent, "Tray");
            InvUI.TopLeft(Root, x, y, width, Height);

            header = InvUI.Label(Root, "header", "Bandeja", 12, TextAnchor.MiddleLeft, InvUI.TextDim);
            InvUI.TopLeft(header.rectTransform, 2f, 0f, width, HeaderH);

            Image frame = InvUI.Img(Root, "frame", InvUI.PanelLight);
            InvUI.TopLeft(frame.rectTransform, 0f, HeaderH, width, bodyH + 4f);

            body = InvUI.Rect(Root, "body");
            InvUI.TopLeft(body, 0f, HeaderH + 2f, width, bodyH);

            hint = InvUI.Label(body, "hint", "Solte aqui as peças que estão atrapalhando.\nElas voltam para a mochila ao fechar.", 11, TextAnchor.MiddleCenter, InvUI.TextDim);
            InvUI.Stretch(hint.rectTransform, 8f, 8f, 8f, 8f);

            Rebuild();
        }

        // ------------------------------------------------------------------ desenho
        public override void Rebuild()
        {
            for (int i = bodies.Count - 1; i >= 0; i--)
            {
                Body b = bodies[i];
                if (b.item == null || b.item.container != list)
                {
                    if (b.rt != null) Object.Destroy(b.rt.gameObject);
                    bodies.RemoveAt(i);
                }
            }

            IReadOnlyList<ItemInstance> items = list.Items;
            for (int i = 0; i < items.Count; i++)
            {
                if (FindBody(items[i]) != null) continue;
                Body nb = new Body();
                nb.item = items[i];
                if (hasSpawn) { nb.pos = spawnPos; nb.vel = spawnVel; hasSpawn = false; }
                else nb.pos = FreeSpot(items[i]);
                ClampInside(nb);
                bodies.Add(nb);
            }

            for (int i = 0; i < bodies.Count; i++)
            {
                Body b = bodies[i];
                if (b.rt != null) Object.Destroy(b.rt.gameObject);
                ItemInstance it = b.item;
                b.rt = ItemVisual.BuildShape(body, it, it.rotation, cell, it.data.color, it == screen.Held ? 0.35f : 1f);
                ItemVisual.AddBadges(b.rt, it, it.rotation, cell, false);
                ApplyPos(b);
            }

            header.text = "Bandeja  <color=#8a8a80>" + bodies.Count + (bodies.Count == 1 ? " peça" : " peças") + "</color>";
            hint.gameObject.SetActive(bodies.Count == 0);
        }

        Body FindBody(ItemInstance it)
        {
            for (int i = 0; i < bodies.Count; i++) if (bodies[i].item == it) return bodies[i];
            return null;
        }

        Vector2 SizeOf(Body b)
        {
            Vector2Int s = b.item.data.shape.GetSize(b.item.rotation);
            return new Vector2(s.x * cell, s.y * cell);
        }

        void ApplyPos(Body b)
        {
            if (b.rt != null) b.rt.anchoredPosition = new Vector2(b.pos.x, -b.pos.y);
        }

        void ClampInside(Body b)
        {
            Vector2 sz = SizeOf(b);
            b.pos.x = Mathf.Clamp(b.pos.x, 0f, Mathf.Max(0f, width - sz.x));
            b.pos.y = Mathf.Clamp(b.pos.y, 0f, Mathf.Max(0f, bodyH - sz.y));
        }

        /// <summary>Primeiro canto da bandeja onde a peça nova não cai sobre outra (se não houver, o canto superior esquerdo).</summary>
        Vector2 FreeSpot(ItemInstance it)
        {
            Vector2Int s = it.data.shape.GetSize(it.rotation);
            Vector2 sz = new Vector2(s.x * cell, s.y * cell);
            float step = cell * 0.5f;
            for (float yy = 4f; yy + sz.y <= bodyH; yy += step)
                for (float xx = 4f; xx + sz.x <= width; xx += step)
                {
                    bool free = true;
                    for (int i = 0; i < bodies.Count && free; i++)
                    {
                        Vector2 o = SizeOf(bodies[i]);
                        free = !(xx < bodies[i].pos.x + o.x && xx + sz.x > bodies[i].pos.x && yy < bodies[i].pos.y + o.y && yy + sz.y > bodies[i].pos.y);
                    }
                    if (free) return new Vector2(xx, yy);
                }
            return new Vector2(4f, 4f);
        }

        // ------------------------------------------------------------------ física
        /// <summary>Chamado todo quadro com a tela aberta: desliza, quica nas bordas e empurra as outras peças.</summary>
        public void Tick(float dt)
        {
            if (bodies.Count == 0) return;
            dt = Mathf.Clamp(dt, 0f, 1f / 30f);
            float damp = Mathf.Exp(-Damping * dt);

            for (int i = 0; i < bodies.Count; i++)
            {
                Body b = bodies[i];
                if (b.item == screen.Held) { b.vel = Vector2.zero; continue; }       // a peça na mão fica parada
                b.pos += b.vel * dt;
                b.vel *= damp;
                if (b.vel.sqrMagnitude < 9f) b.vel = Vector2.zero;
                Wall(b);
            }

            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < bodies.Count; i++)
                    for (int j = i + 1; j < bodies.Count; j++)
                        Collide(bodies[i], bodies[j]);
                for (int i = 0; i < bodies.Count; i++) Wall(bodies[i]);
            }

            for (int i = 0; i < bodies.Count; i++) ApplyPos(bodies[i]);
        }

        void Wall(Body b)
        {
            Vector2 sz = SizeOf(b);
            float maxX = Mathf.Max(0f, width - sz.x), maxY = Mathf.Max(0f, bodyH - sz.y);
            if (b.pos.x < 0f) { b.pos.x = 0f; if (b.vel.x < 0f) b.vel.x = -b.vel.x * WallBounce; }
            else if (b.pos.x > maxX) { b.pos.x = maxX; if (b.vel.x > 0f) b.vel.x = -b.vel.x * WallBounce; }
            if (b.pos.y < 0f) { b.pos.y = 0f; if (b.vel.y < 0f) b.vel.y = -b.vel.y * WallBounce; }
            else if (b.pos.y > maxY) { b.pos.y = maxY; if (b.vel.y > 0f) b.vel.y = -b.vel.y * WallBounce; }
        }

        void Collide(Body a, Body b)
        {
            Vector2 sa = SizeOf(a), sb = SizeOf(b);
            float ox = Mathf.Min(a.pos.x + sa.x, b.pos.x + sb.x) - Mathf.Max(a.pos.x, b.pos.x);
            float oy = Mathf.Min(a.pos.y + sa.y, b.pos.y + sb.y) - Mathf.Max(a.pos.y, b.pos.y);
            if (ox <= 0f || oy <= 0f) return;

            bool fa = a.item == screen.Held, fb = b.item == screen.Held;     // a peça na mão não é empurrada
            if (fa && fb) return;
            float wa = fa ? 0f : (fb ? 1f : 0.5f);
            float wb = fb ? 0f : (fa ? 1f : 0.5f);

            if (ox < oy)
            {
                float dir = (a.pos.x + sa.x * 0.5f) < (b.pos.x + sb.x * 0.5f) ? -1f : 1f;      // para onde A sai
                a.pos.x += dir * ox * wa;
                b.pos.x -= dir * ox * wb;
                Bounce(ref a.vel.x, ref b.vel.x, fa, fb);
            }
            else
            {
                float dir = (a.pos.y + sa.y * 0.5f) < (b.pos.y + sb.y * 0.5f) ? -1f : 1f;
                a.pos.y += dir * oy * wa;
                b.pos.y -= dir * oy * wb;
                Bounce(ref a.vel.y, ref b.vel.y, fa, fb);
            }
        }

        static void Bounce(ref float va, ref float vb, bool fa, bool fb)
        {
            float avg = (va + vb) * 0.5f;
            if (!fa) va = avg - (va - avg) * HitBounce;
            if (!fb) vb = avg - (vb - avg) * HitBounce;
        }

        // ------------------------------------------------------------------ ponteiro
        bool LocalOf(Vector2 screenPoint, out Vector2 local)
        {
            return InvUI.ToTopLeftLocal(body, screenPoint, out local);
        }

        bool HitBody(Body b, Vector2 local, out Vector2Int rel)
        {
            rel = new Vector2Int(Mathf.FloorToInt((local.x - b.pos.x) / cell), Mathf.FloorToInt((local.y - b.pos.y) / cell));
            return b.item.data.shape.Contains(b.item.rotation, rel.x, rel.y);
        }

        public override ItemInstance PickAt(Vector2 screenPoint)
        {
            Vector2 local;
            if (!LocalOf(screenPoint, out local)) return null;
            for (int i = bodies.Count - 1; i >= 0; i--)
            {
                Vector2Int rel;
                if (HitBody(bodies[i], local, out rel)) return bodies[i].item;
            }
            return null;
        }

        public override bool TryGetGrabCell(Vector2 screenPoint, ItemInstance item, out Vector2Int cellOut)
        {
            cellOut = Vector2Int.zero;
            Body b = FindBody(item);
            Vector2 local;
            if (b == null || !LocalOf(screenPoint, out local)) return false;
            Vector2Int rel;
            if (!HitBody(b, local, out rel)) return false;
            cellOut = rel;
            return true;
        }

        public override bool GetItemBox(ItemInstance item, RectTransform layer, out Rect box)
        {
            box = new Rect();
            Body b = FindBody(item);
            if (b == null || b.rt == null || layer == null) return false;
            box = InvUI.RectInLayer(layer, b.rt);
            return true;
        }

        public override void UpdateHover(Vector2 screenPoint, ItemInstance held, int heldRotation) { }

        public override void ClearHover() { }

        public override Vector2 EntryPoint(Vector2 from)
        {
            return InvUI.TopLeftToScreen(body, new Vector2(width * 0.5f, bodyH * 0.5f));
        }

        // ------------------------------------------------------------------ receber peças
        Vector2 Desired(ItemInstance held, int rot, Vector2 local)
        {
            Vector2Int grab;
            if (screen.TryGetGrab(held, out grab)) return local - new Vector2((grab.x + 0.5f) * cell, (grab.y + 0.5f) * cell);
            Vector2Int s = held.data.shape.GetSize(rot);
            return local - new Vector2(s.x * cell * 0.5f, s.y * cell * 0.5f);
        }

        Vector2 ToLocalVelocity(Vector2 screenVel)
        {
            float k = Mathf.Max(0.0001f, body.lossyScale.x);
            Vector2 v = screenVel / k;
            // y da tela cresce para cima; o da bandeja, para baixo
            v.y = -v.y;
            return Vector2.ClampMagnitude(v, MaxSpeed);
        }

        /// <summary>
        /// Põe a peça na bandeja no ponto (arremessada com a velocidade do cursor). 'clamp' = o ponto pode estar fora da bandeja
        /// (soltou a peça em qualquer canto da tela): ela entra pela borda mais próxima.
        /// </summary>
        public TransferResult Receive(ItemInstance held, int rot, Vector2 screenPoint, Vector2 screenVel, bool clamp)
        {
            if (held == null || held.data == null) return TransferResult.Invalid;
            InventoryOwner o = list.Owner;
            if (o != null && o.IsEquipped(held)) return TransferResult.Rejected;      // equipado não fica "no ar"

            Vector2 local;
            if (!LocalOf(screenPoint, out local)) return TransferResult.Invalid;
            if (clamp) local = new Vector2(Mathf.Clamp(local.x, 0f, width), Mathf.Clamp(local.y, 0f, bodyH));
            Vector2 pos = Desired(held, rot, local);
            Vector2 vel = clamp ? Vector2.zero : ToLocalVelocity(screenVel);

            if (held.container == list)
            {
                Body b = FindBody(held);
                if (b == null) return TransferResult.Invalid;
                held.rotation = ((rot % 4) + 4) % 4;
                b.pos = pos; b.vel = vel;
                ClampInside(b);
                Rebuild();
                return TransferResult.Success;
            }

            hasSpawn = true; spawnPos = pos; spawnVel = vel;
            int oldRot = held.rotation;
            TransferResult r = ItemTransfer.Move(held, list);
            if (!ItemTransfer.Ok(r)) { hasSpawn = false; held.rotation = oldRot; return r; }
            if (held.container == list) held.rotation = ((rot % 4) + 4) % 4;      // gira já para a orientação da mão
            else hasSpawn = false;                                                  // juntou com uma pilha existente
            return r;
        }

        public override TransferResult DropHeld(ItemInstance held, int heldRotation, Vector2 screenPoint)
        {
            return Receive(held, heldRotation, screenPoint, screen.CursorVelocity, false);
        }

        public override TransferResult DropOne(ItemInstance held, int heldRotation, Vector2 screenPoint)
        {
            if (held == null || held.stack < 2 || held.container == list) return TransferResult.Invalid;
            Vector2 local;
            if (!LocalOf(screenPoint, out local)) return TransferResult.Invalid;
            hasSpawn = true; spawnPos = local - new Vector2(cell * 0.5f, cell * 0.5f); spawnVel = Vector2.zero;
            TransferResult r = ItemTransfer.Move(held, list, 1);
            if (!ItemTransfer.Ok(r)) hasSpawn = false;
            return r;
        }
    }
}
