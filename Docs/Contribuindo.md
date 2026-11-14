# Contribuindo

Regras curtas para quem vai mexer no código ou nos assets. A maioria vem do guia de referência
([`HorrorFPS-Contexto-Scripts.md`](HorrorFPS-Contexto-Scripts.md), seção 0 e 3); aqui só o essencial.

## Antes de começar

1. **Procure onde mexer.** A tabela "Pedido → onde mexer" do guia de referência diz qual pasta e qual doc cobre cada sistema.
2. **Mude o mínimo** e siga o estilo do arquivo ao redor. Não reformate arquivo inteiro: isso esconde a mudança real no diff.
3. **Nada aqui é compilado fora da Unity.** Quem altera precisa abrir a Unity, esperar compilar e jogar o que mexeu. Se algo só foi conferido por leitura, diga isso no commit ou no PR.

## Código

- Um `MonoBehaviour` / `ScriptableObject` por arquivo, com o **nome do arquivo igual ao da classe**; `namespace HorrorFPS`; 4 espaços; chaves na linha de baixo.
- Comentários, `Tooltip` e textos de interface em **português**. Textos de interface vão para `UIText` (constantes), não espalhados em strings.
- **Não há `.asmdef`** (de propósito). Isso significa que tudo que não está em pasta `Editor` compila junto no jogo.
- **Entrada:** só `PlayerInputHandler` / `ItemInput` leem `InputAction`. Nunca use `Keyboard.current` ou `Gamepad.current` solto em código de gameplay.
- **Tudo é por jogador** (1 a 4 em tela dividida): use o `PlayerContext` recebido, nunca assuma "o jogador".
- **Travar o jogador:** `AddControlLock(chave)` / `RemoveControlLock(chave)` com uma chave própria; sempre destrave ao terminar e em `OnDisable`.
- **Estado que precisa sobreviver** ao salvar ou trocar de nível entra no sistema de save (`ISaveable` / `SaveableEntity`), não em campo estático.
- **Campo estático novo** precisa ser zerado em um `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatics()` (veja como os outros arquivos fazem).

## Código de Editor

- **Tudo que usa `UnityEditor` fica em uma pasta `Editor`** (`Editor/` na raiz do framework ou `SmartEvents/Editor/`). Código de Editor fora dessas pastas **quebra o build do jogo**. Se o arquivo precisa ficar junto do runtime, proteja o trecho com `#if UNITY_EDITOR`.
- Código de Editor só enxerga membros **públicos** do runtime (a pasta `Editor` compila em outro assembly). Se um menu precisar de algo, torne-o `public`, não `internal`.

## Menus do Editor

Os menus são decididos em um lugar só: [`Editor/HorrorFPSMenu.cs`](../Editor/HorrorFPSMenu.cs). Para um menu novo:

```csharp
[MenuItem(HorrorFPSMenu.Stalker + "Meu Menu Novo", false, HorrorFPSMenu.StalkerOrder + 10)]
static void MeuMenuNovo() { ... }
```

- Escolha a área que combina (`Player`, `Items`, `Dialogue`, `Animation`, `Stalker`, `Hurtbox`, `Revive`, `Demo`). Área nova: acrescente o caminho e a ordem em `HorrorFPSMenu`.
- **Sem número no nome do menu.** A ordem é o 3º argumento (`Order + n`).
- Mensagens e comentários que citam um menu devem usar o caminho completo (`Tools > Horror FPS > Área > Nome`), não "menu 15".
- Atualize [`Menus.md`](Menus.md).

## Arquivos, `.meta` e assets

- **Arquivo novo = `.meta` novo, e os dois vão no mesmo commit.** Cenas e prefabs apontam para scripts por GUID.
- **Mover ou renomear** pelo Project da Unity (ou com `git mv` movendo o `.meta` junto). Nunca mova um `.cs` sem o `.meta`.
- Pasta nova também tem `.meta`; pasta que ficou vazia perde o `.meta` (um `.meta` sem arquivo/pasta correspondente é lixo e a Unity avisa).
- Nomes de pasta sem acento e sem espaço (a Unity aceita, mas ferramentas de linha de comando e CI sofrem).
- Cenas, prefabs e materiais são YAML: conflito de merge neles é difícil de resolver. Avise a equipe antes de mexer na mesma cena.

## Documentação

- Um doc por sistema em `Docs/`, com o nome `HorrorFPS-<Assunto>.md`. Mudou o comportamento? Atualize o doc junto com o código.
- O guia de referência ([`HorrorFPS-Contexto-Scripts.md`](HorrorFPS-Contexto-Scripts.md)) é um mapa, não a fonte da verdade: em caso de conflito, **o código vence**.
