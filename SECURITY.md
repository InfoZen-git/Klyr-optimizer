# Politique de sécurité

## Versions supportées

Seule la version la plus récente reçoit les correctifs de sécurité.

| Version | Statut | Sécurité |
|---|---|---|
| 2.1.x | Active | Suivie |
| 2.0.x | Obsolète | Non suivie |

## Périmètre

Klyr est un optimiseur Windows qui :
- Lance des commandes système (`netsh`, `sfc`, `DISM`, `powercfg`, etc.)
- Modifie le registre Windows (HKLM + HKCU)
- Édite le fichier `C:\Windows\System32\drivers\etc\hosts`
- Crée des points de restauration système
- Tue des processus utilisateur (avec confirmation)
- Effectue des reset réseau / pare-feu (avec backup ciblé)

Les vulnérabilités potentielles concernent surtout :
- **Élévation de privilèges** : exploitation d'une optimisation pour obtenir des droits admin non sollicités
- **Injection de commandes** : si un input utilisateur peut altérer une commande PowerShell/CMD exécutée
- **Path traversal** : si un chemin de fichier (logs, exports, backups) peut être manipulé
- **Désactivation de protection** : si une optimisation désactive un service de sécurité critique sans avertissement clair
- **Fuite d'information** : si un log/diagnostic exporte des données sensibles non filtrées

## Comment signaler une vulnérabilité

**Ne pas ouvrir d'issue publique** pour une vulnérabilité de sécurité. Préférer un canal privé via **GitHub Security Advisory** :

1. Aller sur l'onglet **Security** du repo
2. Cliquer sur **« Report a vulnerability »**
3. Décrire le problème avec :
   - Version de Klyr affectée
   - OS testé (Windows 10 / 11, build)
   - Étapes pour reproduire
   - Impact estimé (escalade ? exfiltration ? DoS ?)

## Délais de réponse

| Étape | Délai cible |
|---|---|
| Accusé de réception | Sous 48 h ouvrées |
| Confirmation / classification | Sous 7 jours |
| Patch (si critique) | Sous 14 jours |
| Patch (si modéré) | Inclus dans la prochaine release mineure |
| Disclosure publique | Coordonnée avec le rapporteur après patch |

## Crédit

Si tu signales une vulnérabilité valide :
- Crédit dans le CHANGELOG au moment du patch (sauf si tu préfères rester anonyme)
- Mention dans la release notes du patch
- (Pas de bug bounty — Klyr est un projet open-source non monétisé)

## Hors périmètre

Les éléments suivants ne sont pas considérés comme des vulnérabilités :
- Avertissements SmartScreen / Defender sur l'installeur non signé (limitation connue — code signing prévu plus tard)
- Optimisations qui dégradent les performances sur certaines configs (c'est un bug applicatif, pas une faille — ouvrir une [Issue GitHub](../../issues))
- Fenêtres UAC répétées si `RequiresAdmin=true` sur app non élevée (comportement attendu)
- Logs locaux dans `Documents\Klyr\Logs\` accessibles à l'utilisateur (par design, c'est une app locale)

## Audit du code

Klyr est entièrement open-source. Tu peux auditer :
- `Services/SystemService.cs` — toutes les commandes shell exécutées
- `Services/CleaningOptimizations.cs`, `GamingOptimizations.cs`, `OldPcOptimizations.cs`, `NetworkOptimizations.cs` — les 34 optimisations
- `Services/DiagnosticService.cs` — ce que l'export diagnostic zippe (privacy)
- `app.manifest` — politique d'élévation
- `installer/Klyr_Setup.nsi` — actions à l'installation

Aucune obfuscation, aucun appel réseau caché.
