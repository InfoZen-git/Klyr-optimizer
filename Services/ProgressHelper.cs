using Klyr.Models;

namespace Klyr.Services
{
    /// <summary>
    /// Simule une progression visuelle réaliste pendant l'exécution d'une optimisation.
    /// Suggestion communauté : "ajouter un pourcentage fictif qui augmente jusqu'à la fin"
    /// </summary>
    public static class ProgressHelper
    {
        /// <summary>
        /// Lance la progression fictive en parallèle de l'action réelle.
        /// La barre monte progressivement jusqu'à 90%, puis saute à 100% quand l'action est terminée.
        /// </summary>
        public static async Task<string> RunWithProgressAsync(
            OptimizationItem item,
            Func<Task<string>> action,
            int estimatedMs = 2500)
        {
            item.Progress = 0;

            // Lance la progression fictive en arrière-plan
            var progressTask = SimulateProgressAsync(item, estimatedMs);

            // Lance l'action réelle
            string result = await action();

            // Arrête la simulation et complete à 100%
            item.Progress = 100;
            await Task.Delay(200); // Petit délai pour que l'utilisateur voie 100%

            return result;
        }

        private static async Task SimulateProgressAsync(OptimizationItem item, int estimatedMs)
        {
            // Paliers réalistes : monte vite au début, ralentit vers la fin
            var steps = new (int target, int delayMs)[]
            {
                (15,  (int)(estimatedMs * 0.05)),
                (35,  (int)(estimatedMs * 0.10)),
                (55,  (int)(estimatedMs * 0.15)),
                (70,  (int)(estimatedMs * 0.20)),
                (80,  (int)(estimatedMs * 0.20)),
                (87,  (int)(estimatedMs * 0.15)),
                (92,  (int)(estimatedMs * 0.10)),
                (95,  (int)(estimatedMs * 0.05)),
            };

            foreach (var (target, delay) in steps)
            {
                if (!item.IsRunning) break;
                await AnimateToAsync(item, target, delay);
            }

            // Reste bloqué à 95% jusqu'à ce que l'action réelle finisse
            while (item.IsRunning && item.Progress < 95)
                await Task.Delay(100);
        }

        private static async Task AnimateToAsync(OptimizationItem item, int target, int totalMs)
        {
            int start = item.Progress;
            int steps = Math.Max(1, (target - start));
            int delayPerStep = Math.Max(16, totalMs / steps); // min 16ms (~60fps)

            for (int i = start; i < target; i++)
            {
                if (!item.IsRunning) return;
                item.Progress = i;
                await Task.Delay(delayPerStep);
            }
            item.Progress = target;
        }
    }
}
