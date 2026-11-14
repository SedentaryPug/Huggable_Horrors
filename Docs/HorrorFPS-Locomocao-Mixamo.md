# Horror FPS Framework — Locomoção Humanoid com clipes do Mixamo (Y Bot)

Como pôr o **Y Bot** (ou outro modelo do Mixamo) no corpo do jogador com a **locomoção simples**: parado, andar, correr, agachar (frente/esquerda/direita), girar parado, parada depois de correr, pulo, queda, pouso e escada. O corpo é **Humanoid** (dá IK de pés, mãos e olhar), o **dono vê torso, braços e pernas** (sem a cabeça) e os **outros jogadores veem o corpo inteiro**. Complementa `HorrorFPS-Animacao-Corpo.md` (que explica o módulo todo).

> **Aviso:** escrito **sem compilar** e sem abrir o Unity. As checagens foram estáticas (referências, chaves, formato). Espere uma rodada de ajuste fino ao testar (seção 9). Se algo não compilar ou não animar, mande a mensagem do Console.

---

## 1. Por que Humanoid (e por que os `.anim` que você anexou não servem direto)

Os 14 `.anim` anexados são **Generic**: as curvas mexem nos ossos pelo **nome** (`mixamorig:Hips/...`), sem curvas de músculo. Um corpo **Humanoid** só toca clipes Humanoid (músculos). Para os dois funcionarem juntos o Unity precisa **reimportar o FBX do Mixamo como Humanoid** — os `.anim` extraídos de um FBX Generic não dá para converter.

Escolhi Humanoid porque:
- **IK de pés, mãos e olhar** (`OnAnimatorIK`, `SetIKPosition`, `SetLookAtPosition`) só existe em Humanoid. Pés no chão e mãos na escada dependem disso.
- As **máscaras** do controller (Upper Body, braço esquerdo) e o `PlayerBodyRig` já são Humanoid.
- **Qualquer modelo Humanoid** futuro toca os mesmos clipes (retarget), sem refazer o controller.

O que o menu `Animation > Mixamo: Humanoid + Locomotion Controller` faz por você: reimporta o Y Bot e os FBX de animação como Humanoid (o avatar de animação é **copiado do modelo**) e ajusta cada clipe (loop, rotação, altura, deslocamento). **Se você não tem mais os FBX**, baixe de novo no Mixamo: *Format = FBX Binary (.fbx)*, *Skin = Without Skin*, *Frames per second = 30*, *Keyframe Reduction = none*. Ponha na pasta que quiser (os nomes dos arquivos, como `walking.fbx`, `sneaking_left.fbx`, são usados para reconhecer o clipe).

## 2. Passo a passo

**Tools > Horror FPS > Animation > Mixamo: Humanoid + Locomotion Controller** (janela, quatro blocos numerados):

0. Arraste o **Y_Bot.fbx** (do Project) em *Modelo*. A *Pasta dos clipes* assume a pasta dele; mude se os FBX de animação estiverem em outro lugar.
1. **Humanoid — Converter modelo e animações para Humanoid.** Reimporta o Y Bot (Humanoid, avatar próprio) e cada FBX da pasta que tenha um clipe reconhecido (avatar copiado do modelo, ajustes por clipe). Demora uns segundos por arquivo.
2. **Clipes encontrados — Atualizar lista.** Mostra o clipe achado para cada papel, com duração, velocidade embutida (m/s) e `[Humanoid]` ou `[Generic: não anima]`. Entre um `.anim` Generic e um clipe Humanoid do mesmo papel, vence o Humanoid.
3. **Controller — Criar PlayerBody.controller com estes clipes.** Monta a layer Base (seção 4); as layers Upper Body / Flashlight / Additive continuam como no menu `Animation > Create Player Animator Controller`. O slider *Pose de agachado parado* é explicado na seção 4.
4. **Jogador — Instalar o modelo no jogador** (é o menu `Animation > Setup Selected Model As Player Body`): instancia o Y Bot no prefab do jogador (ou reaproveita a instância que já estiver lá), liga Animator + `PlayerBodyRig` + procedurais, marca os `BodyPart` e **separa a cabeça do corpo** (seção 6).
5. Dê Play com 2 jogadores: olhe para baixo (você vê torso, braços e pernas, sem cabeça) e olhe o outro jogador (corpo inteiro).

Se você recriar o controller depois de instalar, o Animator do corpo fica "Missing": rode o bloco 4 de novo.

