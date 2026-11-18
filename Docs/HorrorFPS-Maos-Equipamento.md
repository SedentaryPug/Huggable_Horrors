<<<<<<< HEAD
# HorrorFPS — Mãos e equipamento (Fase 1: mãos esquerda/direita e G · Fase 2: golpe fraco/forte · Fase 3: precisão, recuo e arma escapando · Fase 4: lanterna na mão e mira só com duas mãos)

Status: **escrito e conferido só por parser de sintaxe C#, NÃO compilado nem testado no Unity.** Se aparecer erro de compilação, mande a mensagem do Console.

Plano (em fases): (1) mãos + escolha ao equipar + tecla G **[feita]**; (2) golpe fraco/forte **[feita]**; (3) precisão da mira (mira na tela) + recuo forte e arma voando da mão com arma pesada em uma mão só **[feita]**; (4) lanterna como item de mão (escolhe a mão, liga com o botão da mão, o feixe pode denunciar o jogador a certos monstros) + mira só com as duas mãos (sem tecla de mirar) + modo deus de teste **[feita, não compilada]**. Detalhes da Fase 4 mais abaixo e em `HorrorFPS-Lanterna-Mira-ModoDeus`.
=======
# HorrorFPS — Mãos e equipamento (Fase 1: mãos esquerda/direita e G · Fase 2: golpe fraco/forte · Fase 3: precisão, recuo e arma escapando)

Status: **escrito e conferido só por parser de sintaxe C#, NÃO compilado nem testado no Unity.** Se aparecer erro de compilação, mande a mensagem do Console.

Plano (em fases): (1) mãos + escolha ao equipar + tecla G **[feita]**; (2) golpe fraco/forte **[feita]**; (3) precisão da mira (mira na tela) + recuo forte e arma voando da mão com arma pesada em uma mão só **[feita]**.
>>>>>>> 6aae17dc446ca29dc38eed6b0a42f50d85e686ae

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
<<<<<<< HEAD
| Mirar (zoom) | **não há tecla**: a arma de fogo mira sozinha quando está nas **duas mãos** (G) | idem (RB) |
| Ligar / desligar a lanterna | o **botão da mão** em que ela está (esquerdo = mão esquerda, direito = mão direita) | LT / RT da mão dela |
| Largar o item equipado | **Q** (toque = mão direita, segurar = mão esquerda; nas duas mãos, larga esse item) | D-pad cima |
=======
| Mirar (zoom) | **Q** (segurar) | botão Select |
>>>>>>> 6aae17dc446ca29dc38eed6b0a42f50d85e686ae
| Recarregar | R | D-pad baixo |

No menu do item (inventário) há "Equipar na mão esquerda / direita", "Passar para a outra mão" e "Desequipar".

## Fase 1 — regras das mãos

<<<<<<< HEAD
- O slot `mao` é a mão **direita** (itens e saves antigos continuam valendo); a esquerda é o slot novo `mao_esq`. A **lanterna** também é item de mão (Fase 4): ocupa uma das duas mãos, como qualquer outro item.
=======
- O slot `mao` é a mão **direita** (itens e saves antigos continuam valendo); a esquerda é o slot novo `mao_esq`. A lanterna continua no slot `lanterna`, à parte.
>>>>>>> 6aae17dc446ca29dc38eed6b0a42f50d85e686ae
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

<<<<<<< HEAD
`(spread da arma + moveSpread × movimento + bloom) × mirar (só nas duas mãos) × pegada (+ tremor, em arma de um projétil)`

- **Movimento** (`moveSpread`): parado 0, agachado ~0,2, andando ~0,5, correndo e no ar 1 (vem do `PlayerMotionProbe.Exertion`, suavizado). Sem o `PlayerMotionProbe` no jogador, esse termo fica 0.
- **Tiros seguidos / bloom** (`bloomPerShot`, `bloomMax`, `bloomRecover`): cada tiro abre a mira para o próximo; passa o intervalo do tiro e ela fecha `bloomRecover` graus por segundo (mais devagar com a mão tremendo). O **primeiro** tiro depois de parar é sempre o mais certeiro.
- **Mirar** (arma nas duas mãos) multiplica tudo por `aimSpreadMultiplier` (já existia).
=======
`(spread da arma + moveSpread × movimento + bloom) × mirar (Q) × pegada (+ tremor, em arma de um projétil)`

