# Menus do Editor (`Tools > Horror FPS`)

Os menus ficam agrupados por área, **sem números**. O caminho de cada área e a ordem dos itens são definidos em um só lugar: `Editor/HorrorFPSMenu.cs`. Para criar um menu novo, veja [`Contribuindo.md`](Contribuindo.md).

## Menus atuais

### Player — prefab do jogador, módulos, inventário inicial e personagens

| Menu | Arquivo |
|---|---|
| `Tools > Horror FPS > Player > Create Player Prefab` | `Editor/HorrorFPSSetup.cs` |
| `Tools > Horror FPS > Player > Add Item, Sanity and Body Modules To Player Prefab` | `Editor/PlayerPrefabInstaller.cs` |
| `Tools > Horror FPS > Player > Starting Inventory` | `Editor/PlayerInventoryWindow.cs` |
| `Tools > Horror FPS > Player > Character Catalog` | `Editor/CharacterCatalogUtil.cs` |
| `Tools > Horror FPS > Player > Add Selected Player Prefab(s) As Characters` | `Editor/CharacterCatalogUtil.cs` |

### Items — itens

| Menu | Arquivo |
|---|---|
| `Tools > Horror FPS > Items > Item Studio` | `Editor/ItemStudioWindow.cs` |

### Dialogue — diálogos

| Menu | Arquivo |
|---|---|
| `Tools > Horror FPS > Dialogue > Dialogue Editor` | `Editor/Dialogue/DialogueGraphWindow.cs` |

### Animation — corpo animado, Animator Controller e Mixamo

| Menu | Arquivo |
|---|---|
| `Tools > Horror FPS > Animation > Add Animation Rig To Player Prefab (placeholder body)` | `Editor/Animation/PlaceholderBodyBuilder.cs` |
| `Tools > Horror FPS > Animation > Create Player Animator Controller` | `Editor/Animation/PlayerAnimatorControllerBuilder.cs` |
| `Tools > Horror FPS > Animation > Setup Selected Model As Player Body` | `Editor/Animation/ModelBodySetup.cs` |
| `Tools > Horror FPS > Animation > Mixamo: Humanoid + Locomotion Controller` | `Editor/Animation/MixamoSetupWindow.cs` |
| `Tools > Horror FPS > Animation > Split Head From Selected Player Body` | `Editor/Animation/BodyMeshSplitter.cs` |

### Stalker — perseguidor, árvore de comportamento e rotas

| Menu | Arquivo |
|---|---|
| `Tools > Horror FPS > Stalker > Add Stalker To Selected Object` | `Editor/Stalker/StalkerMenu.cs` |
| `Tools > Horror FPS > Stalker > Create Stalker Definition From Selected` | `Editor/Stalker/StalkerMenu.cs` |
| `Tools > Horror FPS > Stalker > Stalker Tree Editor` | `Editor/Stalker/StalkerTreeMenu.cs` |
| `Tools > Horror FPS > Stalker > Create Stalker Tree (Default)` | `Editor/Stalker/StalkerTreeMenu.cs` |
| `Tools > Horror FPS > Stalker > Create Stalker Tree (Example: Roar On Spot)` | `Editor/Stalker/StalkerTreeMenu.cs` |
| `Tools > Horror FPS > Stalker > Add Route Runner To Selected Stalker` | `Editor/Stalker/StalkerWorldMenu.cs` |
| `Tools > Horror FPS > Stalker > Create Stalker Route From Selected Ladder` | `Editor/Stalker/StalkerWorldMenu.cs` |
| `Tools > Horror FPS > Stalker > Create Stalker Route (Duct, Vault, Drop...) At Scene View` | `Editor/Stalker/StalkerWorldMenu.cs` |
| `Tools > Horror FPS > Stalker > Create Stalker Ambush Spot From Selected` | `Editor/Stalker/StalkerWorldMenu.cs` |
| `Tools > Horror FPS > Stalker > Create Stalker Test Scene` | `Editor/HorrorFPSSetup.StalkerTest.cs` |
| `Tools > Horror FPS > Stalker > Create Stalker Test Arena In Open Scene` | `Editor/HorrorFPSSetup.StalkerTest.cs` |
| `Tools > Horror FPS > Stalker > Add Route Animator Parameters` | `Editor/Stalker/StalkerWorldMenu.cs` |