## 3. Como cada clipe é importado

O passo 2 aplica por clipe (`Root Transform` no Import Settings ▸ Animation). Com `Apply Root Motion` **desligado** no Animator (o menu `Animation > Setup Selected Model As Player Body` já faz), o deslocamento gravado no clipe é descartado e o corpo anda **parado no lugar** — quem move o jogador é o `FirstPersonController`.

| Clipe | Papel | Loop | Rotação (*Bake Into Pose*) | Altura (*Bake Into Pose*) |
|---|---|---|---|---|
| `idle` | parado | sim | sim (corpo de frente) | sim |
| `walking`, `running` | andar / correr | sim | sim | sim |
| `sneaking_front/left/right` | agachado | sim | sim | sim |
| `falling_idle` | no ar | sim | sim | sim |
| `climbing_ladder` | escada | sim | sim | sim |
| `left_turn`, `right_turn` | girar parado | não | **não** (o giro sai do pose: só as pernas "pisam") | sim |
| `run_to_stop` | parada | não | **não** | sim |
| `jumping` | impulso do pulo | não | sim | **não** (o arco do pulo é do `CharacterController`; senão subiria em dobro) |
| `landing` | pouso | não | **não** | **não** (o agachar do pouso fica no pose) |
| `climbing_to_top_ladder` | chegada ao topo | não | **não** | sim |

Sempre: *Based Upon = Body Orientation / Feet*, *Position XZ* **fora** do pose. Reconhecimento pelo nome (maiúsculas, `_` e espaços não importam): `climb`/`totop`, `runtostop`, `sneak`/`crouch` (+ `idle`/`left`/`right`), `turn` + `left`/`right`, `landing`, `fall`, `jump`, `run`/`sprint`, `walk`, `idle`. Se o clipe se chama `mixamo.com` (o padrão dos FBX do Mixamo), vale o nome do arquivo — e o clipe passa a se chamar como o arquivo.

Medido nos seus `.anim` (30 fps): `walking` 1,03 s e **1,71 m/s** · `running` 0,70 s e **4,56 m/s** · `sneaking_left` ≈ 1,0 m/s · `sneaking_right` ≈ 1,08 m/s · `sneaking_front` **no lugar** · `idle` 9,9 s · `falling_idle` 0,7 s · `jumping` 0,23 s · `landing` 2,0 s (quadril desce a ~0,49 m) · `run_to_stop` 0,9 s · `left_turn` 1,03 s · `right_turn` 1,0 s.

## 4. A layer Base montada (menu `Animation > Mixamo: Humanoid + Locomotion Controller`)

Estado sem clipe é **omitido** junto com as transições dele (um estado vazio mostraria o corpo em T).

| Estado | O que toca | Entra quando |
|---|---|---|
| **`Idle`** (padrão) | `idle` | parado em pé. Fica **fora** da árvore de andar: o clipe é longo (9,9 s) e o blend tree sincroniza a duração dos filhos, o que deixaria o andar em câmera lenta ao acelerar |
| **`Locomotion`** | blend 1D por `Speed`: `walking` em ~1,7 m/s, `running` em ~4,5 m/s (as **velocidades naturais** dos clipes, lidas do próprio clipe). Velocidade de reprodução = `Dir` | `Speed` > 0,15 |
| **`Crouch`** | blend 2D (`MoveX`,`MoveY`): pose parada em (0,0), `sneaking_front` (0;1,5), `left` (−1,5;0), `right` (1,5;0). Os clipes tocam até 30% mais rápido para acompanhar 1,5 m/s. `Dir` | `Crouch` > 0,5 |
| **`Drag`** | a mesma árvore do `Crouch` | `Dragging` |
| **`TurnL` / `TurnR`** | `left_turn` / `right_turn` | parado (`Speed` < 0,1) e `TurnRate` < −120 / > 120 °/s |
| **`RunToStop`** | `run_to_stop` | gatilho `StopRun` (parou depois de correr). Volta a andar depois de 30% do clipe; senão termina em `Idle` aos 85% |
| **`JumpStart`** → **`Air`** | `jumping` (sai aos 70%) → `falling_idle` | gatilho `Jump`; `Grounded` falso |
| **`Land`** | `landing` (sai a ~0,7 s; antes se você já está correndo) | gatilho `Land` (queda > ~1 m) + `Grounded` |
| **`Climb`** | `climbing_ladder` com velocidade = `ClimbRate` (0 = parado no degrau; negativo = descendo, o clipe ao contrário) | `Climbing` (de qualquer estado) |
| `Climb_Top` | `climbing_to_top_ladder` | **ainda sem transição** (falta o `PlayerClimber` avisar "chegou ao topo") |
| `Dead` | **vazio** | `Dead`. Ponha o clipe de morte quando tiver (sem clipe o corpo fica em T) |

