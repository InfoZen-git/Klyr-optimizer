# Changelog

Toutes les modifications notables de Klyr sont documentées ici.

Le format suit [Keep a Changelog](https://keepachangelog.com/fr/1.1.0/) et le projet
utilise un versionnement semantique [SemVer](https://semver.org/lang/fr/).

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
