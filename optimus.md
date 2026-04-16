# OPTIMUS — Analyse profonde des optimisations InfoZen

_Date : 05 avril 2026_

## 1) Périmètre et méthode

Ce rapport analyse **toutes les optimisations proposées** par InfoZen (Gaming, Vieux PC, Nettoyage, Réseau), avec :
- effet réel attendu sur performances (
CPU, RAM, disque, latence, FPS, stabilité),
- bottlenecks techniques et limites,
- risques de régression,
- améliorations **créatives** pour obtenir des gains réellement mesurables.

> Important : analyse statique basée sur le code actuel. Les gains “drastiques” dépendent du matériel, des pilotes, des processus actifs, et de la charge réelle.

---

## 2) Résumé exécutif

### Ce qui a un impact réel et fréquent
1. **Plan d’alimentation haute performance** (gaming_highperf) — peut stabiliser frametime sur laptop/CPU limité.
2. **Désactivation Game DVR / captures fond** (gaming_gamedvr) — gains modestes mais réels sur CPU/I/O.
3. **Nettoyage des fichiers temporaires** (oldpc_temp) — récupère espace, réduit certains ralentissements liés au disque saturé.
4. **Réparation système SFC/DISM** (clean_repair) — n’augmente pas les FPS, mais corrige performances dégradées par corruption système.
5. **Défrag HDD uniquement** (oldpc_defrag) — impact réel sur HDD fragmenté, nul/inutile sur SSD.

### Ce qui est souvent surestimé ou contextuel
- “Tweaks registre magiques” (Nagle/TCP ACK, priorité CPU globale, DirectX global string tweaks) : parfois utile dans des cas précis, souvent neutre, parfois négatif.
- “Libération RAM” via EmptyWorkingSet : effet visuel immédiat possible, mais peut augmenter le paging ensuite.
- Blocage télémétrie via `hosts` : impact performance généralement faible, risque de maintenance réseau.

### Top bottlenecks actuels du moteur d’optimisation
1. Absence d’**A/B benchmark automatique** (avant/après) par optimisation.
2. Optimisations “one-size-fits-all” sans profil matériel (HDD vs SSD, Wi-Fi vs Ethernet, RAM faible vs abondante).
3. Pas de scoring de “risque / bénéfice” par machine avant exécution.

---

## 3) Analyse détaillée par optimisation

## 3.1 Module Gaming

### gaming_highperf — Mode Haute Performance
- **Effet réel** : améliore la constance CPU boost, réduit downclock agressif.
- **Impact typique** : +0 à +8% FPS selon laptop/CPU limit, frametime plus stable.
- **Bottleneck** : inefficace si jeu GPU-bound pur ou déjà en plan perf équivalent.
- **Risques** : chauffe, bruit, batterie.
- **Amélioration créative** : bascule dynamique “High Perf uniquement pendant jeu” (détection process) puis retour automatique au plan équilibré.

### gaming_gamedvr — Désactiver Xbox Game DVR
- **Effet réel** : réduit hooks capture et I/O d’enregistrement fond.
- **Impact typique** : +0 à +5% (surtout CPU modestes), micro-stutters parfois réduits.
- **Bottleneck** : nul si Game DVR déjà désactivé.
- **Risques** : perte de fonctionnalités capture.
- **Amélioration créative** : mode “capture-aware” (désactiver seulement si OBS/ShadowPlay absent).

### gaming_fullscreen — Désactiver Fullscreen Optimization
- **Effet réel** : dépend fortement du jeu/API (DX11, DX12, borderless).
- **Impact typique** : de -2% à +4%, surtout sur jeux anciens.
- **Bottleneck** : réglage global pas optimal ; devrait être par-exécutable.
- **Risques** : alt-tab moins fluide, HDR/VRR comportement variable.
- **Amélioration créative** : profil par jeu (hash exe) et rollback par titre.

### gaming_priority — Priorité CPU globale (Win32PrioritySeparation)
- **Effet réel** : influence scheduling foreground/background mais impact moderne limité.
- **Impact typique** : souvent neutre.
- **Bottleneck** : réglage global risqué pour workloads multitâches.
- **Risques** : dégradation streaming/recording en parallèle.
- **Amélioration créative** : privilégier **Game Mode API** + Process Priority par jeu (non global registre).

