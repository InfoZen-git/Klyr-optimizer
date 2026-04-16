# InfoZen — Audit profond des problèmes & bottlenecks

_Date : 05 avril 2026_

## Méthodologie d’audit

Audit statique complet du code source (`Views`, `ViewModels`, `Services`, `Models`, `Commands`, `installer`, `manifest`, `csproj`) avec focus :
- fiabilité runtime,
- sécurité et privilèges,
- bottlenecks performance (CPU/RAM/UI/I/O),
- cohérence architecture/UX,
- robustesse opérationnelle.

> Important : cet audit est **très profond**, mais reste statique (pas d’exécution runtime Windows dans cet environnement Linux).

---

## Résumé exécutif

Le projet est proprement structuré, mais présente des risques significatifs dans 4 zones :

1. **Privilèges admin et retours d’exécution trompeurs** (P0/P1)
2. **Gestion incomplète des processus longs / timeout / cancellation** (P1)
3. **Bottlenecks de logs + rendering terminal (croissance non bornée)** (P1)
4. **Incohérences fonctionnelles (settings non utilisés, versioning, UX partiellement câblée)** (P2)

---

## Gravité

- **P0** : risque critique (fonction casse, faux succès, opération système dangereuse)
- **P1** : risque élevé (fiabilité/perf forte dégradation)
- **P2** : risque moyen (qualité, dette, incohérence)
- **P3** : risque faible (amélioration)

---

## A) Problèmes critiques (P0)

### P0-01 — Exécutions admin non traçables (faux positifs possibles)
**Zone** : `SystemService.RunCmdAsync`, `SystemService.RunPowerShellAsync`  
**Symptôme** : quand `asAdmin=true`, `UseShellExecute=true` empêche redirection stdout/stderr ; le code retourne souvent chaîne vide.  
**Impact** : UI peut considérer l’action réussie alors que la commande a échoué (ou a été refusée UAC).  
**Cause racine** : design mélange “élévation UAC shell” et “capture de sortie” dans un même flux impossible nativement avec `ProcessStartInfo`.  
**Bottleneck produit** : débogage très difficile ; fiabilité perçue faible.  
**Recommandation** :
- Séparer les runners :
  - runner non-élevé avec capture complète,
  - runner élevé via helper dédié (service local/planificateur/tâche), avec canal de résultat,
- au minimum retourner un statut explicite (`StartedElevatedNoOutput`) au lieu de `""`.

### P0-02 — Contrat `RequiresAdmin` non contraignant
**Zone** : modules d’optimisation + `MainViewModel.RunOptimizationAsync`  
**Symptôme** : `RequiresAdmin=true` n’empêche pas l’exécution si app non admin ; beaucoup d’actions partent quand même.  
**Impact** : échecs imprévisibles, états partiellement appliqués, confusion utilisateur.  
**Cause racine** : `RequiresAdmin` sert de badge/UX, pas de garde métier stricte.  
**Recommandation** :
- Garde centrale avant action : si non admin + `RequiresAdmin`, bloquer proprement avec message et option relance admin.

### P0-03 — Point de restauration potentiellement non fonctionnel par défaut
**Zone** : `SystemService.CreateRestorePointAsync`  
**Symptôme** : exécution en `asAdmin:false` alors que `Checkpoint-Computer` requiert souvent élévation et préconditions.  
**Impact** : faux sentiment de sécurité ; rollback potentiellement absent.  
**Recommandation** :
- valider explicitement la création (code retour réel + existence RP),
- exiger admin pour cette opération,
- échouer dur si impossible, au lieu de continuer silencieusement.

### P0-04 — Opérations très destructrices sans “dry-run” ni sauvegarde dédiée
**Zone** : nettoyage logs Windows, reset pare-feu, reset réseau, édition `hosts`, suppression startup, bloatware remove  
**Impact** : perte de traçabilité système / régression connectivité / rollback difficile.  
**Cause racine** : actions immédiates, pas de snapshot spécifique par action (hors restore point optionnel).  
**Recommandation** :
- stratégie de pré-backup ciblée (hosts, règles firewall, netsh export),
- mode “simulation” + confirmation renforcée par catégorie critique.

---

## B) Problèmes majeurs (P1)

### P1-01 — Timeout antivirus n’annule pas réellement le process
**Zone** : `CleaningOptimizations.RunAntivirusScanAsync`  
**Symptôme** : `WaitAsync(cts.Token)` annule l’attente, mais pas forcément le process PowerShell/Defender en cours.  
**Impact** : process orphelins, charge CPU/disque prolongée, incohérence UI (“annulé” mais scan continue).  
**Recommandation** : runner cancellable avec PID tracking + kill contrôlé si timeout.

