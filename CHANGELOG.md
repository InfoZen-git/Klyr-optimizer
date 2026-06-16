# Changelog

Toutes les modifications notables de Klyr sont documentées ici.

Le format suit [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/) et le projet
utilise un versionnement semantique [SemVer](https://semver.org/lang/fr/).

---

## [2.3.0] — En préparation

Grosse mise à jour inspirée des meilleures features de **Kudu** et **VoltAir**, repensées pour aller plus loin.

### Added — Monitoring matériel (LibreHardwareMonitor)
- Lecture réelle des capteurs via `LibreHardwareMonitorLib` : température + usage GPU (toutes marques), vitesse disque read/write
- `HardwareMonitorService` (singleton thread-safe, best-effort, ne throw jamais)
- Dashboard enrichi : vitesse disque live, **nouvelle carte GPU** (nom + usage + température)
- ⚠️ Température **CPU retirée de l'affichage** : sur AMD/Intel elle dépend du driver ring0 WinRing0, bloqué par l'Intégrité mémoire (HVCI) de Windows 11 → lecture `0` non fiable. On n'affiche rien plutôt qu'une valeur fausse. Le Performance Score n'utilise donc que CPU/RAM/disque.

### Added — Performance Score
- Note globale **/100** affichée en grand sur le Dashboard, recalculée toutes les 2 s
- `PerformanceScoreService` : pondération marge CPU (35) + marge RAM (35) + espace disque libre (30), poids renormalisés si capteur absent
- Code couleur + libellé (Excellent / Bon / Moyen / Faible / Critique)

### Added — Désinstalleur de programmes + détection des restes
- Liste les programmes installés (registre Uninstall HKLM 64/32 bits + HKCU), filtre les composants système et mises à jour
- Désinstallation (mode silencieux si dispo), puis **scan des dossiers résiduels** (InstallLocation, %AppData%, %LocalAppData%, %ProgramData%, Program Files) avec suppression sur confirmation
- Recherche + sélection multiple

### Added — Software Updater (winget)
- Détecte les apps avec maj dispo via `winget upgrade`, parsing robuste de la sortie tabulaire
- Mise à jour en masse en un clic, fallback propre si winget absent

### Added — Disk Analyzer
- Scanne un lecteur/dossier, calcule la taille de chaque sous-dossier (récursif parallélisé), trie par taille
- Barres proportionnelles colorées + drill-down (clic pour explorer, bouton parent, sélecteur de lecteur)

### Added — Startup Manager
- Liste les programmes au démarrage (registre Run HKLM/HKCU + dossiers Startup)
- Activation/désactivation **réversible** (backup dans une clé/dossier Klyr dédié, jamais de suppression destructive)

### Added — Browser Cleaner
- Détecte Chrome, Edge, Brave, Firefox ; nettoie cache / cookies / historique séparément (cases à cocher par navigateur)
- Calcul de l'espace libéré affiché à l'écran et journalisé

### Added — Mise à jour in-app (auto-updater)
- `UpdateService` : vérifie les GitHub Releases au démarrage (GET API, **aucune donnée envoyée**, pas de télémétrie)
- Bouton « v X.Y disponible » dans la barre de titre quand une version plus récente existe → ouvre la page de release
- Setting `EnableUpdateCheck` (désactivable) ; **à configurer** : `GitHubOwner`/`GitHubRepo` dans `UpdateService.cs` (no-op sûr tant que non renseigné)

### Added — Diagnostic enrichi
- Le rapport diagnostic (`system_info.txt`) inclut désormais les capteurs : temp/charge CPU, GPU (nom/temp/usage), vitesses + temp disque

### Added — Scans programmés
- Nettoyage automatique quotidien/hebdo/mensuel via le Planificateur de tâches Windows (`schtasks`)
- Mode headless `Klyr.exe --scheduled-clean` : nettoie temp + caches navigateurs sans ouvrir l'UI, puis se ferme
- UI dans Paramètres (toggle + fréquence)

### Changed
- Sidebar : nouvelle section « Outils » (Désinstalleur, Mises à jour, Analyseur disque, Démarrage, Navigateurs) ; nav désormais scrollable
- Espace libéré par les optimisations de nettoyage désormais **journalisé dans les logs** (mesure réelle du delta d'espace libre sur C:) — l'onglet « Historique » dédié a été retiré car redondant avec les logs
- Badges d'optimisation simplifiés : **Admin / Redémarrage / Sécurité** uniquement (badges « objectif » génériques et « Avancé » retirés)
- `SystemInfoModel` étendu (températures, GPU, vitesses disque, score)
- Version assembly bumpée à 2.3.0.0
- Dépendance ajoutée : `LibreHardwareMonitorLib` 0.9.6

### Notes
- La lecture des températures nécessite les droits administrateur (accès driver ring0). Sans admin, usages/charges restent dispo, températures à « — ».
- Toutes les nouvelles chaînes sont traduites FR + EN.

---

## [2.2.0] — En préparation

### Added
- **Module Streaming / Création** (6 optimisations) :
  - Mode Streamer (Plan d'alimentation Hautes performances + désactivation C-States CPU)
  - Priorité CPU encoder auto (détection OBS / Streamlabs / Davinci / Premiere / Vegas / AE)
  - Game Mode OFF (encodeurs matériel NVENC/QSV/AMF libérés) — Avancé
  - HAGS OFF (Hardware-accelerated GPU scheduling) — Avancé, reboot requis
  - Killer processus Creator (Adobe Updater, Edge, Spotify, Discord, Teams, OneDrive)
  - Cleanup cache OBS / Streamlabs browser sources
- **Cancellation par optimisation** (P1-02) :
  - Bouton « Annuler » par item, visible pendant l'exécution
  - `Func<CancellationToken, Task<string>>` propagé jusqu'à `SystemService.RunCmdAsync` / `RunPowerShellAsync`
  - Le clic sur Annuler tue réellement le processus en cours (SFC, DISM, antivirus, etc.)
  - Nouveau statut « Annulé » (orange) + `CommandResult.WasCancelled`
- **Quarantaine antivirus** : après détection, dialog Yes/No qui propose `Remove-MpThreat` sur chaque menace
- **Localisation FR + EN complète** :
  - Infra `Resources/Strings.resx` (FR neutre) + `Strings.en.resx` (satellite EN auto-extrait en `en/Klyr.resources.dll`)
  - Setting `Language` (auto / fr / en) avec sélecteur dans Settings + message « redémarrage requis »
  - Init de culture au démarrage dans `App.xaml.cs` (avant chargement UI)
  - **Tout** est traduit : sidebar nav, page titles, boutons (Run, Cancel, Run All, Stop, Save, Close…), status des optims, splash screen, dialogs (Confirm, Run All, Critical, Admin Required, Reboot, Quarantine, Process Killer), Settings (titre, toggles + descriptions, sélecteur de langue), Dashboard (cards CPU / RAM / Disk / Session / System, Recent Activity, Uptime), badges (Admin / Reboot / Advanced), Terminal (LIVE, Clear, Export as .txt), About (Version / Developer / Compatibility / Framework / Diagnostic / Copy / Export), Legal (Privacy & GDPR, Restoration Guarantee, Disclaimer, Audit log, Footer), les **40 noms d'optimisations**, les **40 descriptions d'optimisations**, et les principaux **messages de résultat des lambdas** (~35 entrées).

### Changed
- `OptimizationItem.Action` : signature passée de `Func<Task<string>>` à `Func<CancellationToken, Task<string>>`
- `SystemService.RunCmdAsync` / `RunPowerShellAsync` : nouveau paramètre optionnel `cancellationToken` qui kill le process via CTS lié au timeout interne
- `ProgressHelper.RunWithProgressAsync` : reçoit et propage `CancellationToken`
- Status interne reste FR pour préserver les DataTriggers XAML ; nouveau `StatusDisplay` traduit utilisé pour l'affichage
- Version assembly bumpée à 2.2.0.0
- `Legal_*` content : les Runs en gras inline ont été remplacés par des TextBlocks simples (compromis pour permettre la localisation propre via x:Static)

---

## [2.1.0] — Mai 2026

### Added
- Outils diagnostic intégrés dans `À propos` : « Copier infos système » (markdown presse-papier) et « Exporter rapport » (zip logs + settings + diagnostic)
- Auto-save des logs dans `Documents\Klyr\Logs\Klyr_YYYYMMDD.log` (un fichier par jour, append-only)
- Setting `AutoSaveLogs` (default `true`) pour désactiver l'auto-save si besoin
- Service `DiagnosticService` centralisant la génération de diagnostic
- Confirmation explicite avec preview de la liste des processus avant `gaming_kill_processes`
- Module Réseau (9 optimisations) : reset Winsock/TCP, DNS rapides auto-bench, optimisation ping, test vitesse, blocage télémétrie hosts
- Module Nettoyage (8) : disk cleanup, bloatware, SFC/DISM, scan Defender intégré
- Mentions légales + section RGPD
- Splash screen avec barre de progression
- Crash handler centralisé (`ErrorHandler` + crash reports persistants)
- Vérification droits admin au démarrage
- Icône `.ico` multi-résolution (16/32/48/256)
- Mode simulation pour les optimisations critiques
- Settings persistés en JSON dans `%AppData%\Klyr\settings.json`
- Système d'icônes Fluent (Segoe Fluent Icons) — police vectorielle native Windows
- Micro-interactions UI (hover scale sur cartes, press scale sur boutons)

### Changed
- Detection OS conviviale : « Microsoft Windows NT 10.0.26200.0 » → « Windows 11 25H2 »
- Refresh dashboard temps réel : RAM + disque + uptime rafraîchis toutes les 2 secondes
- Capture sortie process élevés réécrite avec des fichiers temp (PowerShell + CMD admin) : avant la sortie était silencieusement perdue à cause de `UseShellExecute=true`
- Thème clair entièrement fonctionnel : palette étendue à 45 brushes, 5 fichiers XAML migrés vers `DynamicResource` (~170 valeurs hex remplacées)
- Build self-contained par défaut : runtime .NET 10 embarquée, plus aucun prérequis sur la machine cible
- Cohérence architecture : csproj `<PlatformTarget>x64</PlatformTarget>` aligné sur `app.manifest processorArchitecture="amd64"`
- Barres de progression responsives via `PercentToStarConverter` (au lieu de largeurs pixel hardcodées)
- Cache d'items par page : la progression d'une optim ne disparaît plus quand on change de page et qu'on revient
- Refonte UI : palette sombre sobre, sans effets néon
- API Win32 `EmptyWorkingSet` pour libération RAM réelle
- Antivirus scan timeout passé de 30 secondes à 10 minutes (cohérent avec un scan complet)
- Suppression intégrale des emojis dans l'UI (remplacés par des glyphs vectoriels Fluent Icons)

### Fixed
- Garde admin (`RequiresAdmin`) bloque réellement l'exécution si parent non admin
- Validation effective des restore points (lecture du `SequenceNumber` créé)
- Timeouts CMD/PowerShell tuent désormais le process tree au lieu de simplement timeout l'attente
- Buffer logs limité à 2000 entrées (avant : croissance infinie)
- Timer de refresh disposé proprement à la fermeture
- PerformanceCounter CPU en singleton (avant : recréation à chaque tick)
- `PercentToWidthConverter` accepte désormais `int` (corrige la barre qui restait à 0%)
- Backups ciblés en place (réseau, pare-feu, hosts) avant ops destructives
- Détection HDD/SSD via MediaType (au lieu de DeviceId fragile)
- AboutWindow affichait « .NET 8 » alors que csproj cible `.NET 10`

---

## [2.0.0] — Décembre 2025

Première version interne. Modules Gaming + Vieux PC.