**Agachado parado:** não há clipe de agachado parado, então o ponto (0,0) do `Crouch` é uma **pose congelada** tirada do ciclo de `sneaking_front` (`Generated/Animation/Clips/Crouch_IdlePose.anim`), em vez do corpo "marchando" parado. O slider *Pose de agachado parado* escolhe o instante do ciclo (0 = passo aberto, com os dois pés no chão); mude e crie o controller de novo se a pose ficar estranha. Quando tiver um clipe de agachado parado de verdade, nomeie-o `crouch_idle` (ou ponha-o no ponto (0,0) da árvore).

**IK Pass:** a layer Base sai com `IK Pass` ligado (as demais já tinham).

## 5. Como poucos clipes viram todas as direções (`PlayerBodyRig`)

Só existem clipes para **frente** em pé. O rig resolve o resto em código (sem clipes laterais ou de costas):

- **De costas:** o clipe de frente toca **ao contrário**. O rig espelha a velocidade (`MoveX/MoveY` ficam como se o jogador andasse para frente) e põe `Dir` = −1, o multiplicador de velocidade dos estados `Locomotion`/`Crouch`/`Drag`. Com histerese (entra de costas com a parte frontal da velocidade < −0,35 e sai > −0,15) para não ficar trocando.
- **De lado:** as **pernas viram** para a direção do movimento (até `maxLegTurn` = 65°, a `legTurnSpeed` = 420 °/s) e o **tronco gira de volta** pelo IK de olhar (peso do corpo sobe a `warpBodyWeight` = 0,9, trava cai a 0,45), então os ombros e a cabeça continuam virados para onde a câmera olha. Agachado **não** vira as pernas (`warpWhenCrouched` desligado), porque o `Crouch` tem clipes para os lados.
- **Parada brusca:** depois de ≥ 0,3 s acima de `stopRunFrom` (3,8 m/s) e velocidade abaixo de `stopRunTo` (1,2 m/s), dispara `StopRun` (o rig cancela o gatilho depois de 0,4 s para ele não ficar esperando).
- **Pouso:** só quedas com magnitude ≥ `landMinMagnitude` (0,5, ~1 m) disparam `Land`; pulos baixos voltam direto ao chão (o clipe `landing` dura 2 s).
- **Escada:** `ClimbRate` = velocidade vertical / `climbRefSpeed` (1,2 m/s), de −1,5 a 1,5.

O corpo gira com a câmera (`bodyYawOffset` = giro das pernas, somado ao yaw da câmera no `PlayerBodyPresenter`); o recuo ao olhar para baixo continua seguindo o olhar.

## 6. Quem vê o quê: dono × outros

Já era assim e **não mudou**: o `BodyVisibility` esconde só a região **Cabeça** na câmera do dono (a sombra continua) e mostra tudo para as outras câmeras. O que faltava era a cabeça ser um **renderer separado**: o Y Bot tem dois meshes de corpo inteiro (`Alpha_Joints`, `Alpha_Surface`).

O **`BodyMeshSplitter`** (menu `Animation > Split Head From Selected Player Body`, e automático no menu `Animation > Setup Selected Model As Player Body` quando nenhuma parte é a cabeça) divide cada `SkinnedMeshRenderer` pelos **pesos do skin**: o triângulo cujos vértices, na média, têm ≥ 50% do peso no osso da cabeça (e ossos abaixo dele) vai para um mesh novo `..._Head` (`BodyPart` = Head); o resto fica no renderer original (`BodyPart` = Torso). Os dois usam os mesmos ossos, bindposes e materiais — animam igual e o corte não aparece. Os meshes novos ficam em `Generated/Animation/Meshes`; o FBX só ganha *Read/Write* (necessário para ler vértices e pesos). Rodar de novo não faz nada (renderers que já têm `BodyPart` são pulados).

## 7. IK: onde ligar (pés, escada, outros)