### P1-02 — Aucune cancellation utilisateur des tâches longues
**Zone** : `RunOptimizationAsync`, `RunAllOptimizationsAsync`, commandes système longues (`SFC`, `DISM`, `defrag`)  
**Impact** : UX bloquée pendant longtemps, impossible d’arrêter proprement.  
**Recommandation** : `CancellationToken` propagé de bout en bout + bouton Stop global.

### P1-03 — Absence de timeout générique sur CMD/PowerShell
**Zone** : `SystemService.RunCmdAsync`, `RunPowerShellAsync`  
**Impact** : risque de hang indéfini sur commandes système cassées/en attente.  
**Recommandation** : timeout configurable + kill process + retour d’erreur standardisé.

### P1-04 — Terminal/log mémoire non bornée
**Zone** : `LogService` (`_fullLog`) + binding `TerminalOutput`  
**Symptôme** : accumulation infinie en mémoire + réaffichage complet du texte à chaque log.  
**Impact** : dégradation progressive CPU/RAM, UI lag sur longues sessions.  
**Bottleneck** : complexité quasi O(n) par append côté rendu UI sur buffer croissant.  
**Recommandation** :
- buffer circulaire (ex: 2 000 lignes max),
- append incrémental UI, pas rebinding complet du bloc texte.

### P1-05 — Timer de refresh jamais libéré
**Zone** : `MainViewModel` (`System.Timers.Timer`)  
**Symptôme** : timer démarré sans `Stop/Dispose`; abonnement `Log.PropertyChanged` non désabonné.  
**Impact** : fuite potentielle lors de cycles de fenêtres / tests / navigation avancée.  
**Recommandation** : implémenter `IDisposable` dans VM + cleanup explicite.

### P1-06 — CPU usage polling coûteux
**Zone** : `SystemService.GetCpuUsage()`  
**Symptôme** : création d’un `PerformanceCounter` à chaque tick + `Thread.Sleep(100)`.  
**Impact** : overhead inutile toutes les 2s ; peut dériver selon machine.  
**Recommandation** : maintenir un counter singleton + lecture amortie (ou perf API plus moderne).

### P1-07 — Incohérence architecture privilèges (manifest/app)
**Zone** : `app.manifest` (`asInvoker`) vs grand volume d’actions admin  
**Impact** : parcours utilisateur fragile (beaucoup de features “échouent partiellement” sans relance admin).  
**Recommandation** : choisir une stratégie unique :
- soit app lancée admin explicitement,
- soit élévation ciblée fiable par opération avec retour structuré.

### P1-08 — Exécutions séquentielles “Run All” très longues sans orchestration avancée
**Zone** : `RunAllOptimizationsAsync`  
**Bottleneck** : exécution strictement séquentielle, confirmations multiples, aucune priorisation/cancellation.  
**Impact** : temps total élevé, expérience laborieuse, risque d’interruption incomplète.

---

## C) Problèmes moyens (P2)

### P2-01 — Settings présents mais non réellement appliqués
**Constats** :
- `ShowAdminBadge` stocké, mais badges semblent affichés directement sur `RequiresAdmin` (pas conditionnés),
- `AutoScrollTerminal` stocké, mais pas de logique de scroll automatique observée,
- `Theme` et `LastModule` persistés mais pas exploités dans le flux principal.
**Impact** : dette UX/produit ; options “fantômes”.

### P2-02 — Incohérence version framework affichée
**Zone** : `AboutWindow` affiche `.NET 8 + WPF` alors que `csproj` cible `.NET 10`.  
**Impact** : confusion support, diagnostic et communication release.

### P2-03 — `PageTitle` gère `Legal` mais la navigation ouvre une fenêtre modale
**Impact** : incohérence conceptuelle (page interne vs dialog externe), dette de design VM/UI.

### P2-04 — Multiples `catch { }` silencieux
**Zone** : services système/settings/admin/etc.  
**Impact** : pertes d’info diagnostique, bugs masqués.  
**Recommandation** : journaliser au moins niveau `WARN` avec contexte.

### P2-05 — Statut d’exécution non typé (strings fragiles)
**Zone** : VM et modules (`"❌"`, `"[ERR]"`, `"Terminé"`)  
**Impact** : logique dépendante de textes localisés ; fragile aux variations message.  
**Recommandation** : résultat structuré (`ResultCode`, `Message`, `Severity`, `RequiresReboot`).

### P2-06 — Process kill list agressive et non contextualisée
**Zone** : `gaming_kill_processes`  
**Impact** : fermeture d’apps utilisateur sans opt-in fin ni whitelist dynamique ; risques métier UX.  
**Recommandation** : prévisualisation + sélection fine.

### P2-07 — Détection disque HDD/SSD probablement imprécise
**Zone** : `oldpc_defrag` (`DeviceId -eq 0`)  
**Impact** : peut cibler mauvais disque / faux diagnostic sur machines multi-disques.

### P2-08 — Mise à jour partielle de la télémétrie système dashboard
**Zone** : RAM/disque majoritairement snapshot initial ; CPU rafraîchi périodiquement.  
**Impact** : dashboard “semi temps réel”, potentiellement trompeur.

