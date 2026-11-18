# Task Bar: Dungeon Abyss — Demo v0.1 (Unity 6.3 LTS)

Jogo híbrido **Idle RPG de Barra de Tarefas + Tamagotchi + Treino estilo *Punch Club* + Estética Dual-Screen de Nintendo DS**, estruturado em arquitetura modular escalável pronta para crescer até o jogo completo.

---

## Como Abrir e Jogar na Unity (Zero Configuração Manual)

1. Abra o **Unity Hub** (com a versão **`Unity 6.3 LTS (6000.3.28f1)`** instalada).
2. Em **Projetos**, clique em **`Adicionar` → `Adicionar projeto do disco`** e selecione a pasta `TaskBarDungeonAbyss`.
3. Na primeira vez que o Editor abrir, o script automático `ProjectSetupWizard` vai rodar sozinho e:
   - Configurar **Direct3D 11 + BitBlt Swapchain** (`useFlipModelSwapchain = false`, essencial para transparência real no Windows sem fundo preto).
   - Importar todos os sprites Pixel Art (`16×16`, `32×32` e `256×192`) em `Point Filter` sem compressão.
   - Gerar os `ScriptableObjects` em `Assets/_Project/Resources/Database/` e a cena `Assets/_Project/Scenes/01_TaskBarDungeonDemo.unity`.
4. Aperte **PLAY (▶)** no topo da Unity para jogar imediatamente!
   - *(Caso queira regerar os assets ou apagar o save de teste para rever a Intro, use o menu superior **`TaskBar Dungeon Abyss`** na barra da Unity).*

### Testando na Barra de Tarefas Real do seu Windows (`.exe`)
No Editor da Unity a transparência é simulada para não recortar o próprio Editor. Para ver a **Dungeon flutuando transparente na sua Barra de Tarefas real**:
1. Vá em **`File` → `Build Profiles` (ou `Build Settings`)**.
2. Clique em **`Build And Run`** e escolha uma pasta (ex: `Build/`).
3. Na Intro, o jogo abre como um **Nintendo DS de duas telas**; assim que você clica no anexo do e-mail misterioso (`<NomeDoPet>_Taskbar.exe`), a janela se transforma em tempo real na faixa transparente ancorada acima da sua barra de tarefas!

---

## Arquitetura Escalável & Preparo para Nintendo DS

Baseada nas melhores práticas de projetos open-source em Unity para janelas transparentes (*UniWindowController* / *UnityDesktopPetFramework*) e mapas procedurais de nós (*Slay the Spire Map in Unity*):

- **`TBDA.Domain` (`noEngineReferences: true` — 100% C# Puro):**
  - Sem qualquer dependência de `UnityEngine`. Toda a matemática de Tamagotchi, treino *Punch Club*, mutação do Mini-Mapa e Save é determinística e pronta tanto para testes automatizados quanto para uma futura tradução experimental para C/C++ (`BlocksDS` / `libnds` no Nintendo DS).
- **`TBDA.Data` (`ScriptableObjects`):**
  - Espécies (`PetSpeciesSO` já com lista `FutureEvolutions` para evoluções ramificadas estilo *Digimon* e o Pet Construtor de Labirintos), Comidas (`CareItemSO`), Estações de Treino (`TrainingDrillSO`) e `GameDatabaseSO`.
- **`TBDA.Platform` (`Win32TaskbarOverlayController`):**
  - Captura segura de `HWND` via `EnumThreadWindows` (`UnityWndClass`), suporte 64-bit (`SetWindowLongPtr`), transparência DWM real e *Click-Through* leve por Raycast com cache de estado.
- **`TBDA.Presentation`:**
  - `StardewDSIntroView`: Prólogo estilo *Stardew Valley* em tela dupla `256×192`.
  - `TaskbarDungeonStripView`: Faixa da masmorra interativa + painel superior retrátil estilo Nintendo DS (abas **Tamagotchi**, **Punch Club** e **Mini-Mapa Mutável**).
  - `DSChiptuneSynthesizer`: Efeitos sonoros retrô gerados proceduralmente em memória.
- **`TBDA.Editor`:**
  - `PixelArtImportPostprocessor` & `ProjectSetupWizard`.