- **Movimento** (`moveSpread`): parado 0, agachado ~0,2, andando ~0,5, correndo e no ar 1 (vem do `PlayerMotionProbe.Exertion`, suavizado). Sem o `PlayerMotionProbe` no jogador, esse termo fica 0.
- **Tiros seguidos / bloom** (`bloomPerShot`, `bloomMax`, `bloomRecover`): cada tiro abre a mira para o próximo; passa o intervalo do tiro e ela fecha `bloomRecover` graus por segundo (mais devagar com a mão tremendo). O **primeiro** tiro depois de parar é sempre o mais certeiro.
- **Mirar (Q)** multiplica tudo por `aimSpreadMultiplier` (já existia).
>>>>>>> 6aae17dc446ca29dc38eed6b0a42f50d85e686ae
- **Pegada:** duas mãos em qualquer arma de fogo = × `twoHandSpreadFactor` (0,7, no controlador); arma **pesada** em uma mão só = × `oneHandSpreadMultiplier` (2); arma leve em uma mão = × 1.

**Mira na tela** (`EquipmentHUD`): quatro traços em volta do centro. O afastamento é exatamente o cone do próximo tiro (`EquipmentController.GetCrosshair`), convertido em pixels com o campo de visão atual, então fecha ao mirar e abre ao correr e ao atirar em rajada. Com a mão tremendo a mira **acompanha por onde a arma realmente aponta** (o ponto do `PlayerHUD` continua no centro da câmera). Fica vermelha sem balas no carregador e apagada recarregando. Só aparece com arma de fogo na mão. No Inspector do `EquipmentHUD`: `crosshair` = Sempre / Só mirando / Desligada, tamanho dos traços, folga mínima, seguir a deriva e suavização. Desligar a mira não muda a dispersão do tiro.

**Coice.** Duas mãos: coice × `twoHandRecoilFactor` (0,8). Arma **pesada com uma mão só**: coice × `oneHandRecoilMultiplier` (2): a câmera sobe e balança bem mais (`DynamicHeadBob.Recoil` agora aceita força até 2) e o modelo na mão dá um coice exagerado. Ao atirar assim, aparece de tempos em tempos o aviso "Coice forte com uma mão só: segure com as duas (G)." (no máximo a cada `oneHandHintSeconds`).

**Arma escapando.** Cada tiro com arma pesada em uma mão só tem `oneHandDropChance` de chance (0,25 na espingarda; sobe até +50% com a mão tremendo) de o coice arrancar a arma da mão: ela sai para cima e para o lado da mão que a segurava, vira um item no chão (`InventoryOwner.Drop` + `WorldItem.Throw`, pode ser pega de novo, com munição e durabilidade preservadas), faz barulho de queda (raio 12 m) e a mão fica vazia. Item com `canDrop` desligado só sai da mão e continua na mochila. O tiro que a arrancou acontece normalmente.

Valores novos em `ItemEquip` (Inspector, arma de fogo, seção "Precisão e arma pesada"): `aimSpreadMultiplier` (já existia, agora aparece), `bloomPerShot`, `bloomMax`, `bloomRecover`, `moveSpread`, `oneHandSpreadMultiplier`, `oneHandRecoilMultiplier`, `oneHandDropChance`. Os botões Pistola / Espingarda / Automática já trazem valores prontos (pistola: sem penalidade com uma mão; espingarda: dispersão × 1,8, coice × 2, 25% de escapar; automática: × 1,8 de dispersão, × 1,8 de coice, 12% de escapar). Itens já existentes ganham os padrões dos campos sozinhos (dispersão × 2, coice × 2, 20% de escapar nas armas pesadas), então confira as armas que você já ajustou. No `EquipmentController`: `twoHandSpreadFactor`, `twoHandRecoilFactor`, `moveSpreadFollow`, `oneHandHintSeconds`.