### P2-09 — Largeurs de barres hardcodées
**Zone** : XAML convertisseur `% -> width` avec paramètres fixes (140/200).  
**Impact** : rendu non responsive selon layout DPI/fenêtre.

### P2-10 — `AnyCPU` + manifest `amd64`
**Zone** : `InfoZen.csproj` vs `app.manifest`  
**Impact** : incohérence de ciblage architecture ; potentiel bruit de maintenance.

### P2-11 — Dépendance forte au shell/strings PowerShell
**Impact** : maintenance difficile, testabilité faible, gestion d’erreurs hétérogène.

### P2-12 — Export logs manuel uniquement
**Impact** : en cas de crash dur précoce, pertes contextes potentiels hors crash report.

---

## D) Problèmes faibles (P3)

### P3-01 — Description fonctionnelle parfois trop optimiste vs réalité technique
Exemple : “progression réelle antivirus” alors que retour réel est partiellement synthétique.

### P3-02 — Dossiers `bin/obj/publish` présents dans workspace source
Impact principal : poids dépôt, bruit revue, risque de confusion artefacts/code.

### P3-03 — Uniformisation messages utilisateur
Mélange icônes/format FR/EN et wording technique hétérogène.

---

## E) Bottlenecks détaillés (performance)

## E1 — Pipeline logs terminal
- **Cause** : `TerminalOutput` rebinding complet à chaque entrée.
- **Effet** : coût de rendu croissant avec taille historique.
- **Symptôme** : latence UI après longues sessions.
- **Priorité** : Haute (P1).

## E2 — Sampling CPU
- **Cause** : recréation `PerformanceCounter` + sleep synchrone.
- **Effet** : micro-coûts répétés + bruit mesure.
- **Priorité** : Moyenne/haute (P1-P2).

## E3 — Exécution monolithique séquentielle des optimisations
- **Cause** : orchestration simple sans classification des durées/risques.
- **Effet** : run global lent, non interruptible.
- **Priorité** : Haute (P1).

## E4 — Opérations I/O réseau/système non instrumentées
- **Cause** : absence métriques temps/erreur normalisées par action.
- **Effet** : impossible d’identifier précisément les optimisations lentes.
- **Priorité** : Moyenne (P2).

---

## F) Top 15 problèmes priorisés (ordre de traitement recommandé)

1. P0-01 — Retours admin trompeurs (runner élevé)
2. P0-02 — Garde `RequiresAdmin` non bloquante
3. P0-03 — Validation réelle point de restauration
4. P1-01 — Cancellation/timeout réel des process longs
5. P1-04 — Buffer logs borné + append incrémental terminal
6. P1-03 — Timeout générique runner CMD/PS
7. P1-05 — Dispose timer + désabonnement events
8. P1-07 — Stratégie élévation unifiée (manifest vs runtime)
9. P1-08 — Orchestrateur Run All (stop/skip/retry)
10. P1-06 — Optimiser polling CPU
11. P2-01 — Activer réellement settings non branchés
12. P2-02 — Corriger incohérence .NET 8/.NET 10
13. P2-04 — Éliminer catches silencieux (logs min)
14. P2-07 — Fiabiliser détection HDD/SSD
15. P2-05 — Résultats typés au lieu de parsing string

---

## G) Risques opérationnels concrets si non corrigé

- Support difficile (faux “succès”, erreurs silencieuses)
- Régressions système non réversibles selon parcours utilisateur
- Dégradation de performances UI sur sessions longues
- Perte de confiance utilisateur (settings qui semblent inactifs)
- Coût de maintenance croissant (scripts shell non normalisés)

---

## H) Plan de remédiation conseillé (3 vagues)

## Vague 1 — Fiabilité critique (immédiat)
- Refactor runner admin/non-admin + statuts typés
- Garde admin stricte par optimisation
- Timeout/cancellation process robuste
- Validation forte des restore points

## Vague 2 — Performance & UX
- Terminal avec buffer borné + append optimisé
- Dispose VM/timer/events
- AutoScroll réel + setting badge admin effectif
- Instrumentation durée/erreur par optimisation

## Vague 3 — Cohérence & industrialisation
- Harmonisation versioning framework
- Nettoyage catches silencieux + observabilité
- Rationalisation scripts PowerShell
- Normalisation architecture disque/ciblage x64

---

## I) Conclusion

InfoZen est un bon socle produit, mais il existe des **failles de fiabilité critiques** autour de l’élévation admin et de l’exécution des commandes système, plus des **bottlenecks de performance UI/logs** qui limiteront la montée en qualité.

Le principal verrou n’est pas la quantité de fonctionnalités (déjà élevée), mais la **robustesse d’exécution**, l’**observabilité** et la **cohérence des garanties** fournies à l’utilisateur.