### gaming_network_latency — Nagle/ACK tweaks
- **Effet réel** : potentiellement utile pour applis socket spécifiques, pas universel.
- **Impact typique** : ping “statique” peu changé ; jitter parfois mieux/pire.
- **Bottleneck** : réglage applique à toutes interfaces, sans test préalable.
- **Risques** : comportement réseau imprévisible sur certains drivers/FW.
- **Amélioration créative** : test de latence multi-endpoints avant/après + auto-revert si régression.

### gaming_directx — Tweaks DirectX + HAGS
- **Effet réel** : HAGS peut aider certains GPU/drivers, pénaliser d’autres.
- **Impact typique** : -3% à +5% selon couple GPU/driver/jeu.
- **Bottleneck** : activation aveugle sans check version pilote.
- **Risques** : instabilité/stutter selon stack graphique.
- **Amélioration créative** : matrice de compatibilité (vendor+driver) et recommandation conditionnelle.

### gaming_kill_processes — Fermer processus inutiles
- **Effet réel** : libère RAM/CPU immédiat, utile sur machines limitées.
- **Impact typique** : +0 à +10% sur low-end saturé.
- **Bottleneck** : liste statique ; peut fermer outils voulus (Discord, Spotify).
- **Risques** : perte session utilisateur, frustration.
- **Amélioration créative** : score d’impact par process (CPU/RAM/IO en temps réel) + sélection utilisateur avant kill.

### gaming_fps_unlock — “Déblocage FPS”
- **Effet réel** : très dépendant du jeu/pilote ; ce tweak registre n’est pas un unlock universel.
- **Impact typique** : souvent nul.
- **Bottleneck** : simplification excessive.
- **Risques** : faux sentiment de gain.
- **Amélioration créative** : intégrer presets NVIDIA/AMD connus (V-Sync, Reflex/Anti-Lag, frame cap intelligent) via guides contextualisés.

### gaming_gpu_schedule — HAGS
- **Effet réel** : idem gaming_directx (variable).
- **Impact typique** : contextuel.
- **Bottleneck** : doublon fonctionnel avec `gaming_directx`.
- **Risques** : redondance + incohérence UX.
- **Amélioration créative** : fusionner en une optimisation unique “GPU Scheduler (benchmark-guided)”.

---

## 3.2 Module Vieux PC

### oldpc_startup — Réduire le démarrage
- **Effet réel** : améliore boot/login et réactivité post-boot.
- **Impact typique** : significatif si beaucoup d’auto-start.
- **Bottleneck** : cible principalement clés `Run`; ignore tâches planifiées/services tiers.
- **Risques** : suppression brute de démarrage utile.
- **Amélioration créative** : mode “Startup Impact Analyzer” (mesure boot trace + classement impact).

### oldpc_animations — Désactiver animations
- **Effet réel** : UI plus réactive sur iGPU faible/CPU ancien.
- **Impact typique** : ressenti net, FPS jeu peu concerné.
- **Bottleneck** : binaire ON/OFF, pas de granularité.
- **Risques** : UX visuelle dégradée.
- **Amélioration créative** : preset progressif (léger/moyen/agressif) avec preview.

### oldpc_registry — Nettoyage registre (MRU)
- **Effet réel** : quasi nul sur performances globales modernes.
- **Impact typique** : négligeable.
- **Bottleneck** : “registry cleaning” ne traite pas le vrai bottleneck matériel.
- **Risques** : suppression historique utile utilisateur.
- **Amélioration créative** : remplacer par “privacy cleanup” explicite plutôt que “performance”.

### oldpc_ram — EmptyWorkingSet
- **Effet réel** : libère RAM instantanément, mais peut provoquer rechargement mémoire ensuite.
- **Impact typique** : gain court terme, possible stutter ultérieur si paging.
- **Bottleneck** : opération globale sans discrimination des processus critiques.
- **Risques** : latence accrue après action.
- **Amélioration créative** : ciblage intelligent (seulement processus background lourds, exclure foreground + services système sensibles).

### oldpc_temp — Suppression temporaires
- **Effet réel** : vrai gain d’espace ; améliore systèmes proches saturation.
- **Impact typique** : stable sur long terme (moins de pression disque).
- **Bottleneck** : suppression agressive peut échouer sur fichiers verrouillés.
- **Risques** : nettoyage incomplet / bruit erreurs.
- **Amélioration créative** : nettoyage différé au reboot + rapport “top dossiers nettoyés”.

