# InfoZen — Analyse complète du code et des fonctionnalités

_Date d’analyse : 05 avril 2026_

## 1) Résumé exécutif

InfoZen est une application desktop WPF en C# (pattern MVVM) orientée **optimisation système Windows** et **gestion de ressources**.

Le projet est structuré proprement, avec :
- une séparation claire UI / ViewModel / Services,
- un moteur d’optimisations par modules (Gaming, Vieux PC, Nettoyage, Réseau),
- des services transverses solides (logs, gestion d’erreurs, paramètres persistants, vérification admin),
- une UX cohérente et lisible (thème dark, progression, terminal live).

Le cœur du produit est déjà fonctionnel et assez complet pour une v2.x. Les principaux points d’amélioration se situent surtout dans :
- la robustesse des exécutions élevées (UAC / `asAdmin`),
- la cohérence technique de versioning (.NET 10 vs mentions .NET 8),
- la gouvernance de certaines optimisations potentiellement sensibles (réseau/registre/hosts).

---

## 2) Architecture technique

## 2.1 Stack et socle

- **Langage / runtime** : C# / .NET (`net10.0-windows` dans `InfoZen.csproj`)
- **UI** : WPF
- **Pattern** : MVVM (View + `MainViewModel` + `RelayCommand`/`AsyncRelayCommand`)
- **Persistance légère** : JSON dans `%AppData%\InfoZen\settings.json`
- **Interop système** : CMD, PowerShell, Registre, API Win32 (P/Invoke)

## 2.2 Entrée application et cycle de vie

- `App.xaml` démarre sur `Views/SplashScreen.xaml`
- `App.xaml.cs` :
  - enregistre la capture d’erreurs globales (`ErrorHandler.Register()`),
  - charge les settings (`SettingsService.Current`),
  - initialise un log de démarrage,
  - sauvegarde les settings en fermeture.

## 2.3 Flux d’exécution principal

1. Splash screen animé
2. Vérification admin non bloquante (`AdminChecker.CheckAndWarn()`)
3. Ouverture `MainWindow` avec `MainViewModel`
4. Navigation par modules
5. Exécution d’optimisations unitaires ou en lot
6. Logs centralisés + export txt

---

## 3) Cartographie des composants

## 3.1 ViewModel

### `MainViewModel`
Responsabilités principales :
- navigation de pages (`Dashboard`, `Gaming`, `OldPC`, `Cleaning`, `Network`, `Terminal`, `Legal`),
- chargement dynamique de la liste d’optimisations selon module,
- orchestration de l’exécution d’une optimisation:
  - confirmation utilisateur (optionnelle),
  - point de restauration automatique (optionnel),
  - progression visuelle fictive + exécution réelle,
  - log du résultat et état UI,
- exécution “Tout exécuter”
- création manuelle d’un point de restauration,
- rafraîchissement périodique CPU (timer 2s) + infos système.

Points forts : orchestration claire et lisible.

## 3.2 Modèles

### `OptimizationItem`
- Modèle d’action unitaire (id, nom, description, catégorie, flags admin/reboot, action async)
- État runtime UI (`IsRunning`, `Status`, `Progress`)
- Expose propriétés calculées (`ProgressStr`, `ProgressVisible`)

### `SystemInfoModel`
- Snapshot/état temps réel (CPU, RAM, OS, uptime, disque, machine)
- Propriétés formatées pour affichage dashboard/statusbar

## 3.3 Services transverses

### `SystemService`
Service central d’exécution :
- `RunCmdAsync`, `RunPowerShellAsync`
- écriture/suppression registre HKLM/HKCU
- collecte infos système
- création point de restauration
- export fichiers de log

### `LogService`
- singleton thread-safe côté UI (via Dispatcher)
- accumulation texte terminal + collection structurée
- export en `.txt` avec en-tête

### `SettingsService`
- load/save JSON robuste (fallback valeurs par défaut)
- settings fonctionnels bien ciblés UX/sécurité

### `ErrorHandler`
- capture exceptions UI / background / Task non observées
- génération de rapports crash détaillés dans Documents
- message utilisateur avec option de continuer ou quitter

