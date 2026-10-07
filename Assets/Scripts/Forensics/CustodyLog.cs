using System;
using Cyverse.Level;

namespace Cyverse.Forensics
{
    /// <summary>
    /// The one chain-of-custody record the seized device carries from the SOC to
    /// the lab, laid out like a real intake form: an item block (item #,
    /// description, serial), then a custody table where every handoff is a row
    /// of Date/Time, Released By, Received By and Reason.
    ///
    /// The SOC panel shows entry 1 (the client releases the device to the SOC
    /// analyst). The lab's form repeats entry 1 and the player writes entry 2
    /// (the SOC analyst releases the same item to Digital Forensics, for
    /// analysis). Both sides read from here so the case, item and names match.
    /// </summary>
    public static class CustodyLog
    {
        public const string ItemNumber = "#1";
        public const string ClientName = "R. Alvarez";
        public const string ClientRole = "Client IT";
        // The SOC analyst is their own person, not the player: entry 2 is a
        // handoff between two people, and two different signatures show it.
        public const string SocAnalystName = "L. Torres";
        public const string SocRole = "SOC Analyst";
        public const string LabRole = "Digital Forensics";
        public const string CollectionReason = "Incident investigation";
        public const string TransferReason = "For analysis";

        public static string Client => ClientName + " / " + ClientRole;
        public static string SocAnalyst => SocAnalystName + " / " + SocRole;
        public static string LabAnalyst => "You / " + LabRole;

        public static string Computer(SocEvidenceRecord record) =>
            record != null && !string.IsNullOrEmpty(record.computer) ? record.computer : "WS-03";

        // The year comes from the collection stamp, not today's date, so the
        // number reads the same in the SOC and in the lab across New Year.
        public static string CaseNumber(SocEvidenceRecord record)
        {
            string stamp = CollectedAt(record);
            string year = stamp.Length >= 4 && int.TryParse(stamp.Substring(0, 4), out _)
                ? stamp.Substring(0, 4) : DateTime.UtcNow.Year.ToString();
            return $"IR-{year}-{Stable(Computer(record)) % 9000 + 1000}";
        }

        public static string ItemDescription(SocEvidenceRecord record) =>
            $"Mobile phone seized at {Computer(record)}";

        public static string Serial(SocEvidenceRecord record)
        {
            uint h = Stable("sn:" + Computer(record));
            return $"SN {h >> 16:X4}-{h & 0xFFFF:X4}";
        }

        public static string CollectedAt(SocEvidenceRecord record) =>
            record != null && !string.IsNullOrEmpty(record.collectedAtUtc)
                ? record.collectedAtUtc : Now();

        public static string Now() => DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'");

        // FNV-1a: string.GetHashCode is not stable across runtimes, and the case
        // number must read the same in the SOC and in the lab.
        private static uint Stable(string s)
        {
            uint h = 2166136261;
            foreach (char c in s) { h ^= c; h *= 16777619; }
            return h;
        }
    }
}