### oldpc_defrag — Défragmentation HDD
- **Effet réel** : excellent sur HDD fragmentés, surtout accès aléatoires.
- **Impact typique** : forte amélioration I/O HDD.
- **Bottleneck** : action longue, monopolise disque.
- **Risques** : inconfort utilisateur durant process.
- **Amélioration créative** : planification automatique hors heures actives + priorité basse I/O.

### oldpc_theme — Thème léger / transparence off
- **Effet réel** : petit gain GPU/UI sur machines faibles.
- **Impact typique** : surtout ressenti interface.
- **Bottleneck** : impact jeu/perf brute limité.
- **Risques** : préférence utilisateur contrariée.
- **Amélioration créative** : mode “auto” (désactive transparence seulement en charge CPU/GPU élevée).

### oldpc_telemetry — Désactiver télémétrie services
- **Effet réel** : faible gain perf direct ; impact surtout privacy/bruit réseau fond.
- **Impact typique** : modeste.
- **Bottleneck** : bénéfice surestimé par rapport à risques compatibilité diagnostics.
- **Risques** : perte de certaines remontées système utiles.
- **Amélioration créative** : proposer niveau (Minimal / Balanced / Aggressive) avec liste claire des compromis.

---

## 3.3 Module Nettoyage

### clean_disk — Disk Cleanup
- **Effet réel** : libère espace, améliore potentiellement système saturé.
- **Impact typique** : bon pour maintenance, indirect sur perf.
- **Bottleneck** : dépend catégories cleanmgr disponibles.
- **Risques** : suppression caches utiles (faible).
- **Amélioration créative** : estimation de gain avant exécution + top catégories.

### clean_recycle — Vider corbeille
- **Effet réel** : stockage uniquement.
- **Impact typique** : nul sur FPS/CPU, utile pour espace.
- **Bottleneck** : aucun.
- **Risques** : suppression définitive.
- **Amélioration créative** : afficher taille corbeille avant confirmation.

### clean_logs — Suppression logs Windows
- **Effet réel** : gain stockage faible à modéré.
- **Impact typique** : quasi nul sur performances.
- **Bottleneck** : détruit historique diagnostic.
- **Risques** : perte forensic/diagnostic.
- **Amélioration créative** : export/compression logs plutôt que purge totale.

### clean_prefetch — Suppression prefetch
- **Effet réel** : souvent contre-productif (Windows reconstruit prefetch).
- **Impact typique** : peut ralentir premiers lancements après nettoyage.
- **Bottleneck** : optimisation datée.
- **Risques** : dégradation temporaire boot/app launch.
- **Amélioration créative** : retirer cette optimisation par défaut, la garder en “avancé dépannage”.

### clean_bloatware — Suppression apps inutiles
- **Effet réel** : réduit bruit fond et stockage ; gain variable.
- **Impact typique** : positif sur machines OEM chargées.
- **Bottleneck** : liste statique, dépend SKU Windows.
- **Risques** : suppression d’apps voulues.
- **Amélioration créative** : inventaire interactif + score usage réel avant suppression.

### clean_repair — SFC + DISM
- **Effet réel** : haute valeur quand OS corrompu, sinon coût élevé pour gain nul.
- **Impact typique** : fiabilité/stabilité plutôt que performance brute.
- **Bottleneck** : très long, I/O intense.
- **Risques** : impression de “freeze” utilisateur.
- **Amélioration créative** : exécuter conditionnellement si détection erreurs CBS/EventLog.

### clean_dns_cache — Flush DNS
- **Effet réel** : utile en dépannage DNS, pas en boost global.
- **Impact typique** : résolution de problèmes ponctuels.
- **Bottleneck** : aucun gain durable.
- **Risques** : aucun majeur.
- **Amélioration créative** : coupler à test résolution DNS avant/après.

### clean_antivirus — Quick scan Defender
- **Effet réel** : sécurité, pas optimisation performance directe.
- **Impact typique** : peut temporairement dégrader perf pendant scan.
- **Bottleneck** : longue durée, charge CPU/disk.
- **Risques** : confusion entre “sécurité” et “accélération”.
- **Amélioration créative** : déplacer vers module “Santé/Sécurité” séparé et planifier hors usage actif.