### `AdminChecker`
- détecte droits admin au démarrage
- avertissement non bloquant (bon compromis UX)

### `ProgressHelper`
- simulation réaliste de progression (paliers + easing implicite)
- améliore nettement le feedback utilisateur sur opérations longues

---

## 4) Analyse UI/UX

## 4.1 Fenêtres et navigation

- `SplashScreen` : branding + progression de boot
- `MainWindow` :
  - sidebar modules/outils,
  - dashboard métriques,
  - pages modules,
  - terminal live,
  - statusbar système
- `SettingsWindow` : options sécurité/interface
- `LegalWindow` : RGPD + responsabilités + audit
- `AboutWindow` : versioning / auteur / compatibilité

## 4.2 Design system

- Palette dark unifiée dans `App.xaml`
- Styles globaux réutilisés (`PrimaryButtonStyle`, `SecondaryButtonStyle`, `NavButtonStyle`, `OptimizationCardStyle`, `ToggleStyle`, `TerminalTextStyle`)
- Convertisseurs WPF dédiés (visibilité pages, bool inverse, largeur %)

Conclusion UX : application cohérente, lisible, orientée action, avec bon niveau de feedback.

---

## 5) Inventaire complet des fonctionnalités

## 5.1 Fonctionnalités globales

- Dashboard système (CPU/RAM/Disque/Uptime/OS)
- Terminal de logs en direct
- Exécution optimisation unitaire
- Exécution batch “Tout exécuter”
- Confirmation avant action (option)
- Point de restauration auto (option) + manuel
- Badge Admin et warning reboot
- Export logs `.txt`
- Paramètres persistants JSON
- Gestion globale des erreurs (crash reports)
- Vérification admin au démarrage
- Splash boot + progression

## 5.2 Module Gaming / FPS (9)

1. `gaming_highperf` — plan d’alimentation haute performance
2. `gaming_gamedvr` — désactivation Game DVR
3. `gaming_fullscreen` — tweak Fullscreen Optimization
4. `gaming_priority` — priorité CPU foreground
5. `gaming_network_latency` — Nagle/ACK TCP
6. `gaming_directx` — réglages DirectX + HAGS
7. `gaming_kill_processes` — fermeture processus non essentiels
8. `gaming_fps_unlock` — tweak limitation FPS
9. `gaming_gpu_schedule` — activation HAGS

## 5.3 Module Vieux PC (8)

1. `oldpc_startup` — réduction démarrage
2. `oldpc_animations` — réduction effets visuels
3. `oldpc_registry` — nettoyage MRU registre
4. `oldpc_ram` — libération RAM via `EmptyWorkingSet`
5. `oldpc_temp` — suppression temporaires
6. `oldpc_defrag` — défragmentation HDD conditionnelle
7. `oldpc_theme` — thème léger / transparence off
8. `oldpc_telemetry` — réduction télémétrie

## 5.4 Module Nettoyage (8)

1. `clean_disk` — `cleanmgr` (sagerun)
2. `clean_recycle` — vidage corbeille
3. `clean_logs` — purge journaux Windows
4. `clean_prefetch` — nettoyage prefetch
5. `clean_bloatware` — suppression apps inutiles
6. `clean_repair` — SFC + DISM
7. `clean_dns_cache` — flush DNS
8. `clean_antivirus` — QuickScan Defender (timeout étendu)

## 5.5 Module Réseau (9)

1. `net_tcpip` — tuning netsh TCP
2. `net_reset` — reset stack réseau (reboot)
3. `net_dns_fast` — DNS Cloudflare + Google
4. `net_ping` — réduction latence TCP/ACK
5. `net_telemetry_block` — blocage hosts Microsoft telemetry
6. `net_flush_dns` — flush DNS
7. `net_reset_firewall` — reset firewall
8. `net_speed_test` — test débit HTTP Cloudflare
9. `net_info` — diagnostic interfaces réseau

---

## 6) Analyse qualitative approfondie

## 6.1 Forces du code