`IBodyIK` (`BodyIK.cs`) é o ponto de extensão: um MonoBehaviour no corpo (ou em qualquer filho do jogador) que implementa `OnBodyIK(rig, animator, state, layerIndex)`. O `PlayerBodyRig` o chama de dentro do `OnAnimatorIK`, depois do olhar e das mãos, **uma vez por layer com IK Pass** (trabalhe com `layerIndex == 0` ou seja idempotente). Em tempo de execução: `rig.AddIK(modulo)`. O rig expõe `LegYaw` (giro atual das pernas).

Próximos módulos (não incluídos neste pacote):
- **IK de pés:** raio para baixo de cada pé (`SetIKPosition/Rotation` em `LeftFoot/RightFoot`) e abaixar o quadril pelo desnível; peso zero no ar, na escada e com pés em movimento rápido.
- **Escada:** mãos e pés nos degraus (`SetIKPosition` nos 4 membros) com alvos tirados da escada (`PlayerClimber.Ladder`); o peso sobe ao agarrar e desce ao soltar.
- **Mira / lanterna:** a mão esquerda já usa alvos presos à câmera; trocar por pontos de empunhadura do modelo do item.

## 8. Equipar um item muda o Animator?

**Sim — e a base já ajuda:** hoje o `HandItem` (genérico / corpo a corpo / pistola / longa) liga a layer `Upper Body` com a pose de segurar certa. Para um item que se usa de um jeito **específico**, o caminho é um **`AnimatorOverrideController` por item**: o controller base fica igual e o override troca só os **clipes** (por nome de clipe: `Hold_Pistol`, `Attack`...), sem duplicar o grafo. O item guardaria a referência (campo no `ItemEquip`) e o `PlayerAnimItemAdapter` aplicaria ao equipar e devolveria o controller base ao guardar. Itens pesados também podem trocar o conjunto de locomoção (andar mais lento, sem correr). Fica para o próximo pacote, junto com o IK dos pés e da escada.

## 9. Ajustes finos (o que esperar ao testar)

| Sintoma | O que mexer |
|---|---|
| Andar parece um trote | O `FirstPersonController` anda a 3,2 m/s e o clipe `walking` é 1,7 m/s: a árvore mistura andar + correr. Baixe `walkSpeed` para ~2 m/s se quiser um andar puro |
| Cabeça/câmera dentro do peito ao agachar | O agachar do Mixamo deixa a cabeça a ~1,4 m. Aumente `crouchHeight` do `FirstPersonController` para ~1,35–1,45 m |
| Agachar acelerado | Baixe `crouchSpeed` para ~1,1–1,2, ou mexa no *Time Scale* dos filhos da árvore `Crouch` |
| Pose de agachado parado estranha | Slider da janela (passo 4) ou ponha um clipe de agachado parado |
| Gira os pés a toda hora ao mexer o mouse | Em `Idle` → `TurnL/TurnR` suba o limite de `TurnRate` (120 → 200) |
| Pouso demorado | A saída do `Land` está em ~0,7 s; mude o *Exit Time* da transição `Land → Idle` |
| Pés escorregando ao correr (5,4 vs 4,56 m/s) | Baixe a `sprintSpeed` ou acelere o clipe `running` (*Speed* no estado `Locomotion` não: use *Time Scale* do filho) |
| Corpo "torcido" ao andar de lado | O limite é 65°. Ajuste `maxLegTurn`/`warpBodyWeight` no `PlayerBodyRig` |
| Fase do andar/correr fora de sincronia (pé cruzando na mistura) | Ajuste o *Cycle Offset* do filho `running` na árvore `Locomotion` |
| Escada com o corpo deslocado | O clipe tem posição própria; ajuste *Position XZ/Y Offset* do clipe no Import Settings. O IK de mãos/pés na escada vem no próximo pacote |

## 10. Limitações deste pacote

- **Nada foi compilado nem testado no Unity.**
- Sem IK de pés nem de escada (só o gancho `IBodyIK`), sem clipe de morte, sem `Climb_Top` ligado, sem troca de Animator por item.
- Sem clipes para os lados/de costas **em pé**: usa pernas viradas e clipe ao contrário (a aparência é boa a 45°, aceitável a 65°).
- O agachar parado é uma pose congelada.
- O corte da cabeça é pelo peso do skin (limite de 50% em `HeadWeightThreshold`, no `BodyMeshSplitter`): se pegar o pescoço de forma estranha, ajuste o limite ou separe a cabeça à mão e marque `BodyPart` = Head. Os dois meshes compartilham os mesmos vértices na borda, então não aparece fresta.
