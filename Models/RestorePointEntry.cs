namespace Klyr.Models
{
    /// <summary>
    /// v2.4.0 — Un point de restauration système Windows.
    /// </summary>
    public class RestorePointEntry
    {
        public uint     SequenceNumber { get; set; }
        public string   Description    { get; set; } = "";
        public int      RestorePointType { get; set; }
        public DateTime CreationTime   { get; set; }

        public string DateStr => CreationTime == DateTime.MinValue
            ? "—"
            : CreationTime.ToString("dd/MM/yyyy HH:mm");

        public string TypeStr => RestorePointType switch
        {
            0  => "Installation app",
            1  => "Désinstallation app",
            10 => "Création système",
            12 => "Mise à jour",
            13 => "Restauration annulée",
            _  => "Point manuel"
        };
    }
}
