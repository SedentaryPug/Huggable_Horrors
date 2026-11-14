# HorrorFPS — Mãos e equipamento (Fase 1: mãos esquerda/direita e G · Fase 2: golpe fraco/forte · Fase 3: precisão, recuo e arma escapando)

Status: **escrito e conferido só por parser de sintaxe C#, NÃO compilado nem testado no Unity.** Se aparecer erro de compilação, mande a mensagem do Console.

Plano (em fases): (1) mãos + escolha ao equipar + tecla G **[feita]**; (2) golpe fraco/forte **[feita]**; (3) precisão da mira (mira na tela) + recuo forte e arma voando da mão com arma pesada em uma mão só **[feita]**.

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

## Fase 3 — precisão da mira, recuo forte e arma escapando da mão (só arma de fogo)

**Dispersão dinâmica.** O cone onde o tiro cai (meio ângulo, em graus) agora é calculado assim, a cada quadro:

`(spread da arma + moveSpread × movimento + bloom) × mirar (Q) × pegada (+ tremor, em arma de um projétil)`

- **Movimento** (`moveSpread`): parado 0, agachado ~0,2, andando ~0,5, correndo e no ar 1 (vem do `PlayerMotionProbe.Exertion`, suavizado). Sem o `PlayerMotionProbe` no jogador, esse termo fica 0.
- **Tiros seguidos / bloom** (`bloomPerShot`, `bloomMax`, `bloomRecover`): cada tiro abre a mira para o próximo; passa o intervalo do tiro e ela fecha `bloomRecover` graus por segundo (mais devagar com a mão tremendo). O **primeiro** tiro depois de parar é sempre o mais certeiro.
- **Mirar (Q)** multiplica tudo por `aimSpreadMultiplier` (já existia).
- **Pegada:** duas mãos em qualquer arma de fogo = × `twoHandSpreadFactor` (0,7, no controlador); arma **pesada** em uma mão só = × `oneHandSpreadMultiplier` (2); arma leve em uma mão = × 1.

**Mira na tela** (`EquipmentHUD`): quatro traços em volta do centro. O afastamento é exatamente o cone do próximo tiro (`EquipmentController.GetCrosshair`), convertido em pixels com o campo de visão atual, então fecha ao mirar e abre ao correr e ao atirar em rajada. Com a mão tremendo a mira **acompanha por onde a arma realmente aponta** (o ponto do `PlayerHUD` continua no centro da câmera). Fica vermelha sem balas no carregador e apagada recarregando. Só aparece com arma de fogo na mão. No Inspector do `EquipmentHUD`: `crosshair` = Sempre / Só mirando / Desligada, tamanho dos traços, folga mínima, seguir a deriva e suavização. Desligar a mira não muda a dispersão do tiro.

**Coice.** Duas mãos: coice × `twoHandRecoilFactor` (0,8). Arma **pesada com uma mão só**: coice × `oneHandRecoilMultiplier` (2): a câmera sobe e balança bem mais (`DynamicHeadBob.Recoil` agora aceita força até 2) e o modelo na mão dá um coice exagerado. Ao atirar assim, aparece de tempos em tempos o aviso "Coice forte com uma mão só: segure com as duas (G)." (no máximo a cada `oneHandHintSeconds`).

**Arma escapando.** Cada tiro com arma pesada em uma mão só tem `oneHandDropChance` de chance (0,25 na espingarda; sobe até +50% com a mão tremendo) de o coice arrancar a arma da mão: ela sai para cima e para o lado da mão que a segurava, vira um item no chão (`InventoryOwner.Drop` + `WorldItem.Throw`, pode ser pega de novo, com munição e durabilidade preservadas), faz barulho de queda (raio 12 m) e a mão fica vazia. Item com `canDrop` desligado só sai da mão e continua na mochila. O tiro que a arrancou acontece normalmente.

