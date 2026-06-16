using Klyr.Models;

namespace Klyr.Services
{
    /// <summary>
    /// v2.3.0 — Calcule un Performance Score 0-100 (plus haut = meilleur).
    /// Inspiré de VoltAir ("average CPU, RAM, storage") mais en "marge disponible" pondérée.
    ///
    /// Composantes (poids renormalisés si un capteur manque) :
    ///   - Marge CPU    (100 - charge CPU)            poids 35
    ///   - Marge RAM    (100 - % RAM utilisée)        poids 35
    ///   - Marge disque (% espace libre sur C:)       poids 30
    ///
    /// NB : la composante thermique CPU a été retirée — la température die AMD/Intel
    /// dépend d'un driver ring0 (WinRing0) souvent bloqué par l'Intégrité mémoire (HVCI)
    /// de Windows 11, ce qui la rend non fiable. Le score ne dépend donc pas d'un capteur
    /// que l'utilisateur ne peut pas voir.
    /// </summary>
    public static class PerformanceScoreService
    {
        public static int Compute(SystemInfoModel info)
        {
            double weightedSum = 0;
            double totalWeight = 0;

            // CPU : marge = 100 - charge
            double cpuHeadroom = 100.0 - Clamp01to100(info.CpuUsage);
            weightedSum += cpuHeadroom * 35;
            totalWeight += 35;

            // RAM : marge = 100 - % utilisée
            double ramHeadroom = 100.0 - Clamp01to100(info.RamPercent);
            weightedSum += ramHeadroom * 35;
            totalWeight += 35;

            // Disque : % libre sur C:
            if (info.DiskTotalGb > 0)
            {
                double diskFreePct = (double)info.DiskFreeGb / info.DiskTotalGb * 100.0;
                weightedSum += Clamp01to100(diskFreePct) * 30;
                totalWeight += 30;
            }

            if (totalWeight <= 0) return 0;

            int score = (int)Math.Round(weightedSum / totalWeight);
            return Math.Clamp(score, 0, 100);
        }

        private static double Clamp01to100(double v) => Math.Clamp(v, 0, 100);
    }
}