---

## 3.4 Module Réseau

### net_tcpip — netsh global TCP tuning
- **Effet réel** : dépend OS/version/offload NIC/FAI ; gains non garantis.
- **Impact typique** : faible à modéré dans cas spécifiques.
- **Bottleneck** : paramètres globaux appliqués sans baseline.
- **Risques** : régression throughput/latence selon environnement.
- **Amélioration créative** : profil intelligent par type réseau (fiber/high-latency/mobile hotspot).

### net_reset — Réinitialisation réseau complète
- **Effet réel** : très efficace en réparation stack cassée.
- **Impact typique** : fort en dépannage, nul sinon.
- **Bottleneck** : nécessite reboot, perturbe config personnalisée.
- **Risques** : perte paramètres manuels.
- **Amélioration créative** : “simulate impact” + diff de config avant reset + restore guidé.

### net_dns_fast — DNS Cloudflare/Google
- **Effet réel** : accélère résolution DNS si DNS FAI lent.
- **Impact typique** : gain sur ouverture de sites, pas sur débit brut.
- **Bottleneck** : pas de benchmark DNS préalable.
- **Risques** : politique réseau entreprise/VPN incompatibles.
- **Amélioration créative** : benchmark multi-DNS automatique (latence + fiabilité) puis choix optimal.

### net_ping — ACK/Nagle latency tweaks
- **Effet réel** : variable ; parfois améliore jitter, parfois neutre.
- **Impact typique** : contextuel.
- **Bottleneck** : tuning agressif sans test QoS.
- **Risques** : comportement sous charge moins stable.
- **Amélioration créative** : test bufferbloat (download/upload saturé) et profil adaptatif.

### net_telemetry_block — hosts block télémétrie
- **Effet réel** : gain perf faible ; impact privacy plus marqué.
- **Impact typique** : faible.
- **Bottleneck** : maintenance manuelle hosts, doublons éventuels.
- **Risques** : faux positifs de blocage domaine.
- **Amélioration créative** : utiliser firewall outbound rules nommées plutôt que hosts global.

### net_flush_dns — Flush DNS
- **Effet réel** : dépannage ponctuel.
- **Impact typique** : non durable.
- **Bottleneck** : n’améliore pas ping/throughput.
- **Risques** : minimes.
- **Amélioration créative** : combiner avec renouvellement DHCP conditionnel.

### net_reset_firewall — Reset firewall
- **Effet réel** : utile seulement si règles corrompues/conflictuelles.
- **Impact typique** : dépannage, pas optimisation de débit.
- **Bottleneck** : destructif pour règles custom.
- **Risques** : surface de sécurité temporairement modifiée.
- **Amélioration créative** : mode “audit & prune rules” avant reset total.

### net_speed_test — Test vitesse
- **Effet réel** : mesure, pas optimisation.
- **Impact typique** : diagnostic utile.
- **Bottleneck** : test download unique (pas upload, pas jitter, pas perte).
- **Risques** : conclusion trompeuse si CDN variable.
- **Amélioration créative** : suite benchmark réseau complète (latence, jitter, loss, upload, multi-run médiane).

### net_info — Infos réseau
- **Effet réel** : observabilité.
- **Impact typique** : aide au diagnostic.
- **Bottleneck** : pas d’analyse automatique.
- **Risques** : aucun majeur.
- **Amélioration créative** : “health score réseau” avec recommandations ciblées.

---

## 4) Ce qui freine les gains réels aujourd’hui

1. **Pas de boucle mesure -> action -> re-mesure**
   - Sans benchmark avant/après, impossible d’isoler ce qui marche réellement sur une machine donnée.

2. **Optimisations globales et statiques**
   - Beaucoup de tweaks sont appliqués à l’aveugle, sans profil matériel/usage.

3. **Confusion entre maintenance, réparation, sécurité et performance**
   - Certaines actions sont utiles mais ne “boostent” pas directement les performances.

4. **Absence de moteur de rollback transactionnel par optimisation**
   - Sauvegardes existent pour certains cas, mais pas encore un rollback unifié et automatique.

---