### Hurtbox — zonas de dano

| Menu | Arquivo |
|---|---|
| `Tools > Horror FPS > Hurtbox > Add Humanoid Hurtboxes To Selected` | `Editor/Hurtbox/HurtboxMenu.cs` |
| `Tools > Horror FPS > Hurtbox > Add Simple Hurtbox Zones (Head/Torso/Legs) To Selected` | `Editor/Hurtbox/HurtboxMenu.cs` |
| `Tools > Horror FPS > Hurtbox > Remove Hurtboxes From Selected` | `Editor/Hurtbox/HurtboxMenu.cs` |
| `Tools > Horror FPS > Hurtbox > Create Hurtbox Test Dummy` | `Editor/Hurtbox/HurtboxMenu.cs` |
| `Tools > Horror FPS > Hurtbox > Create 'Hurtbox' Physics Layer` | `Editor/Hurtbox/HurtboxMenu.cs` |

### Revive — reanimação em co-op

| Menu | Arquivo |
|---|---|
| `Tools > Horror FPS > Revive > Create Revive Profile` | `Editor/Revive/ReviveMenu.cs` |
| `Tools > Horror FPS > Revive > Toggle Revive Debug Keys` | `Editor/Revive/ReviveMenu.cs` |
| `Tools > Horror FPS > Revive > Revive Diagnostics (Play Mode)` | `Editor/Revive/ReviveMenu.cs` |

### Objectives — objetivos da partida

| Menu | Arquivo |
|---|---|
| `Tools > Horror FPS > Objectives > Create Objective Catalog` | `Editor/Objectives/ObjectivesMenu.cs` |
| `Tools > Horror FPS > Objectives > Add Objective Actions To Selected` | `Editor/Objectives/ObjectivesMenu.cs` |

### Tutorials — dicas de tutorial

| Menu | Arquivo |
|---|---|
| `Tools > Horror FPS > Tutorials > Create Tutorial Catalog` | `Editor/Tutorials/TutorialsMenu.cs` |
| `Tools > Horror FPS > Tutorials > Create Tutorial Trigger Volume` | `Editor/Tutorials/TutorialsMenu.cs` |
| `Tools > Horror FPS > Tutorials > Reset Tips Seen` | `Editor/Tutorials/TutorialsMenu.cs` |

Como usar os dois sistemas: [`HorrorFPS-Objetivos-Tutorial.md`](HorrorFPS-Objetivos-Tutorial.md).

### Demo — jogo de demonstração e exemplos prontos

| Menu | Arquivo |
|---|---|
| `Tools > Horror FPS > Demo > Create Full Game Demo (menu, níveis, save, cutscenes)` | `Editor/HorrorFPSSetup.FullDemo.cs` |
| `Tools > Horror FPS > Demo > Create Sample Items` | `Editor/SampleItems.cs` |
| `Tools > Horror FPS > Demo > Create Sample Dialogues` | `Editor/Dialogue/DialogueMenus.cs` |
| `Tools > Horror FPS > Demo > Create Sample Objectives` | `Editor/Objectives/ObjectivesMenu.cs` |

Também existem `Assets > Create > HorrorFPS > Stalker Node Script (Action)` e `(Condition)` (`Editor/Stalker/StalkerTreeMenu.cs`) e os `Assets > Create > HorrorFPS > ...` dos ScriptableObjects.

## De onde veio cada menu (número antigo → caminho novo)

Os docs e o histórico do Git antigos citam os menus pelo número. A tabela traduz:

