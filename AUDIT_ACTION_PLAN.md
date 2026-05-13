# AUDIT_ACTION_PLAN - OnlyWinget

## Priorita' immediate

1. **Aggiungere cancellazione e timeout operativi granulari**
   - Gravita': alta
   - Sforzo: alto
   - Rischio se ignorato: UI bloccata per ore o indefinitamente durante install/update/elevazione.
   - Dopo l'intervento testare: timeout winget diretto, UAC annullato, processo elevato bloccato, annullamento singolo pacchetto e annullamento batch.

2. **Mettere in sicurezza import preset e argomenti avanzati**
   - Gravita': alta
   - Sforzo: medio
   - Rischio se ignorato: file preset non fidati possono eseguire opzioni installer arbitrarie.
   - Dopo l'intervento testare: import con `--custom`, import con `--override`, conferma esplicita, salvataggio/riapertura, apply negato se non confermato.

3. **Proteggere dati utente dopo load invalido/legacy**
   - Gravita': alta
   - Sforzo: medio
   - Rischio se ignorato: sovrascrittura di preset recuperabili dopo warning.
   - Dopo l'intervento testare: JSON invalido, file vuoto, formato legacy, save bloccato, backup creato, percorso di recupero documentato.

4. **Sostituire o irrobustire parser manifest/output**
   - Gravita': alta
   - Sforzo: alto
   - Rischio se ignorato: selezioni installer errate quando winget o winget-pkgs cambiano formato.
   - Dopo l'intervento testare: manifest reali complessi, YAML multilinea/list inline, localizzazioni diverse, output ambiguo, fallback esplicito.

5. **Rendere l'integrazione GitHub resiliente**
   - Gravita': media
   - Sforzo: medio
   - Rischio se ignorato: reduced mode frequente, UX lenta e scelte non coerenti con sorgente/versione.
   - Dopo l'intervento testare: rete assente, 404, 500, timeout, risposta grande, cache hit/miss.

## Interventi consigliati in ordine

1. Introdurre modello operativo cancellabile: `OperationContext`, `CancellationToken`, pulsante UI, timeout per tipo comando, stato cancellato/timeout.
2. Cambiare `ElevatedWingetLauncher` per supportare timeout, logging migliore, gestione UAC lasciato aperto e kill opzionale controllato.
3. Aggiungere guardia dati: dopo `InvalidData` o `IoError`, disabilitare save sul file originario finche' non c'e' backup/recupero o scelta utente.
4. Introdurre trust boundary per preset importati: flag `RequiresReview`, conferma avanzata, disabilitazione apply per righe non revisionate con argomenti custom/override.
5. Separare use case da `MainViewModel`: `SearchPackagesWorkflow`, `PresetApplyWorkflow`, `UpdatesWorkflow`, `PackageInterrogationWorkflow`.
6. Sostituire parser YAML manuale con parser robusto o fixture parser dedicato ben coperto; aggiungere corpus manifest da winget-pkgs.
7. Normalizzare error handling: codici errore, log applicativo locale, messaggi utente non grezzi, diagnostica tecnica separata.
8. Rendere smoke test realmente skipped quando disabilitati e pianificare job periodico con winget reale.
9. Hardening packaging: guardie su `Reset-Directory`, validazione `RemoveFolderEx`, visibilita' uninstall documentata.
10. Valutare migrazione xUnit v3 o registrare decisione motivata di restare su v2.

## Rischi se non si interviene

- Blocco operativo su macchine utente durante installer interattivi, UAC, rete lenta o winget appeso.
- Perdita dati preset in casi di file corrotto/legacy.
- Installazioni con opzioni non previste per manifest non parseati correttamente.
- Superficie di rischio da preset importati con argomenti installer arbitrari.
- Falsa fiducia da test verdi che non esercitano i flussi piu' rischiosi.
- Diagnostica insufficiente per problemi produzione non riproducibili localmente.

## Cosa testare dopo ogni intervento

- **Timeout/cancel**: test unitari su `WingetService`, test UI command-state, test launcher elevato con processo finto, manual test UAC.
- **Argomenti avanzati**: import JSON non fidato, preview, conferma, salvataggio, apply, redazione log.
- **Dati invalidi**: load invalid/legacy, save, backup, recovery, import/export.
- **Parser**: corpus fixture con manifest reali e output winget in inglese/italiano; casi ambigui e no installer.
- **GitHub integration**: mock HTTP con 404/500/timeout/large content; cache e retry.
- **Packaging**: package x86/x64/bundle, install/uninstall in VM, verifica folder cleanup e visibilita' uninstall.
- **CI**: unit, format, build, package, smoke reale abilitato, vulnerabilita', deprecazioni.
