<div align="center">

# Klyr

**Optimiseur PC Windows — 40 optimisations en 5 modules + monitoring matériel + 5 outils avancés. FR + EN, 100% local, sans télémétrie.**

🌐 **Français** · [English](README.en.md)

![Klyr Dashboard](Assets/Branding/screenshot-dashboard.png)

[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?style=flat-square&logo=windows)](https://www.microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-See%20LICENSE.txt-blue?style=flat-square)](LICENSE.txt)
[![Discord](https://img.shields.io/badge/Discord-Rejoindre-5865F2?style=flat-square&logo=discord&logoColor=white)](https://discord.com/invite/nPWU9cW3NG)
[![Édité par](https://img.shields.io/badge/édité_par-InfoZen_·_Yahya-4B8BF5?style=flat-square)](#)

</div>

---

## Pourquoi Klyr

Windows accumule au fil des mois des paramètres lourds, des services inutiles, de la télémétrie et des optimisations désactivées par défaut. Klyr regroupe **40 ajustements** validés en 5 modules, sans installer 50 utilitaires séparés et sans envoyer une seule donnée sur internet.

- ⚡ **Mesurable** : +377 points 3DMark mesurés après le module Gaming
- 🔒 **100% local** : aucune télémétrie, aucun tracking, aucun compte
- ↩️ **Réversible** : point de restauration automatique + backups ciblés des fichiers système
- 📖 **Open-source** : tu peux lire chaque ligne avant d'installer

---

## Nouveautés v2.3.0

```
                 KLYR  v2.2.0  ─────────────────►  v2.3.0
 ┌────────────────────────────┐      ┌────────────────────────────────────────┐
 │ DASHBOARD                   │      │ DASHBOARD                               │
 │  • CPU % / RAM % / Disque   │  ──► │  • Jauge circulaire Performance Score   │
 │                             │      │  • Carte GPU (nom / usage / température) │
 │                             │      │  • Vitesse disque live                  │
 ├────────────────────────────┤      ├────────────────────────────────────────┤
 │ MODULES (5)                 │      │ MODULES (5)  — inchangés                │
 │  Gaming · Vieux PC ·        │  ══  │  Gaming · Vieux PC · Nettoyage ·        │
 │  Nettoyage · Réseau ·       │      │  Réseau · Streaming                     │
 │  Streaming                  │      │                                        │
 ├────────────────────────────┤      ├────────────────────────────────────────┤
 │ OUTILS                      │      │ OUTILS  (+5 intégrés en onglets)        │
 │  Terminal                   │  ──► │  Terminal · Désinstalleur · Maj winget  │
 │                             │      │  · Analyseur disque · Démarrage ·       │
 │                             │      │  Navigateurs                            │
 ├────────────────────────────┤      ├────────────────────────────────────────┤
 │ SYSTÈME                     │      │ SYSTÈME                                 │
 │  • Fenêtre redimensionnable │  ──► │  • Sidebar : état actif + scrollable    │
 │  • FR / EN                  │      │  • Scans programmés (Task Scheduler)    │
 │                             │      │  • Auto-updater GitHub (opt-out)        │
 │                             │      │  • Redémarrage auto au choix de langue  │
 └────────────────────────────┘      └────────────────────────────────────────┘
        4 modules outils                    monitoring matériel temps réel
                                            + 5 outils façon Kudu/VoltAir
```

**En résumé** : la v2.2.0 était un optimiseur (5 modules). La v2.3.0 devient une **suite complète** — monitoring matériel temps réel (températures, Performance Score), 5 nouveaux outils intégrés (désinstalleur, winget, analyseur disque, démarrage, navigateurs), scans programmés et mises à jour automatiques.

---

## Installation

1. Télécharger la dernière release depuis [Releases](../../releases) → **`Klyr_Setup_v2.3.0.exe`**
2. Lancer l'installeur
3. L'app s'installe dans `C:\Program Files\Klyr` et un raccourci apparaît sur le Bureau

L'installeur intègre le runtime **.NET 10** — aucun prérequis sur la machine cible.

### Note SmartScreen

Klyr n'est pas signée avec un certificat EV. Windows SmartScreen peut afficher :

> *« Windows a protégé votre PC »*

C'est normal pour toute app non signée. Pour continuer :

1. Clic sur **« Informations complémentaires »**
2. Clic sur **« Exécuter quand même »**

Si Defender bloque le téléchargement : ajoute le dossier de téléchargement aux exclusions, ou télécharge à nouveau.

---

## Modules

| Module | Nb | Points clés |
|---|---|---|
| **Gaming / FPS** | 9 | Mode haute perf, désactivation Game DVR, déblocage FPS, latence TCP, fermeture processus parasites |
| **Vieux PC** | 8 | Désactivation services lourds, télémétrie, animations, nettoyage RAM Win32, defrag SSD/HDD intelligent |
| **Nettoyage** | 8 | Disk cleanup, suppression bloatware, SFC/DISM, scan Defender intégré + quarantaine, vidage caches |
| **Réseau** | 9 | Reset Winsock/TCP, DNS auto-bench, optimisation ping, test vitesse, blocage télémétrie hosts |
| **Streaming / Création** | 6 | Mode Streamer (perf + latence), prio CPU encoder auto, Game Mode OFF, HAGS OFF, killer parasites, cleanup cache OBS |

Les optimisations marquées **« Avancé »** (gain incertain ou risque de régression) sont masquées par défaut. Active-les dans Paramètres si tu veux pousser plus loin.

**Annulation par optimisation** : chaque optim affiche un bouton « Annuler » pendant l'exécution qui tue le processus en cours (utile pour les longues passes SFC/DISM/scan antivirus).

**Langue** : FR par défaut, EN disponible (Settings → Langue / Language). L'app suit la culture système par défaut.

---

## Monitoring matériel & Performance Score

Le Dashboard affiche en temps réel (rafraîchi toutes les 2 s) via **LibreHardwareMonitor** :

- **Performance Score /100** — note globale (charge CPU + RAM + espace disque libre), code couleur
- **Carte GPU dédiée** (nom + usage + température, via NvAPI — toutes marques)
- **Vitesse disque** read/write live
- CPU / RAM / disque avec barres responsives

> La température **GPU** s'affiche via NvAPI. La température **CPU** n'est pas affichée : sur AMD/Intel elle dépend d'un driver noyau (WinRing0) souvent bloqué par l'Intégrité mémoire (HVCI) de Windows 11, ce qui la rend non fiable — on préfère ne rien afficher plutôt qu'un « 0 » trompeur.

---

## Outils avancés

Accessibles depuis la section **Outils** du menu latéral :

| Outil | Description |
|---|---|
| **Désinstalleur** | Liste les programmes installés, désinstalle et **détecte les fichiers résiduels** (dossiers + données app) à supprimer |
| **Mises à jour** | Détecte et installe en masse les maj logicielles via **winget** |
| **Analyseur disque** | Visualise l'occupation par dossier (barres proportionnelles + drill-down) |
| **Démarrage** | Active/désactive les programmes au démarrage (réversible, sans suppression) |
| **Navigateurs** | Vide cache / cookies / historique de Chrome, Edge, Firefox, Brave |

**Scans programmés** (Paramètres → Scans programmés) : nettoyage automatique quotidien/hebdo/mensuel via le Planificateur de tâches Windows, en arrière-plan sans ouvrir l'app.

---

## Privacy & Sécurité

Klyr ne fait **aucun appel réseau** sauf :
- Test de vitesse internet (ping `1.1.1.1` sur demande utilisateur)
- Benchmark DNS (test de résolution `github.com`, `microsoft.com`, `cloudflare.com`)
- **Vérification de mise à jour** (v2.3.0) : un simple `GET` vers l'API GitHub Releases au démarrage pour comparer la version. **Aucune donnée envoyée** (lecture seule), désactivable dans *Paramètres → Vérifier les mises à jour*.
- **Software Updater** (v2.3.0) : `winget` télécharge les mises à jour des logiciels **que tu sélectionnes** (action explicite).

**Aucune donnée personnelle n'est jamais envoyée nulle part.** Pas d'analytics, pas de tracking, pas de compte.

### Backups automatiques avant ops critiques

| Action | Backup créé |
|---|---|
| `net_reset` (reset Winsock/TCP) | `Documents\Klyr\Backups\Network\` |
| `net_reset_firewall` | Export règles pare-feu via `netsh advfirewall export` |
| `net_telemetry_block` (édition hosts) | Copie `hosts` daté |
| `clean_event_logs` | Export `.evtx` avant clear |

Plus une option de **point de restauration système** automatique avant chaque optimisation admin (configurable dans Paramètres).

---

## Tu as trouvé un bug ?

Klyr embarque deux outils diagnostic dans la fenêtre `À propos` :

### Copier infos système
Copie un bloc markdown dans le presse-papier, à coller dans une [GitHub Issue](../../issues) :
```
**Klyr v2.1.0**
- OS : Windows 11 25H2
- CPU : AMD Ryzen 5 9600X
- RAM : 7.4 / 31.1 Go (24%)
- Disque C: : 765 / 930 Go libres
- Architecture : x64
- Admin : oui
```

### Exporter rapport
Génère un `.zip` dans `Documents\Klyr\Reports\` contenant :
- `system_info.txt` diagnostic complet (OS, CPU, RAM, settings, .NET version)
- `settings.json` tes paramètres
- `logs/` tous les logs persistants
- `terminal_session.txt` tampon terminal de la session

Joins ce zip à ton bug report tu n'as rien d'autre à écrire que la description du problème, le diagnostic suffit.

**Privacy** : aucune donnée personnelle dans le zip. Tu peux l'ouvrir avant de l'envoyer pour vérifier.

---

## Résultats mesurés

| Config testée | Avant | Après | Gain |
|---|---|---|---|
| Ryzen 5 9600X, RTX 5060 TI 16Gb (Full optimisation Gaming) | 3DMark 14681 | 3DMark 15058 | **+377 pts** |

> Les gains varient selon la configuration. Plus la base est non-optimisée, plus la marge est élevée.
> Les optimisations marquées « Avancé » peuvent dégrader certains setups utilise les avec discernement.

---

## Architecture

```
Klyr/
├── Klyr.sln                          Solution Visual Studio
├── Klyr.csproj                       Cible .NET 10 / WPF / x64
├── App.xaml / App.xaml.cs            Resources globales + ErrorHandler init
├── app.manifest                      UAC asInvoker + DPI PerMonitorV2
│
├── README.md / README.en.md          Cette page (FR / EN)
├── CHANGELOG.md                      Historique des versions
├── SECURITY.md                       Politique de signalement de vulnérabilité
├── LICENSE.txt                       Conditions d'utilisation
├── .gitignore  /  .gitattributes
│
├── .github/
│   ├── ISSUE_TEMPLATE/               Templates bug_report + feature_request + config.yml
│   └── copilot-instructions.md       Hints GitHub Copilot
│
├── Assets/
│   ├── Icons/icon.ico                Icône multi-résolution (16/24/32/48/64/128/256)
│   └── Branding/                     Mark + wordmark SVG + PNG export + screenshot
│
├── Commands/                         RelayCommand + AsyncRelayCommand
├── Models/                           OptimizationItem, SystemInfoModel (+ capteurs/score),
│                                     InstalledProgram, UpgradablePackage, DiskEntry, StartupEntry
├── Services/
│   ├── SystemService                 P/Invoke Win32, runner CMD/PowerShell (CancellationToken kill), restore points
│   ├── HardwareMonitorService        v2.3.0 — capteurs LibreHardwareMonitor (temp CPU/GPU, vitesse disque)
│   ├── PerformanceScoreService       v2.3.0 — note /100 pondérée
│   ├── UninstallerService            v2.3.0 — programmes installés + détection des restes
│   ├── WingetService                 v2.3.0 — software updater (parsing winget)
│   ├── DiskAnalyzerService           v2.3.0 — taille par dossier (récursif parallélisé)
│   ├── StartupManagerService         v2.3.0 — démarrage Windows (toggle réversible)
│   ├── BrowserCleanerService         v2.3.0 — cache/cookies/historique navigateurs
│   ├── ScheduledScanService          v2.3.0 — Task Scheduler (schtasks)
│   ├── SilentCleanupService          v2.3.0 — nettoyage headless (--scheduled-clean)
│   ├── UpdateService                 v2.3.0 — vérification GitHub Releases (lecture seule)
│   ├── LogService                    Buffer circulaire 2000 entrées + auto-save + catégories i18n
│   ├── SettingsService               Persistance JSON %AppData%\Klyr (langue + thème + scans + toggles)
│   ├── ThemeService                  Bascule Dark/Light dynamique (45 brushes)
│   ├── DiagnosticService             Génération diagnostic + export zip
│   ├── ErrorHandler                  Crash handler global + crash reports persistants
│   ├── AdminChecker                  WindowsPrincipal — vérif droits admin
│   ├── ProgressHelper                Animation de progression (propage CancellationToken)
│   ├── OptimizationBenchmarkService  Capture snapshots CPU/RAM/disque avant/après
│   ├── OptimizationProfileService    Métadonnées des optimisations
│   ├── GamingOptimizations           Module 1 (9)   ── OldPcOptimizations  Module 2 (8)
│   ├── CleaningOptimizations         Module 3 (8 + antivirus quarantaine)
│   ├── NetworkOptimizations          Module 4 (9)   ── StreamingOptimizations Module 5 (6)
│
├── Resources/                        Localisation FR/EN (Strings.resx + .en.resx + .Designer.cs)
├── Styles/                           Icons.xaml (Segoe Fluent Icons) + Animations.xaml
├── ViewModels/                       MainViewModel (IDisposable, nav cache, run all, CTS, OpenTool)
├── Views/                            MainWindow, SplashScreen, Settings, About, Legal,
│                                     Uninstaller, Updater, DiskAnalyzer, StartupManager,
│                                     BrowserCleaner  (v2.3.0)
└── Installer/                        BUILD_INSTALLER.bat + script Klyr_Setup.nsi
```

---

## Liens

- [CHANGELOG.md](CHANGELOG.md) — historique des versions
- [SECURITY.md](SECURITY.md) — politique de signalement de vulnérabilité
- [LICENSE.txt](LICENSE.txt) — conditions d'utilisation

---

<div align="center">

**Klyr** — Édité par **InfoZen · Yahya** — 2026

</div>