Valores novos em `ItemEquip` (Inspector, arma de fogo, seção "Precisão e arma pesada"): `aimSpreadMultiplier` (já existia, agora aparece), `bloomPerShot`, `bloomMax`, `bloomRecover`, `moveSpread`, `oneHandSpreadMultiplier`, `oneHandRecoilMultiplier`, `oneHandDropChance`. Os botões Pistola / Espingarda / Automática já trazem valores prontos (pistola: sem penalidade com uma mão; espingarda: dispersão × 1,8, coice × 2, 25% de escapar; automática: × 1,8 de dispersão, × 1,8 de coice, 12% de escapar). Itens já existentes ganham os padrões dos campos sozinhos (dispersão × 2, coice × 2, 20% de escapar nas armas pesadas), então confira as armas que você já ajustou. No `EquipmentController`: `twoHandSpreadFactor`, `twoHandRecoilFactor`, `moveSpreadFollow`, `oneHandHintSeconds`.

Para testar: espingarda numa mão só (clique escolhe a mão ao equipar), atirar várias vezes; depois G para segurar com as duas mãos e comparar; correr e atirar; mirar com Q.

## Arquivos alterados

| Arquivo | Mudança |
|---|---|
| `Inventory/HandSide.cs` (novo) | enum None / Right / Left |
| `Inventory/InventoryOwner.cs` | `EquipToHand`, `HandOf`, `GetHandItem`, `Unequip`, `ToggleTwoHanded`, `TwoHandSide`, `HandsChanged`, escolha pendente, `HasAnyEquipped`, save |
| `Input/ItemInput.cs` | `UseLeft`, `UseRight`, `TwoHand`; `Aim` agora Q / Select; `UseEquipped` = qualquer botão de mão |
| `Equipment/EquipmentController.cs` | `HandState` por mão, mão esquerda espelhada, duas mãos, G toque/segurar, carga do golpe forte; Fase 3: `CurrentSpread`, bloom, `GetCrosshair`, coice por pegada, `FlingFromHand` |
| `Equipment/EquipmentHUD.cs` | uma linha por mão (`[2 mãos]`, `(guardado)`) + lanterna; Fase 3: mira na tela |
| `Inventory/WorldItem.cs` | Fase 3: `Throw(velocity, spin)` |
| `Body/DynamicHeadBob.cs` / `CameraImpulse.cs` | Fase 3: `Recoil` aceita força até 2 |
| `Equipment/ItemHit.cs` | campo `heavy` |
| `Items/ItemEquip.cs` | `HandGrip grip`, `IsHeavy` e os campos do golpe forte; Fase 3: bloom, `moveSpread`, `oneHand*` |
| `Inventory/Hotbar.cs` | `Select(slot)` |
| `UI/Inventory/InventoryScreen.cs` / `.Window.cs` | atalho de item de mão abre a escolha por clique; menu com mão esquerda/direita |
| `Editor/ItemEquipDrawer.cs` | mostra `grip` e os campos do golpe forte; Fase 3: seção "Precisão e arma pesada" |
| `Editor/EquipPresets.cs` | Fase 3: valores de precisão e de uma mão só nos botões Pistola / Espingarda / Automática |

## Limitações conhecidas

- `PlayerAnimItemAdapter` / animação do corpo só olham o slot `mao` (direita): item só na mão esquerda não dispara a animação de segurar, e o golpe forte não tem animação própria no corpo.
- As duas mãos compartilham o mesmo tremor (ItemTremor) e a mira vale só para a arma de fogo "principal" (duas mãos, senão direita, senão esquerda); o bloom, porém, é de cada mão.
- A arma que escapa não tem animação própria no corpo: some da mão e aparece no chão.
- Só arma de fogo escapa da mão: arma branca pesada (taco) com uma mão só não tem penalidade de coice ou de queda.
- No controle, a mira ficou no botão Select por falta de botão livre; ajuste em `ItemInput.Build()`.
- O golpe forte não gasta stamina e não tem bônus por segurar a arma com duas mãos (dá para somar depois).
- A mira na tela usa o campo de visão da câmera do jogador; em telas divididas ela segue o viewport de cada jogador.
- Chamar `owner.ToggleEquip(item)` sem escolher a mão equipa na direita (ou na esquerda, se a direita estiver ocupada e a esquerda livre).
