using Klyr.Models;
using Klyr.Resources;

namespace Klyr.Services
{
    /// <summary>
    /// v2.4.0 — Module Confidentialité.
    /// Durcissement de la vie privée Windows via clés de registre / stratégies.
    /// Toutes les actions sont réversibles (un point de restauration auto est créé
    /// avant exécution car RequiresAdmin=true + AutoRestorePoint) et ne touchent que
    /// des réglages documentés de télémétrie / suivi.
    /// </summary>
    public static class PrivacyOptimizations
    {
        public static List<OptimizationItem> GetOptimizations()
        {
            var items = new List<OptimizationItem>
            {
                new OptimizationItem
                {
                    Id          = "priv_telemetry",
                    Name        = Strings.Optim_priv_telemetry_Name,
                    Description = Strings.Optim_priv_telemetry_Desc,
                    Category    = "Confidentialité",
                    RequiresAdmin = true,
                    Purpose     = OptimizationPurpose.SecurityPrivacy,
                    Action = async ct =>
                    {
                        string script = @"
                            New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' -Force | Out-Null
                            Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' -Name 'AllowTelemetry' -Value 0 -Type DWord -Force
                            Stop-Service  'DiagTrack' -Force -ErrorAction SilentlyContinue
                            Set-Service   'DiagTrack' -StartupType Disabled -ErrorAction SilentlyContinue
                            Stop-Service  'dmwappushservice' -Force -ErrorAction SilentlyContinue
                            Set-Service   'dmwappushservice' -StartupType Disabled -ErrorAction SilentlyContinue
                            Write-Output 'Télémétrie Windows désactivée (DiagTrack + AllowTelemetry=0).'
                        ";
                        return (await SystemService.RunPowerShellAsync(script, asAdmin: true, cancellationToken: ct)).DisplayMessage;
                    }
                },
                new OptimizationItem
                {
                    Id          = "priv_advertising_id",
                    Name        = Strings.Optim_priv_advertising_id_Name,
                    Description = Strings.Optim_priv_advertising_id_Desc,
                    Category    = "Confidentialité",
                    RequiresAdmin = true,
                    Purpose     = OptimizationPurpose.SecurityPrivacy,
                    Action = async ct =>
                    {
                        string script = @"
                            New-Item -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo' -Force | Out-Null
                            Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo' -Name 'Enabled' -Value 0 -Type DWord -Force
                            New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo' -Force | Out-Null
                            Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\AdvertisingInfo' -Name 'DisabledByGroupPolicy' -Value 1 -Type DWord -Force
                            Write-Output 'ID publicitaire désactivé.'
                        ";
                        return (await SystemService.RunPowerShellAsync(script, asAdmin: true, cancellationToken: ct)).DisplayMessage;
                    }
                },
                new OptimizationItem
                {
                    Id          = "priv_activity_history",
                    Name        = Strings.Optim_priv_activity_history_Name,
                    Description = Strings.Optim_priv_activity_history_Desc,
                    Category    = "Confidentialité",
                    RequiresAdmin = true,
                    Purpose     = OptimizationPurpose.SecurityPrivacy,
                    Action = async ct =>
                    {
                        string script = @"
                            New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Force | Out-Null
                            Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Name 'EnableActivityFeed'        -Value 0 -Type DWord -Force
                            Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Name 'PublishUserActivities'     -Value 0 -Type DWord -Force
                            Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System' -Name 'UploadUserActivities'      -Value 0 -Type DWord -Force
                            Write-Output 'Historique d''activité (Timeline) désactivé.'
                        ";
                        return (await SystemService.RunPowerShellAsync(script, asAdmin: true, cancellationToken: ct)).DisplayMessage;
                    }
                },
                new OptimizationItem
                {
                    Id          = "priv_location",
                    Name        = Strings.Optim_priv_location_Name,
                    Description = Strings.Optim_priv_location_Desc,
                    Category    = "Confidentialité",
                    RequiresAdmin = true,
                    Purpose     = OptimizationPurpose.SecurityPrivacy,
                    Action = async ct =>
                    {
                        string script = @"
                            $p = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location'
                            New-Item -Path $p -Force | Out-Null
                            Set-ItemProperty -Path $p -Name 'Value' -Value 'Deny' -Type String -Force
                            Write-Output 'Suivi de localisation désactivé.'
                        ";
                        return (await SystemService.RunPowerShellAsync(script, asAdmin: true, cancellationToken: ct)).DisplayMessage;
                    }
                },
                new OptimizationItem
                {
                    Id          = "priv_tailored",
                    Name        = Strings.Optim_priv_tailored_Name,
                    Description = Strings.Optim_priv_tailored_Desc,
                    Category    = "Confidentialité",
                    RequiresAdmin = true,
                    Purpose     = OptimizationPurpose.SecurityPrivacy,
                    Action = async ct =>
                    {
                        string script = @"
                            $p = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Privacy'
                            New-Item -Path $p -Force | Out-Null
                            Set-ItemProperty -Path $p -Name 'TailoredExperiencesWithDiagnosticDataEnabled' -Value 0 -Type DWord -Force
                            Write-Output 'Expériences personnalisées (publicité ciblée Windows) désactivées.'
                        ";
                        return (await SystemService.RunPowerShellAsync(script, asAdmin: true, cancellationToken: ct)).DisplayMessage;
                    }
                },
                new OptimizationItem
                {
                    Id          = "priv_app_launch_tracking",
                    Name        = Strings.Optim_priv_app_launch_tracking_Name,
                    Description = Strings.Optim_priv_app_launch_tracking_Desc,
                    Category    = "Confidentialité",
                    RequiresAdmin = true,
                    Purpose     = OptimizationPurpose.SecurityPrivacy,
                    Action = async ct =>
                    {
                        string script = @"
                            $p = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced'
                            New-Item -Path $p -Force | Out-Null
                            Set-ItemProperty -Path $p -Name 'Start_TrackProgs' -Value 0 -Type DWord -Force
                            Write-Output 'Suivi de lancement des applications désactivé.'
                        ";
                        return (await SystemService.RunPowerShellAsync(script, asAdmin: true, cancellationToken: ct)).DisplayMessage;
                    }
                },
                new OptimizationItem
                {
                    Id          = "priv_feedback",
                    Name        = Strings.Optim_priv_feedback_Name,
                    Description = Strings.Optim_priv_feedback_Desc,
                    Category    = "Confidentialité",
                    RequiresAdmin = true,
                    Purpose     = OptimizationPurpose.SecurityPrivacy,
                    Action = async ct =>
                    {
                        string script = @"
                            New-Item -Path 'HKCU:\Software\Microsoft\Siuf\Rules' -Force | Out-Null
                            Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Siuf\Rules' -Name 'NumberOfSIUFInPeriod' -Value 0 -Type DWord -Force
                            New-Item -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' -Force | Out-Null
                            Set-ItemProperty -Path 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection' -Name 'DoNotShowFeedbackNotifications' -Value 1 -Type DWord -Force
                            Write-Output 'Demandes de feedback Windows désactivées.'
                        ";
                        return (await SystemService.RunPowerShellAsync(script, asAdmin: true, cancellationToken: ct)).DisplayMessage;
                    }
                },
            };

            OptimizationProfileService.ApplyMetadata(items);
            return items;
        }
    }
}