<<<<<<< HEAD
Para testar: espingarda numa mão só (clique escolhe a mão ao equipar), atirar várias vezes; depois G para segurar com as duas mãos (aí ela mira sozinha: zoom e mira fechada) e comparar; correr e atirar.

## Fase 4 — lanterna como item de mão, mira só com duas mãos

**Lanterna.** Agora é um item de mão como os outros: equipe pelo atalho e escolha a mão com o clique (esquerdo / direito) ou pelo menu do item no inventário. **Não existe mais a tecla F / D-pad cima para ligar.** O botão da mão em que ela está (esquerdo do mouse / LT para a mão esquerda, direito / RT para a direita) liga e desliga. Regras:

- Ocupa uma mão. Não vai nas duas mãos (G diz "A lanterna se segura com uma mão só.").
- O feixe é uma luz presa ao modelo da mão: acompanha o balanço e o tremor do batimento, e aponta para onde a mão aponta. Na mão esquerda o modelo é espelhado.
- Tirar da mão para a mochila (desequipar, trocar de mão) **apaga** a lanterna; **largá-la no chão (Q) a mantém acesa**. Se a outra mão passa a segurar um item com as duas mãos, a lanterna fica guardada: sem luz e **sem gastar bateria** até voltar.
- Bateria, falha da luz com pouca bateria, "Sem pilhas." e as pilhas (`flashlight_recharge`) funcionam como antes.
- O botão da mão passa a ser da lanterna, então ela **sempre bloqueia o arrastar objetos** (`InventoryOwner.BlocksGrab`), como as outras armas. Para arrastar algo, guarde a lanterna.
- A HUD mostra a lanterna na linha da mão: `Dir  Lanterna  ligada  82%` (a linha separada da lanterna saiu).
- Itens e saves antigos: o item com `equipSlot = "lanterna"` continua valendo (`InventoryOwner.IsHandItem` reconhece o tipo Lanterna); um save antigo que a guardava no slot `lanterna` passa para uma mão livre ao carregar (direita, senão esquerda). Os botões de preset e os itens de exemplo agora criam a lanterna com `equipSlot = "mao"`.
- `EquipmentController.Lamp` (item da lanterna utilizável), `FlashlightOn` (acesa e emitindo luz) e `PlayerVisibility` (lanterna acesa = mais visível) seguem esse modelo; guardada ou sem pilhas não conta.

**Feixe que denuncia (só alguns monstros).** Campo novo no `StalkerProfile`, grupo *Lanterna do jogador*: `detectsFlashlightBeam` (desligado por padrão). Ligado, o monstro **enxerga a luz da lanterna acesa** e descobre onde o jogador está por ela: precisa da lanterna dentro do cone de visão dele, até `beamDetectRange` (40 m, bem além da visão normal), sem parede no caminho. A suspeita enche numa taxa `beamDetectStrength` (0,7) que cai um pouco com a distância e sobe × `beamLitBonus` (1,6) quando o feixe aponta para o monstro. Vale o maior entre essa visão e a visão normal do corpo, e jogador escondido continua invisível. Para dar a habilidade a um monstro, duplique o `StalkerProfile` dele e marque `detectsFlashlightBeam`; os que não marcarem só sentem a lanterna pelo fator de visibilidade, como antes. O dado do feixe vem de `EquipmentController.TryGetBeam(player, out BeamInfo)`.

**Mira só com duas mãos.** A ação `Aim` (Q / Select) foi **removida**. A arma de fogo só mira (zoom `aimFov`, dispersão `aimSpreadMultiplier`, arma no centro) enquanto está nas duas mãos (G); com uma mão não há zoom. A mira na tela continua aparecendo com a arma em uma ou duas mãos; o modo "Só mirando" do `EquipmentHUD` agora significa "só com as duas mãos". Tutoriais antigos com `{Aim}` mostram o botão do G e `{Flashlight}` o botão da mão direita.

