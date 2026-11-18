# HorrorFPS — Lanterna na mão, feixe que denuncia, mira só com duas mãos, modo deus e largar o item equipado

Status: **escrito e conferido só por parser de sintaxe C#, NÃO compilado nem testado no Unity.** Se aparecer erro de compilação ou algo estranho em Play, mande o texto do Console.

Complementa `HorrorFPS-Maos-Equipamento` (Fase 4), `HorrorFPS-Visibilidade` e `HorrorFPS-Perseguidor`.

## 1. Lanterna como item de mão

- Equipa como qualquer item de mão: atalho + clique esquerdo (mão esquerda) ou direito (mão direita), ou o menu do item no inventário.
- **Liga e desliga com o botão da mão em que está.** A tecla F / D-pad cima e a ação `ToggleLight` não existem mais.
- Uma mão só (G não vale para ela). Ao tirar da mão para a mochila, apaga (largada no chão, continua acesa: seção 1b). Guardada por um item de duas mãos na outra mão: sem luz e sem gastar bateria.
- Bloqueia o arrastar objetos enquanto equipada (o botão é dela).
- Bateria, pilhas, falha da luz e "Sem pilhas." continuam como antes. A HUD mostra `Dir  Lanterna  ligada  82%` na linha da mão.
- Itens antigos com `equipSlot = "lanterna"` continuam funcionando; saves antigos movem a lanterna para uma mão livre.

**Passos no Unity (uma vez):**
1. Nada para configurar em cena: o `EquipmentController` e o `GodMode` são instalados sozinhos. Se você montou o prefab do jogador à mão, rode *Tools > Horror FPS > Player > Add Item, Sanity and Body Modules To Player Prefab* de novo.
2. Itens de lanterna que você criou antes continuam valendo. Para novos, use o botão **Lanterna** do `ItemEquip` (agora com `equipSlot = "mao"`). Se quiser, troque o slot dos antigos para `mao` (opcional).
3. Os geradores de itens de exemplo e de cena de teste do projeto já criam a lanterna no formato novo; o que já foi gerado continua valendo.

## 1b. Largar o item equipado (e o estado dele vai junto)

**Tecla: Q** (teclado) ou **D-pad cima** (controle), com o inventário fechado:

| Situação | O que acontece |
|---|---|
| item nas duas mãos (G) | Q larga esse item |
| uma mão cada | **toque** em Q larga o da mão direita (o da esquerda, se a direita está vazia); **segurar** Q por ~0,3 s larga o da mão esquerda |

O item cai na frente do jogador (o mesmo cálculo do descarte pelo inventário: não nasce dentro de parede), faz um barulho curto (raio 6 m) e a HUD avisa "Largou: ...". Item com `canDrop` desligado recusa ("Não dá para largar isto."). Pelo inventário (G com a tela aberta, ou o menu do item) o comportamento é o mesmo.

**O estado do item não se perde.** O item do chão é a **mesma instância** do item (`ItemInstance`), então tudo o que mora nela continua: flags (lanterna **ligada**) e contadores (munição no carregador, bateria, durabilidade). Quem pegar de novo recebe o item como estava; também vai para o save (`DropRegistry` grava flags e contadores). Vale para qualquer item com estado, não só equipamento.

**Lanterna largada acesa continua acesa.** O `WorldItem` cria um `WorldItemLight` para todo item do tipo Lanterna: se a flag `ligada` está na instância, a luz continua no chão (spot com os mesmos valores do item), **gastando a bateria**, com a mesma falha no fim, e apaga sozinha quando a bateria acaba. Voltar ao nível pelo save recria a luz acesa. O feixe sai pelo eixo que a lanterna aponta na mão (`ItemEquip.heldEuler`): se o modelo do mundo (`worldPrefab`) estiver virado diferente do modelo da mão, o feixe sai torto; ajuste o `heldEuler` ou a rotação do `worldPrefab`.

**Pegar a lanterna acesa do chão** a leva para a mochila **apagada** (acesa só na mão; na mochila ela não gasta bateria). Se preferir que continue acesa, remova a linha em `WorldItem.Interact`.

Em código: `EquipmentController.DropFromHand(HandSide)` larga o item de uma mão, ou use `owner.Drop(item)` para qualquer item; `InventoryOwner.DroppingItem` diz, durante o descarte, qual item está saindo (é como o controlador sabe que não deve apagar a lanterna).

## 2. O feixe denuncia o jogador (só alguns monstros)

No **StalkerProfile** do monstro, grupo *Lanterna do jogador (o feixe denuncia)*:

| Campo | Padrão | O que faz |
|---|---|---|
| `Detects Flashlight Beam` | desligado | liga a habilidade neste perfil |
| `Beam Detect Range` | 40 m | até onde ele nota a luz acesa (a luz denuncia de longe) |
| `Beam Detect Strength` | 0,7 | quão rápido a luz o faz notar o jogador |
| `Beam Lit Bonus` | 1,6 | multiplicador quando o feixe aponta para o monstro |

