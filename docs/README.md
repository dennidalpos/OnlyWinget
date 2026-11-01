# Documentazione OnlyWinget

OnlyWinget gestisce preset di applicazioni, ricerca nelle sorgenti WinGet, aggiornamenti delle app e Windows Update da un'interfaccia WinUI 3 in inglese e italiano.

| Destinatario | Documento | Contenuto |
| --- | --- | --- |
| Chi vuole iniziare | [README](../README.md) | Requisiti, distribuzioni supportate, primo avvio, dati locali e limiti. |
| Utente finale | [Guida utente](user-guide.md) | Percorsi operativi e diagrammi Mermaid per preset, ricerca, aggiornamenti, sorgenti, attività e impostazioni. |
| Sviluppatore | [Architettura](architecture.md) | Dipendenze, composizione, persistenza, processi esterni e interfaccia. |
| Manutentore | [Operazioni](operations.md) | Comandi, verifiche, isolamento dei dati, cancellazione e recupero. |
| Responsabile del rilascio | [Release](release.md) | Versionamento, gate, setup NSIS, ZIP portatile e limiti della qualificazione. |
| Chi usa gli script | [Riferimento script](../scripts/README.md) | Task runner, parametri, prerequisiti e regressioni disponibili. |

I diagrammi descrivono i flussi implementati; non attestano l'esecuzione di installazioni o il superamento dei gate di rilascio. I documenti datati presenti in questa cartella riportano verifiche storiche e vanno letti con il loro scenario e la loro data.

Le fonti tecniche principali sono [i progetti](../src), [il task runner](../scripts/run.ps1), [la configurazione SDK](../global.json) e [i workflow CI](../.github/workflows). Le istruzioni per gli agenti sono in [AGENTS.md](../AGENTS.md) e nelle [skill del progetto](../.agents/skills).