**Modo deus.** `Player/GodMode.cs` (instalado sozinho, só no Editor e em Development Build; nasce tudo desligado): F1 / Select liga ou desliga tudo, F2 invencível, F3 invisível para os monstros, F4 estamina infinita, F5 recursos infinitos (munição sem recarregar, bateria da lanterna, durabilidade). Um aviso no canto da tela do jogador mostra o que está ligado. Invencível usa `StatsComponent.Immortal` (não leva dano e `Kill()` não mata); invisível usa `PlayerCheats` (`StalkerBridge.IsHidden` passa a valer verdadeiro e o barulho dele não chega ao monstro). Detalhes em `HorrorFPS-Lanterna-Mira-ModoDeus`.

**Largar o item equipado.** Q / D-pad cima larga o item da mão no chão **com o estado dele** (lanterna continua acesa e gastando bateria, arma mantém a munição do carregador, durabilidade preservada). Detalhes em `HorrorFPS-Lanterna-Mira-ModoDeus` (seção 1b).
=======
Para testar: espingarda numa mão só (clique escolhe a mão ao equipar), atirar várias vezes; depois G para segurar com as duas mãos e comparar; correr e atirar; mirar com Q.
>>>>>>> 6aae17dc446ca29dc38eed6b0a42f50d85e686ae

## Arquivos alterados

| Arquivo | Mudança |
|---|---|
| `Inventory/HandSide.cs` (novo) | enum None / Right / Left |
| `Inventory/InventoryOwner.cs` | `EquipToHand`, `HandOf`, `GetHandItem`, `Unequip`, `ToggleTwoHanded`, `TwoHandSide`, `HandsChanged`, escolha pendente, `HasAnyEquipped`, save |
<<<<<<< HEAD
| `Input/ItemInput.cs` | `UseLeft`, `UseRight`, `TwoHand`; `UseEquipped` = qualquer botão de mão; Fase 4: removidas `Aim` e `ToggleLight`, novas `GodMaster` / `GodInvincible` / `GodInvisible` / `GodStamina` / `GodAmmo` (F1..F5) |
| `Equipment/EquipmentController.cs` | `HandState` por mão, mão esquerda espelhada, duas mãos, G toque/segurar, carga do golpe forte; Fase 3: `CurrentSpread`, bloom, `GetCrosshair`, coice por pegada, `FlingFromHand`; Fase 4: lanterna por mão (`AttachBeam`, `TickLamp`), `AimHand` só com duas mãos, `FirearmHand`, `TryGetBeam` / `BeamInfo`, `FlashlightOn` |
| `Equipment/EquipmentHUD.cs` | uma linha por mão (`[2 mãos]`, `(guardado)`, bateria da lanterna); Fase 3: mira na tela |
| `Inventory/WorldItem.cs` | Fase 3: `Throw(velocity, spin)`; Fase 4: luz da lanterna largada (`WorldItemLight`, novo) e apagar ao pegar |
=======
| `Input/ItemInput.cs` | `UseLeft`, `UseRight`, `TwoHand`; `Aim` agora Q / Select; `UseEquipped` = qualquer botão de mão |
| `Equipment/EquipmentController.cs` | `HandState` por mão, mão esquerda espelhada, duas mãos, G toque/segurar, carga do golpe forte; Fase 3: `CurrentSpread`, bloom, `GetCrosshair`, coice por pegada, `FlingFromHand` |
| `Equipment/EquipmentHUD.cs` | uma linha por mão (`[2 mãos]`, `(guardado)`) + lanterna; Fase 3: mira na tela |
| `Inventory/WorldItem.cs` | Fase 3: `Throw(velocity, spin)` |
>>>>>>> 6aae17dc446ca29dc38eed6b0a42f50d85e686ae
| `Body/DynamicHeadBob.cs` / `CameraImpulse.cs` | Fase 3: `Recoil` aceita força até 2 |
| `Equipment/ItemHit.cs` | campo `heavy` |
| `Items/ItemEquip.cs` | `HandGrip grip`, `IsHeavy` e os campos do golpe forte; Fase 3: bloom, `moveSpread`, `oneHand*` |
| `Inventory/Hotbar.cs` | `Select(slot)` |
| `UI/Inventory/InventoryScreen.cs` / `.Window.cs` | atalho de item de mão abre a escolha por clique; menu com mão esquerda/direita |
| `Editor/ItemEquipDrawer.cs` | mostra `grip` e os campos do golpe forte; Fase 3: seção "Precisão e arma pesada" |
<<<<<<< HEAD
| `Editor/EquipPresets.cs` | Fase 3: valores de precisão e de uma mão só nos botões Pistola / Espingarda / Automática; Fase 4: Lanterna com `equipSlot = "mao"` e `blocksGrab` ligado |
| `Inventory/InventoryOwner.cs` (Fase 4) | `IsHandItem` reconhece a lanterna, `BlocksGrab`, G recusa a lanterna, save antigo move a lanterna para uma mão |
| `Stalker/StalkerProfile.cs`, `StalkerSenses.cs`, `StalkerBridge.cs` (Fase 4) | campos `detectsFlashlightBeam` etc., `BeamVisibility`, invisibilidade de teste |
| `Stats/StatsComponent.cs`, `Core/PlayerCheats.cs` (novo), `Player/GodMode.cs` (novo), `Core/PlayerModules.cs` (Fase 4) | modo deus |
| `Body/PlayerVisibility.cs`, `Animation/PlayerAnimItemAdapter.cs`, `Tutorials/*`, `Items/ItemEquip.cs`, `Editor/SampleItems.cs` (Fase 4) | seguem a lanterna na mão e a mira sem tecla |
=======
| `Editor/EquipPresets.cs` | Fase 3: valores de precisão e de uma mão só nos botões Pistola / Espingarda / Automática |
>>>>>>> 6aae17dc446ca29dc38eed6b0a42f50d85e686ae