Como funciona: se a lanterna acesa do jogador está dentro do cone de visão do monstro, no alcance e sem parede no caminho, a suspeita enche como se ele visse o jogador (e guarda a posição do jogador como "último ponto visto"). Vale o maior entre essa visão e a visão normal. Jogador escondido continua invisível. Para dar a habilidade só a alguns monstros: duplique o `StalkerProfile` (Ctrl+D), marque o campo no duplicado e use-o na `StalkerDefinition` desses monstros. Os outros só sentem a lanterna pelo fator de visibilidade (`Lamp Factor` 1,3), como antes. A luz falhando (bateria no fim) denuncia menos.

Em código: `EquipmentController.TryGetBeam(player, out EquipmentController.BeamInfo b)` devolve origem, direção, alcance, abertura do cone e intensidade da lanterna acesa.

## 3. Mira só com as duas mãos

- A ação `Aim` (Q / Select) foi removida.
- Arma de fogo nas **duas mãos** (G): mira sozinha (zoom `aimFov`, dispersão `aimSpreadMultiplier`, arma no centro). Em **uma mão**: sem zoom.
- A mira na tela (`EquipmentHUD`) aparece com a arma em uma ou duas mãos; o modo *Só mirando* passa a ser "só com as duas mãos".
- Tutoriais: o texto da dica de arma de fogo já explica; `{Aim}` mostra o botão do G e `{Flashlight}` o botão da mão direita (compatibilidade).
- Se você quiser que a mira exija também ficar parado ou fora da corrida, dá para somar em `EquipmentController.TickAim`.

## 4. Modo deus (só Editor e Development Build)

`Player/GodMode.cs` (um por jogador, instalado sozinho, **nasce tudo desligado**). Cada jogador usa o seu teclado; no controle, Select faz o F1.

| Tecla | Efeito |
|---|---|
| **F1** / Select | liga ou desliga o pacote todo (se algo está ligado, desliga tudo) |
| **F2** | invencível: não leva dano e `Kill()` não mata (usa `StatsComponent.Immortal`); cura ao ligar |
| **F3** | invisível: os monstros não veem, não ouvem os passos e não atacam (usa `PlayerCheats` + `StalkerBridge.IsHidden`) |
| **F4** | estamina infinita |
| **F5** | recursos infinitos: munição sem recarregar, bateria da lanterna, durabilidade das armas brancas |

Um aviso no canto da tela do jogador mostra o que está ligado. No Inspector do `GodMode` há `Start Enabled` (liga tudo ao nascer: útil numa cena de teste) e `Show Label`.

Limites: não é um modo fantasma (sem voar nem atravessar parede); não trava a sanidade; o `StatsComponent.Invulnerable` de cena continua separado de `Immortal`. Reanimação e finalizador não disparam porque o jogador não leva dano nem é visto. F1..F5 não colidem com as teclas de depuração do monstro (F8..F11) e da reanimação (F6..F11).

## 5. Para testar

1. Lanterna: equipe pelo atalho, clique esquerdo (mão esquerda), veja a luz só ligar ao clicar de novo; troque de mão pelo menu do inventário; segure uma arma de duas mãos na outra mão (G) e veja a lanterna sumir e voltar.
2. Mira: pistola numa mão (sem zoom), G (zoom e mira fechada), G de novo (volta).
3. Feixe: na cena `Test_Stalker`, marque `Detects Flashlight Beam` no perfil do monstro, acenda a lanterna a ~30 m dele em campo aberto, de frente para ele (o F8 mostra o cone), e depois de costas / atrás de uma parede (não deve notar).
4. Modo deus: F1 e deixe o monstro chegar perto (não deve reagir nem ferir); F4 e corra; F5 e atire sem recarregar.

## 6. Arquivos

Novos: `Core/PlayerCheats.cs`, `Player/GodMode.cs`, `Inventory/WorldItemLight.cs` (e os `.meta`). Largar item: `Input/ItemInput.cs` (`DropEquipped`), `Equipment/EquipmentController.cs` (`TickDropKey`, `DropFromHand`), `Inventory/InventoryOwner.cs` (`Drop` mantém o estado, `DroppingItem`), `Inventory/WorldItem.cs` (luz da lanterna no chão, apaga ao pegar). Alterados: `Equipment/EquipmentController.cs`, `Equipment/EquipmentHUD.cs`, `Inventory/InventoryOwner.cs`, `Input/ItemInput.cs`, `Items/ItemEquip.cs`, `Body/PlayerVisibility.cs`, `Stalker/StalkerProfile.cs`, `StalkerSenses.cs`, `StalkerBridge.cs`, `Stats/StatsComponent.cs`, `Core/PlayerModules.cs`, `Animation/PlayerAnimItemAdapter.cs`, `Tutorials/TutorialText.cs`, `TutorialDefaults.cs`, `Editor/EquipPresets.cs`, `Editor/SampleItems.cs`.

## 7. Limites conhecidos

- Duas lanternas (uma em cada mão) funcionam, mas a visibilidade e a animação consideram só uma (a acesa, senão a da direita).
- O monstro com a habilidade enxerga a luz mesmo no escuro total; não há ainda "fugir da luz" nem "cego à luz" pronto (dá para fazer num módulo com `ModifyVisibility`).
- Arrastar objetos fica bloqueado com a lanterna equipada.
- A luz da lanterna largada usa sombra suave: muitas lanternas acesas no chão ao mesmo tempo pesam; sombra ligada em todas.
- Só a lanterna tem efeito visível no chão (luz). Outros estados (munição, durabilidade) são preservados na instância, mas não aparecem no objeto do mundo.
