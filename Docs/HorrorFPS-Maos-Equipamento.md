# HorrorFPS — Mãos e equipamento (Fase 1: mãos esquerda/direita e G · Fase 2: golpe fraco/forte)

Status: **escrito e conferido só por parser de sintaxe C#, NÃO compilado nem testado no Unity.** Se aparecer erro de compilação, mande a mensagem do Console.

Plano (em fases): (1) mãos + escolha ao equipar + tecla G **[feita]**; (2) golpe fraco/forte **[feita]**; (3) precisão da mira (mira na tela) + recuo forte e arma voando da mão com arma pesada em uma mão só **[falta]**.

## Controles (com o inventário fechado)

| Ação | Teclado e mouse | Controle |
|---|---|---|
| Equipar pelo atalho | tecla do atalho (1–9...) e depois **clique esquerdo = mão esquerda / clique direito = mão direita** (5 s para escolher; Backspace cancela) | LB e depois LT / RT |
| Usar o item da mão esquerda | botão esquerdo do mouse | LT |
| Usar o item da mão direita | botão direito do mouse | RT |
| **Golpe fraco** (arma branca) | **toque** no botão da mão | toque no gatilho |
| **Golpe forte** (arma branca) | **segurar** o botão da mão e **soltar** | segurar e soltar o gatilho |
| Duas mãos com o item da mão **direita** | **G** (toque) | RB (toque) |
| Duas mãos com o item da mão **esquerda** | **G segurado** (0,3 s) | RB segurado |
| Voltar para uma mão | G de novo | RB |
| Mirar (zoom) | **Q** (segurar) | botão Select |
| Recarregar | R | D-pad baixo |

No menu do item (inventário) há "Equipar na mão esquerda / direita", "Passar para a outra mão" e "Desequipar".

## Fase 1 — regras das mãos

- O slot `mao` é a mão **direita** (itens e saves antigos continuam valendo); a esquerda é o slot novo `mao_esq`. A lanterna continua no slot `lanterna`, à parte.
- Com **duas mãos**, o item vai para perto do centro, qualquer botão do mouse o usa e o item da outra mão fica "guardado" (modelo escondido, sem ação) até voltar para uma mão.
- `ItemEquip.grip` (Auto / Light / Heavy): Auto = arma de fogo com vários projéteis ou automática conta como **pesada**. Arma pesada em uma mão só **não recarrega** ("Precisa das duas mãos para recarregar (G)"); soltar para uma mão no meio da recarga cancela.
- R recarrega a arma da mão direita; se ela não precisa, a da esquerda.
- O clique que escolhe a mão não ataca nem agarra objeto (`InventoryOwner.HandPickBlocksInput`, `BlocksGrab`).
- O modo de duas mãos vai para o save (`OwnerSave.twoHand`).

## Fase 2 — golpe fraco e forte (só arma branca)

- **Toque** = golpe **fraco**: o de sempre, rápido, dano `damage`. Sai ao soltar o botão (um quadro de atraso).
- **Segurar** = a arma vai para trás. Passando `heavyHoldSeconds` (0,3 s) ela termina de armar, a pose aumenta e toca um aviso curto: **soltar agora dá o golpe forte**. Soltar antes disso dá o golpe fraco. Dá para ficar segurando o tempo que quiser.
- Golpe **forte**: dano × `heavyDamageMultiplier` (1,8), arco 30% mais largo, dura × `heavySwingMultiplier` (1,5), barulho × `heavyNoiseMultiplier` (1,4), gasta `heavyDurabilityCost` (2) de durabilidade ao acertar, chacoalha mais a câmera. **Errar** um golpe forte deixa a mão parada por 0,5 s (acertar, 0,25 s).
- Abrir o inventário, guardar a arma ou passar para duas mãos com a outra mão cancela a carga.
- `heavyAttack` desligado no item = o golpe sai assim que aperta (comportamento antigo, sem golpe forte).
- `ItemHit.heavy` diz à hurtbox/inimigo se o golpe foi forte.
- Valores novos em `ItemEquip` (aparecem no Inspector em arma branca): `heavyAttack`, `heavyHoldSeconds`, `heavyDamageMultiplier`, `heavySwingMultiplier`, `heavyNoiseMultiplier`, `heavyDurabilityCost`. Itens já existentes ganham os padrões sozinhos.

## Arquivos alterados

| Arquivo | Mudança |
|---|---|
| `Inventory/HandSide.cs` (novo) | enum None / Right / Left |
| `Inventory/InventoryOwner.cs` | `EquipToHand`, `HandOf`, `GetHandItem`, `Unequip`, `ToggleTwoHanded`, `TwoHandSide`, `HandsChanged`, escolha pendente, `HasAnyEquipped`, save |
| `Input/ItemInput.cs` | `UseLeft`, `UseRight`, `TwoHand`; `Aim` agora Q / Select; `UseEquipped` = qualquer botão de mão |
| `Equipment/EquipmentController.cs` | `HandState` por mão, mão esquerda espelhada, duas mãos, G toque/segurar, carga do golpe forte |
| `Equipment/EquipmentHUD.cs` | uma linha por mão (`[2 mãos]`, `(guardado)`) + lanterna |
| `Equipment/ItemHit.cs` | campo `heavy` |
| `Items/ItemEquip.cs` | `HandGrip grip`, `IsHeavy` e os campos do golpe forte |
| `Inventory/Hotbar.cs` | `Select(slot)` |
| `UI/Inventory/InventoryScreen.cs` / `.Window.cs` | atalho de item de mão abre a escolha por clique; menu com mão esquerda/direita |
| `Editor/ItemEquipDrawer.cs` | mostra `grip` e os campos do golpe forte |

## Limitações conhecidas

- `PlayerAnimItemAdapter` / animação do corpo só olham o slot `mao` (direita): item só na mão esquerda não dispara a animação de segurar, e o golpe forte não tem animação própria no corpo.
- As duas mãos compartilham o mesmo tremor (ItemTremor) e a mira vale só para a arma de fogo "principal" (duas mãos, senão direita, senão esquerda).
- No controle, a mira ficou no botão Select por falta de botão livre; ajuste em `ItemInput.Build()`.
- O golpe forte não gasta stamina e não tem bônus por segurar a arma com duas mãos (dá para somar depois).
- Chamar `owner.ToggleEquip(item)` sem escolher a mão equipa na direita (ou na esquerda, se a direita estiver ocupada e a esquerda livre).
