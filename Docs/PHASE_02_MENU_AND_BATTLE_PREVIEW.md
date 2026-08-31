# Phase 02 — Menu e anteprima del combattimento

La Fase 2 aggiunge un flusso persistente e verificabile `Menu → Tutorial Draft → Menu`. Le due scene sono completamente authorate e salvate prima del Play Mode; il runtime modifica soltanto stato, testo, colore, trasformazioni e scena attiva.

## Scene e gerarchie

`Assets/_Veyra/Scenes/SCN_MainMenu.unity` contiene:

```text
SCN_MainMenu
|-- Main Camera
|-- UIRoot
|   `-- Canvas
|       `-- SafeArea
|           |-- BackgroundLayers
|           |-- TitleArea
|           |-- HeroPreview
|           |-- StartCard
|           |-- Footer
|           |-- Dimmer
|           |-- SettingsModal
|           |-- LoadingOverlay
|           `-- ErrorModal
`-- EventSystem
```

`Assets/_Veyra/Scenes/SCN_W01_L01_Tutorial.unity` contiene:

```text
SCN_W01_L01_Tutorial
|-- Main Camera
|-- BattlePreviewRoot
|   |-- Background
|   |-- HeroSlot
|   |   |-- HeroVisual
|   |   |-- HeroProjectileOrigin
|   |   |-- HeroHitTarget
|   |   `-- GuardVisual
|   |-- EnemySlot
|   |   |-- EnemyVisual
|   |   |-- EnemyProjectileOrigin
|   |   |-- EnemyHitTarget
|   |   `-- MarkPreview
|   `-- PreviewEffects
|       |-- HeroBasicProjectile
|       |-- HeroTechniqueProjectile
|       `-- EnemyProjectile
|-- UIRoot
|   `-- Canvas
|       `-- SafeArea
|           |-- EnemyPanel
|           |-- IntentPanel
|           |-- CombatMessage
|           |-- HeroPanel
|           |-- FocusPanel
|           |-- ActionBar
|           `-- BTN_BackToMenu
`-- EventSystem
```

Ogni scena usa un solo `EventSystem` con `InputSystemUIInputModule`, un Canvas Screen Space Overlay e un `CanvasScaler` 1080 × 1920 con Match 0.5. `SafeAreaFitter` adatta la UI all'area sicura del dispositivo.

## Flusso e controlli

- `INIZIA` blocca tocchi successivi, mostra l'overlay persistente e carica asincronamente `SCN_W01_L01_Tutorial`.
- `IMPOSTAZIONI` apre il modal già presente e il dimmer blocca il menu sottostante.
- `CHIUDI` e Android Back/Escape chiudono prima il modal.
- `ATTACCO`, `GUARDIA`, `TECNICA` e `MARCHIO` riproducono quattro feedback distinti usando soltanto istanze prefab già presenti.
- `MENU` e Android Back/Escape nella battle preview ricaricano asincronamente `SCN_MainMenu`.
- Loading ed Error Modal esistono già nella scena; nessuno dei due viene costruito al runtime.

## Impostazioni locali

`LocalSettingsStore` usa `PlayerPrefs` soltanto per le preferenze della Fase 2:

| Preferenza | Chiave |
|---|---|
| Versione schema | `Veyra.Settings.Version` |
| Volume generale | `Veyra.Settings.MasterVolume` |
| Volume musica | `Veyra.Settings.MusicVolume` |
| Volume effetti | `Veyra.Settings.SfxVolume` |
| Vibrazione | `Veyra.Settings.VibrationEnabled` |

Il volume generale viene applicato a `AudioListener.volume`. Musica e SFX sono soltanto memorizzati per il futuro sistema audio: la Fase 2 non include clip o sorgenti e non dichiara che siano già udibili. I controlli sono caricati con `SetValueWithoutNotify`; chiusura e pausa dell'app salvano i valori.

## Direzione visuale provvisoria

Il concept `La linfa risale` usa un albero corrotto ai bordi, un Hero01 piccolo nel terzo inferiore e una vena di luce che risale verso il titolo. L'area centrale rimane pulita.

| Ruolo | Colore |
|---|---|
| Sfondo | `#0B1715` |
| Pannello | `#142622` |
| Pannello evidenziato | `#1D3731` |
| Luce/purificazione | `#59D7D0` |
| Luce chiara | `#B9FFF0` |
| Corruzione | `#8F4AC7` |
| Testo principale | `#F3F7F4` |
| Testo secondario | `#A9BBB5` |
| Errore/danno | `#E85C65` |
| Accento oro limitato | `#D5AE62` |

Gli sprite PNG `Prototype` di Hero01, Enemy01, proiettili, anello Guardia e impulso Marchio sono importati come Sprite, Point, Uncompressed, senza mip map, pivot centrato e 32 PPU. I relativi prefab sono sotto `Prefabs/UI/Battle` e `Prefabs/VFX/Combat`; le istanze degli effetti partono inattive nella scena tutorial. Anche il font TMP dinamico è un asset persistente `Prototype` generato dal font integrato di Unity.

## Tool Editor

La generazione è manuale e idempotente:

1. Aprire il progetto con Unity `6000.5.5f1`.
2. Eseguire **Tools > Veyra > Phase 02 > Create Main Menu And Battle Preview**.
3. Leggere il riepilogo `[Veyra Phase 02]` nella Console. Asset, prefab e scene esistenti vengono preservati e mai sovrascritti automaticamente.
4. Eseguire **Tools > Veyra > Phase 02 > Validate Phase 02** per i controlli Edit Mode.
5. Eseguire **Tools > Veyra > Phase 02 > Validate Phase 02 With Play Mode** per il flusso automatizzato completo.

Se Unity non espone il font integrato necessario, la generazione UI si arresta senza riferimenti rotti. In quel caso eseguire **Tools > Veyra > Phase 02 > Import TMP Essential Resources**, attendere l'importazione ufficiale e rilanciare il comando di creazione.

## Controlli manuali

1. Aprire entrambe le scene e verificare che menu, HUD, personaggi ed effetti esistano già prima del Play.
2. Controllare il Game View a `360 × 640`, `390 × 844` e `412 × 915`.
3. Verificare leggibilità, margini, Safe Area, altezza `INIZIA` e target touch dei comandi.
4. Aprire, modificare, chiudere e riaprire Impostazioni; cambiare scena e confermare che i valori persistano.
5. Premere rapidamente due volte `INIZIA` e ciascun comando: non devono comparire caricamenti o effetti duplicati.
6. Provare i quattro feedback e verificare che HP e Focus rimangano invariati.
7. Tornare al menu e controllare la Console per errori, Missing Script o Missing Reference.
8. Entrare e uscire dal Play Mode e confermare che le gerarchie persistenti non cambino.

## Limite della battle preview

La scena tutorial è una shell della futura Fase 3. I valori `Vita 100/100`, `Corruzione 100/100` e `Focus 0/3` sono solo presentazione. Non esistono danni, costi, formule, turni, IA, vittoria, sconfitta o purificazione. Le coroutine muovono effetti preesistenti e mostrano feedback, senza fisica e senza attacco continuo.

La Fase 3 potrà trasformare questa shell in un vero tutorial e introdurre le regole del combattimento a turni. Nessuna parte di quel sistema è implementata qui.
