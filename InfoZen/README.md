# InfoZen – Optimiseur PC v2.1
> Développé par Octave

## Installation (utilisateur final)

1. Téléchargez **InfoZen_Setup_v2.1.0.exe**
2. Lancez l'installeur (droits admin requis)
3. Suivez l'assistant
4. InfoZen apparaît sur le Bureau et dans le Menu Démarrer

## Compilation (développeurs)

### Prérequis
- Windows 10/11 x64
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Visual Studio 2022 ou VS Code

### Build rapide
```powershell
cd InfoZen
dotnet build -c Release
dotnet run
```

### Créer l'installeur complet
1. Installer [NSIS 3.x](https://nsis.sourceforge.io/Download)
2. Double-cliquer sur `installer\BUILD_INSTALLER.bat`
3. L'installeur `InfoZen_Setup_v2.1.0.exe` est généré à la racine

## Structure du projet

```
InfoZen/
├── InfoZen.csproj
├── app.manifest           ← UAC + DPI awareness
├── App.xaml / App.xaml.cs ← Styles globaux + ErrorHandler
├── LICENSE.txt
│
├── Assets/Icons/
│   └── icon.ico           ← Icône multi-résolution
│
├── Commands/
│   └── RelayCommand.cs    ← ICommand MVVM
│
├── Models/
│   ├── OptimizationItem.cs
│   └── SystemInfoModel.cs
│
├── Services/
│   ├── SystemService.cs        ← PowerShell / CMD / Registre
│   ├── LogService.cs           ← Terminal + export logs
│   ├── SettingsService.cs      ← Paramètres JSON persistants
│   ├── ErrorHandler.cs         ← Gestion crashes
│   ├── AdminChecker.cs         ← Vérification droits admin
│   ├── ProgressHelper.cs       ← Progression animée
│   ├── GamingOptimizations.cs  ← Module Gaming (9)
│   ├── OldPcOptimizations.cs   ← Module Vieux PC (8)
│   ├── CleaningOptimizations.cs ← Module Nettoyage (8)
│   └── NetworkOptimizations.cs ← Module Réseau (9)
│
├── ViewModels/
│   └── MainViewModel.cs
│
├── Views/
│   ├── MainWindow.xaml/cs
│   ├── SplashScreen.xaml/cs
│   ├── SettingsWindow.xaml/cs
│   ├── AboutWindow.xaml/cs
│   └── LegalWindow.xaml/cs
│
└── installer/
    ├── InfoZen_Setup.nsi        ← Script NSIS
    └── BUILD_INSTALLER.bat      ← Build complet en 1 clic
```

## Modules

| Module | Optimisations | Points clés |
|---|---|---|
| 🎮 Gaming / FPS | 9 | Haute perf, Game DVR, DirectX, latence réseau |
| 💻 Vieux PC | 8 | Démarrage, RAM (EmptyWorkingSet), télémétrie |
| 🧹 Nettoyage | 8 | Disk cleanup, bloatware, SFC/DISM, antivirus |
| 🌐 Réseau | 9 | TCP/IP, DNS rapides, ping, test vitesse |

## Changelog

### v2.1.0 (Mars 2026)
- Fix : Libération RAM via API Win32 EmptyWorkingSet (réelle)
- Fix : Antivirus timeout 30s → 10 minutes
- Nouveau : Progression animée sur chaque optimisation
- Nouveau : Mentions légales + RGPD
- Nouveau : Gestion centralisée des erreurs (crash logs)
- Nouveau : Vérification droits admin au démarrage
- Nouveau : Icône .ico multi-résolution
- Design : Palette sombre sobre, sans effets néon
- Build : Script installeur NSIS inclus