| Antigo | Novo |
|---|---|
| 1. Create Player Prefab | `Player > Create Player Prefab` |
| 3. Create Full Game Demo (menu, níveis, save, cutscenes) | `Demo > Create Full Game Demo (menu, níveis, save, cutscenes)` |
| 4. Add Item, Sanity and Body modules to Player Prefab | `Player > Add Item, Sanity and Body Modules To Player Prefab` |
| 5. Create Sample Items | `Demo > Create Sample Items` |
| 7. Player Starting Inventory | `Player > Starting Inventory` |
| 8. Character Catalog | `Player > Character Catalog` |
| 9. Add Selected Player Prefab(s) as Characters | `Player > Add Selected Player Prefab(s) As Characters` |
| 10. Item Studio | `Items > Item Studio` |
| 11. Dialogue Editor | `Dialogue > Dialogue Editor` |
| 12. Create Sample Dialogues | `Demo > Create Sample Dialogues` |
| 14. Add Animation Rig To Player Prefab (placeholder body) | `Animation > Add Animation Rig To Player Prefab (placeholder body)` |
| 15. Create Player Animator Controller | `Animation > Create Player Animator Controller` |
| 16. Setup Selected Model As Player Body | `Animation > Setup Selected Model As Player Body` |
| 19. Add Stalker To Selected Object | `Stalker > Add Stalker To Selected Object` |
| 20. Create Stalker Definition From Selected | `Stalker > Create Stalker Definition From Selected` |
| 21. Create Revive Profile | `Revive > Create Revive Profile` |
| 22. Toggle Revive Debug Keys | `Revive > Toggle Revive Debug Keys` |
| 23. Revive Diagnostics (Play Mode) | `Revive > Revive Diagnostics (Play Mode)` |
| 24. Stalker Tree Editor | `Stalker > Stalker Tree Editor` |
| 25. Create Stalker Tree (Default) | `Stalker > Create Stalker Tree (Default)` |
| 26. Create Stalker Tree (Example: Roar On Spot) | `Stalker > Create Stalker Tree (Example: Roar On Spot)` |
| 31. Mixamo: Humanoid + Locomotion Controller | `Animation > Mixamo: Humanoid + Locomotion Controller` |
| 32. Split Head From Selected Player Body | `Animation > Split Head From Selected Player Body` |
| 36. Add Route Runner To Selected Stalker | `Stalker > Add Route Runner To Selected Stalker` |
| 37. Create Stalker Route From Selected Ladder | `Stalker > Create Stalker Route From Selected Ladder` |
| 38. Create Stalker Route (Duct, Vault, Drop...) At Scene View | `Stalker > Create Stalker Route (Duct, Vault, Drop...) At Scene View` |
| 39. Create Stalker Ambush Spot From Selected | `Stalker > Create Stalker Ambush Spot From Selected` |
| 40. Add Route Animator Parameters | `Stalker > Add Route Animator Parameters` |
| 41. Add Humanoid Hurtboxes To Selected | `Hurtbox > Add Humanoid Hurtboxes To Selected` |
| 42. Add Simple Hurtbox Zones (Head/Torso/Legs) To Selected | `Hurtbox > Add Simple Hurtbox Zones (Head/Torso/Legs) To Selected` |
| 43. Remove Hurtboxes From Selected | `Hurtbox > Remove Hurtboxes From Selected` |
| 44. Create Hurtbox Test Dummy | `Hurtbox > Create Hurtbox Test Dummy` |
| 45. Create 'Hurtbox' Physics Layer | `Hurtbox > Create 'Hurtbox' Physics Layer` |

### Menus que não existem mais

| Antigo | O que aconteceu |
|---|---|
| 27. Create Showcase Scene (Everything) | **Removido** junto com a cena `Level_Showcase`, os materiais `Showcase*` e o código gerador (`HorrorFPSSetup.Showcase*`, `DemoProps`, `PropsDemoBuilder*`). Está no histórico do Git (commit anterior a "Remove a cena Showcase"). |
| 2. Create Demo Scene · 6. Create Demo Props In Open Scene · 13. Create Dialogue Demo Props · 17. Create Props Demo · 18. Create Stalker Demo | Já não estavam no repositório (repetiam o que a cena completa montava). A bancada do perseguidor (18) voltou como `Stalker > Create Stalker Test Scene` e `Stalker > Create Stalker Test Arena In Open Scene`. |
| 28 a 30, 33 a 35 | Números que nunca chegaram a este repositório. Docs antigos de IK das mãos (maçaneta, válvula) citam menus 34/36/37 que não existem aqui; no repositório, 36 e 37 eram as rotas do perseguidor. |