## Arremessáveis (garrafa, sinalizador, rádio)

O tipo de equipamento `Throwable` usa o mesmo botão de mão: **tocar** joga perto, **segurar** carrega a força e **soltar** joga uma unidade (a mão mostra o braço indo para trás e voltando). O objeto jogado chama o monstro com o barulho dele mesmo, não do jogador. Detalhes, campos e como montar: [`HorrorFPS-Distracoes`](HorrorFPS-Distracoes.md).

## Limitações conhecidas

<<<<<<< HEAD
- `PlayerAnimItemAdapter` / animação do corpo guardam um único `handItem` (o último equipado, em qualquer mão); a lanterna em qualquer mão só marca `hasFlashlight`. O golpe forte não tem animação própria no corpo.
- As duas mãos compartilham o mesmo tremor (ItemTremor) e o zoom da mira vale só para a arma de fogo nas duas mãos; a mira na tela acompanha a arma "principal" (duas mãos, senão direita, senão esquerda); o bloom é de cada mão.
- A arma que escapa não tem animação própria no corpo: some da mão e aparece no chão.
- Só arma de fogo escapa da mão: arma branca pesada (taco) com uma mão só não tem penalidade de coice ou de queda.
- Com a arma em uma mão só não há zoom de mira (de propósito); quem quiser mirar segura com as duas mãos (G).
- Duas lanternas, uma em cada mão, funcionam, mas `PlayerVisibility` e a animação consideram só a acesa (ou a da direita).
=======
- `PlayerAnimItemAdapter` / animação do corpo só olham o slot `mao` (direita): item só na mão esquerda não dispara a animação de segurar, e o golpe forte não tem animação própria no corpo.
- As duas mãos compartilham o mesmo tremor (ItemTremor) e a mira vale só para a arma de fogo "principal" (duas mãos, senão direita, senão esquerda); o bloom, porém, é de cada mão.
- A arma que escapa não tem animação própria no corpo: some da mão e aparece no chão.
- Só arma de fogo escapa da mão: arma branca pesada (taco) com uma mão só não tem penalidade de coice ou de queda.
- No controle, a mira ficou no botão Select por falta de botão livre; ajuste em `ItemInput.Build()`.
>>>>>>> 6aae17dc446ca29dc38eed6b0a42f50d85e686ae
- O golpe forte não gasta stamina e não tem bônus por segurar a arma com duas mãos (dá para somar depois).
- A mira na tela usa o campo de visão da câmera do jogador; em telas divididas ela segue o viewport de cada jogador.
- Chamar `owner.ToggleEquip(item)` sem escolher a mão equipa na direita (ou na esquerda, se a direita estiver ocupada e a esquerda livre).