- **Architecture lisible** : séparation des responsabilités globalement respectée
- **Expérience utilisateur mature** : feedback constants (status, progression, logs, badges)
- **Résilience** : gestion des erreurs centralisée + rapports crash persistants
- **Opérationnel** : module installateur NSIS complet (setup + uninstall + raccourcis)
- **Paramétrable** : options sensibles exposées clairement aux utilisateurs

## 6.2 Risques et limites techniques

1. **Exécution admin partielle**
   - Plusieurs optimisations `RequiresAdmin=true` appellent des méthodes avec `asAdmin=false`.
   - Le flag `RequiresAdmin` agit surtout en UX (badge/warning) mais pas en garde-fou strict.

2. **Incohérence versioning framework**
   - Projet compilé `net10.0-windows` mais UI/README mentionnent parfois `.NET 8`.
   - Peut créer de la confusion support/build.

3. **Actions à fort impact système**
   - reset firewall/réseau, édition hosts, suppression logs Windows, DISM/SFC.
   - Correct pour un outil d’optimisation, mais nécessite communication claire et rollback robuste.

4. **Progression fictive non annulable**
   - Bonne UX, mais pas de mécanisme de cancellation utilisateur durant action longue.

5. **Dépendance Windows stricte**
   - Logique métier fortement couplée à Windows (attendu), non portable.

## 6.3 Dette technique / points de robustesse

- Pas de couche d’abstraction pour “runner” système (testabilité réduite)
- Peu de validation pré-exécution (ex: vérification service Defender actif avant scan, état réseau, etc. partiellement fait)
- Historique de logs en mémoire uniquement tant qu’app active (hors export manuel)

---

## 7) Sécurité, conformité, RGPD

- Positionnement RGPD explicite dans l’app : local-only, pas de collecte de données
- Les logs restent locaux (`Documents\InfoZen\Logs`)
- Les actions sensibles sont tracées dans le terminal
- Présence d’une fenêtre légale claire (bon point produit)

Point d’attention : certaines actions modifient fortement le système, donc la pédagogie des impacts est essentielle (déjà partiellement couverte par descriptions + warnings).

---

## 8) Build, packaging, distribution

- Script `installer/BUILD_INSTALLER.bat` : publish release + génération installeur NSIS
- Script NSIS :
  - installation en `Program Files`,
  - droits admin (`RequestExecutionLevel admin`),
  - raccourcis Bureau + Start Menu,
  - entrée uninstall registre,
  - check minimum Windows 10.

Packaging globalement propre et professionnel pour distribution grand public.

---

## 9) Incohérences / anomalies observées

1. **Manifest vs README/commentaires**
   - `app.manifest` indique `requestedExecutionLevel` = `asInvoker` (pas élévation auto), alors que beaucoup d’optimisations supposent admin.
   - C’est cohérent avec warning non bloquant, mais peut surprendre côté UX.

2. **Compatibilité annoncée .NET**
   - `InfoZen.csproj` cible `.NET 10`
   - `AboutWindow` affiche `.NET 8 + WPF`
   - README mentionne `.NET 10` (cohérent avec csproj)

3. **Paramètres non pleinement exploités dans UI**
   - `ShowAdminBadge` existe mais les badges semblent affichés directement depuis `RequiresAdmin` sans condition sur setting.

4. **Page `Legal` dans `PageTitle`**
   - `MainViewModel` gère `Legal` dans le switch, mais la navigation ouvre une fenêtre dédiée (`LegalWindow`) et pas une page centralisée.

---

## 10) Conclusion

InfoZen v2.1 présente une base **solide, fonctionnelle et déjà mature** pour un outil d’optimisation Windows :
- bonne structure technique,
- couverture fonctionnelle large (34 optimisations),
- UX claire,
- logging + sécurité opérationnelle correctement pensés.

Les prochains gains de qualité seraient surtout sur la **fiabilité d’exécution admin**, la **cohérence de versioning**, et la **formalisation des garde-fous** autour des optimisations les plus invasives.

Le projet est globalement prêt pour une évolution v2.2/v2.3 orientée robustesse et industrialisation.