## 5) Solutions créatives à fort impact (priorité “drastique”)

## 5.1 Moteur de benchmark scientifique intégré (A/B)
Avant chaque optimisation sensible :
- Capturer baseline 2–5 min :
  - FPS/frametime (si jeu ciblé),
  - CPU package power + fréquence effective,
  - RAM hard faults/s,
  - Disk queue length,
  - réseau: ping/jitter/loss.
- Appliquer optimisation.
- Re-mesurer mêmes indicateurs.
- Conserver uniquement si gain au-dessus d’un seuil statistique (ex: >3%).

**Impact attendu** : élimine les tweaks placebo, maximise les gains réels par machine.

## 5.2 Profils hardware-aware automatiques
Créer des profils :
- `Laptop_Battery`, `Laptop_AC`, `Desktop_Gaming`, `Old_HDD_4GB`, `Workstation_Stream`.
- Chaque profil active un sous-ensemble sûr d’optimisations validées.

**Impact attendu** : gains plus élevés et moins de régressions.

## 5.3 Optimisation orientée frametime (pas seulement FPS)
Pour gaming :
- Mesurer p95/p99 frametime,
- Prioriser réduction des stutters (scheduler, processus fond, I/O contention).

**Impact attendu** : fluidité perçue bien supérieure sans forcément augmenter FPS moyen.

## 5.4 Tuner réseau adaptatif
- Exécuter mini-bench multi-serveurs, avec/sans tweaks,
- Choisir automatiquement config la plus stable (jitter/loss),
- Revert si variation défavorable après 24h.

**Impact attendu** : meilleure qualité de jeu en ligne réelle, pas seulement ping brut.

## 5.5 Mode “Game Session Booster” temporaire
- À l’ouverture du jeu : appliquer tweaks temporaires (power plan, kill soft, priorité process).
- À la fermeture : rollback complet.

**Impact attendu** : gains visibles pendant usage, zéro dette système long terme.

## 5.6 Optimiseur de démarrage basé sur traces ETW
- Tracer boot/login via ETW,
- Identifier top 10 contributeurs de lenteur,
- proposer suppression/délais intelligent par app.

**Impact attendu** : réduction mesurable du temps de démarrage (parfois drastique sur machines encombrées).

## 5.7 Contrôle thermique intelligent
- Surveiller température CPU/GPU + power limit,
- Ajuster automatiquement plan/perf pour éviter thermal throttling.

**Impact attendu** : stabilité performance prolongée, surtout laptops.

---

## 6) Reclassification utile des optimisations (clarté produit)

- **Performance directe** : highperf, kill processes ciblé, startup, defrag HDD (si HDD), animations off.
- **Maintenance / stabilité** : clean_disk, clean_temp, SFC/DISM, network reset.
- **Dépannage ponctuel** : flush DNS, reset firewall, ping tweaks.
- **Sécurité / privacy** : antivirus scan, telemetry block/disable.

Cette séparation évite les attentes irréalistes (“+FPS”) pour des actions qui n’ont pas cet objectif.

---

## 7) Feuille de route recommandée (impact maximal)

## Phase 1 (rapide, 1–2 semaines)
1. Ajouter benchmark avant/après sur 6 optimisations majeures.
2. Ajouter score “Confiance du gain” par optimisation.
3. Masquer par défaut les tweaks à effet incertain (ex: prefetch clean, registry cleaning performance).

## Phase 2 (2–4 semaines)
1. Profils matériels automatiques.
2. Game Session Booster avec rollback automatique.
3. DNS auto-benchmark + sélection optimale.

## Phase 3 (4–8 semaines)
1. ETW boot analyzer.
2. Tuner réseau adaptatif avec auto-revert.
3. Dashboard d’efficacité historique (gains cumulés machine réelle).

---

## 8) Conclusion

InfoZen propose une base solide avec plusieurs actions utiles, mais le potentiel “drastique” dépend d’un changement clé : **passer d’un moteur de tweaks statiques à un moteur d’optimisation piloté par mesures et profils matériels**.

La meilleure stratégie n’est pas “plus de tweaks”, mais **moins de tweaks, mieux sélectionnés, mesurés, réversibles et contextualisés**. C’est ce qui transformera les optimisations en gains de performance réellement observables et reproductibles.
