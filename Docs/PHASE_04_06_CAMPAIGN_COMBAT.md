# Fasi 04-06 — Campagna, nemici adattivi e scelta finale

## Flusso della campagna

La prima sequenza di World01 è:

1. `SCN_MainMenu` → `INIZIA` apre `SCN_W01_L01_Tutorial`.
2. La vittoria nel tutorial sblocca il Livello 2. Una sconfitta non sblocca nulla.
3. `CONTINUA` apre `SCN_W01_L02_ThornGuardian`.
4. Dopo aver sconfitto il Custode del Rovo, la scelta confermata `SALVA` o `UCCIDI` sblocca il Livello 3.
5. `CONTINUA` apre `SCN_W01_L03_AshWatcher`.
6. La scelta finale sul Vigile delle Ceneri completa il primo blocco. Il menu mostra il riepilogo delle due decisioni e permette di rigiocare il Livello 3.

Il progresso è locale e versionato. Memorizza il tutorial completato e, separatamente, l'esito `Saved` o `Killed` dei due incontri. `RIGIOCA TUTORIAL` non cancella né modifica le decisioni già registrate.

## Comandi del giocatore

| Comando | Effetto |
| --- | --- |
| `ATTACCO` | Infligge 20 danni e consuma il turno. |
| `GUARDIA` | Annulla interamente il prossimo Attacco o Colpo caricato nemico. Resta preparata finché non blocca un colpo e non è cumulabile. |
| `TECNICA` | Infligge 32 danni. Dopo l'uso richiede 2 azioni di combattimento per tornare disponibile. |
| `ANALIZZA` | Mostra nome, razza, corruzione, emozione, tendenza e intenzione. Non consuma il turno, non modifica HP o cooldown e non entra nella memoria delle azioni di combattimento. Il nemico percepisce comunque di essere stato analizzato e può rispondere. |

L'intenzione nemica è scelta e bloccata prima del comando del giocatore. Il nemico può commentare ciò che il giocatore preme, ma non può cambiare la mossa già annunciata per contrastarlo nello stesso turno.

I nemici possono usare:

- `ATTACCO`: danno immediato;
- `GUARDIA`: riduce del 65% il prossimo danno ricevuto e poi si consuma;
- `CARICA`: non infligge danno e annuncia il Colpo caricato;
- `COLPO CARICATO`: si attiva nel turno successivo ed è completamente bloccabile con Guardia.

## Valori finali degli incontri

Le statistiche comuni di Hero01 sono 100 HP, Attacco 20, Tecnica 32 e cooldown Tecnica di 2 azioni.

| Valore | Livello 2 | Livello 3 |
| --- | --- | --- |
| Nemico | Custode del Rovo | Vigile delle Ceneri |
| Razza | Custode Silvano | Umano Mutato |
| HP | 115 | 130 |
| Corruzione iniziale | 58% | 82% |
| Emozione iniziale | Triste | Arrabbiato |
| Intelligenza | 1 | 2 |
| Attacco nemico | 22 | 24 |
| Colpo caricato | 40 | 44 |
| Riduzione Guardia nemica | 65% | 65% |
| Seed deterministico | 2403 | 3503 |
| Ritorno al menu | 2,5 secondi | 2,5 secondi |

Il Custode introduce Guardia, carica e dialoghi coscienti. Il Vigile conserva le ultime 6 azioni completate, riconosce Attacchi o Guardie ripetuti e il ritmo della Tecnica. Se il giocatore cambia strategia, la sicurezza della previsione diminuisce. Una contromossa adattiva non può mai superare il 65% di probabilità: il nemico impara, ma non legge il futuro.

Corruzione ed emozione influenzano il comportamento. Lo stato emotivo può diventare Guardingo, Spaventato o Rassegnato e `ANALIZZA` mostra sempre il valore corrente. La corruzione resta compresa tra 0 e 100.

## Scelta `SALVA` o `UCCIDI`

Quando gli HP del nemico arrivano a zero, il nemico è sconfitto ma non ancora morto:

1. i comandi di combattimento vengono disabilitati;
2. compare `SCELTA FINALE` con `SALVA` e `UCCIDI`;
3. la scelta apre una conferma con `CONFERMA` e `INDIETRO`;
4. solo la conferma registra definitivamente l'esito.

`SALVA` porta la corruzione a 0 e registra `Saved`. `UCCIDI` registra `Killed` senza effetti cruenti. Se Hero01 perde, viene mostrato `SCONFITTA`, non appare alcuna scelta e la progressione non cambia.

## Menu e reset del progresso

Il menu mostra il prossimo scontro e, dopo ogni incontro, il riepilogo `SALVATO` o `UCCISO`. Il pulsante principale cambia tra `INIZIA`, `CONTINUA` e `RIGIOCA LIVELLO 3`.

`AZZERA PROGRESSI` è disponibile quando esiste un salvataggio. Apre sempre una finestra di conferma: `CONFERMA` cancella soltanto la progressione della campagna, mentre `INDIETRO` annulla l'operazione. Le impostazioni audio non vengono cancellate.

## Strumenti Unity

Eseguire gli strumenti soltanto fuori dal Play Mode:

- `Tools > Veyra > Campaign > Create Encounters 02-03`: crea o rigenera Livello 2, Livello 3 e controlli campagna del menu; aggiorna inoltre le Build Settings. Può essere eseguito più volte senza creare duplicati.
- `Tools > Veyra > Campaign > Validate Phases 04-06`: controlla modelli, apprendimento, progressione, riferimenti persistenti, scene e Build Settings.

Ordine previsto nelle Build Settings:

1. `SCN_MainMenu`
2. `SCN_W01_L01_Tutorial`
3. `SCN_W01_L02_ThornGuardian`
4. `SCN_W01_L03_AshWatcher`

## Prova manuale rapida

1. Fuori dal Play Mode, eseguire due volte `Create Encounters 02-03`, poi `Validate Phases 04-06`.
2. Aprire il menu, azzerare i progressi con la conferma e premere `INIZIA`.
3. Vincere il tutorial e verificare che il menu proponga il Custode del Rovo.
4. Nel Livello 2 usare `ANALIZZA`: HP, intenzione e cooldown devono restare invariati. Chiudere il dossier, leggere una carica e usare Guardia contro il Colpo caricato.
5. Sconfiggere il Custode, scegliere `SALVA`, confermare e verificare che il menu proponga il Vigile delle Ceneri mostrando `CUSTODE: SALVATO`.
6. Nel Livello 3 ripetere Attacco per far riconoscere l'abitudine, poi cambiare sequenza con Guardia e Tecnica: il feedback deve indicare che la previsione è diventata meno sicura.
7. Sconfiggere il Vigile, scegliere `UCCIDI`, confermare e controllare nel menu il riepilogo di entrambi gli esiti.
8. Usare `RIGIOCA LIVELLO 3` per verificare l'esito opposto sul Vigile. Per riprovare il Custode, azzerare la campagna e ripetere il percorso.
9. Per verificare rapidamente il bilanciamento del Custode: `ATTACCO` per 7 turni produce `SCONFITTA` con il nemico a 1 HP; `TECNICA`, `ATTACCO`, `GUARDIA` sul Colpo caricato, `TECNICA`, `ATTACCO`, `ATTACCO` produce invece la vittoria con Hero01 a 34 HP. Dopo la sconfitta non deve apparire `SCELTA FINALE` e non deve essere sbloccato alcun nuovo livello.

Alla fine della verifica, uscire dal Play Mode e controllare che la Console non contenga errori o eccezioni runtime.
