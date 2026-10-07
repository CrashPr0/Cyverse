using System;
using UnityEngine;

namespace Cyverse.Level
{
    /// <summary>Structured SOC evidence handed from Cyber Defense to Digital
    /// Forensics. JsonUtility keeps the WebGL save portable and inspectable.</summary>
    [Serializable]
    public class SocEvidenceRecord
    {
        public string alertTitle;
        public string time;
        public string computer;
        public string user;
        public string activity;
        public string verificationResult;
        public string collectedAtUtc;
        public string analystName;
        public string inventoryItem;
    }

    /// <summary>Persistent SOC-to-DF handoff state. The three booleans are
    /// separate on purpose: the Hub gate can explain exactly which required
    /// key item has not been earned yet.</summary>
    public static class SocProgress
    {
        public const string CompromisedComputerKey = "cv_soc_compromised_computer";
        public const string ChainOfCustodyKey = "cv_soc_chain_of_custody";
        public const string PlaybookKey = "cv_soc_playbook_solved";
        public const string EvidenceJsonKey = "cv_soc_evidence_json";
        /// <summary>The seized device is sealed in the SOC evidence locker. Not part of
        /// <see cref="HasAllDfKeys"/>: the Hub gate stays as is, and Level 3 supplies a
        /// training device when the locker was never used.</summary>
        public const string EvidenceLockedKey = "cv_soc_evidence_locked";

        public static bool HasCompromisedComputer => PlayerPrefs.GetInt(CompromisedComputerKey, 0) == 1;
        public static bool HasChainOfCustody => PlayerPrefs.GetInt(ChainOfCustodyKey, 0) == 1;
        public static bool HasSolvedPlaybook => PlayerPrefs.GetInt(PlaybookKey, 0) == 1;
        public static bool HasAllDfKeys => HasCompromisedComputer && HasChainOfCustody && HasSolvedPlaybook;
        public static bool HasEvidenceLocked => PlayerPrefs.GetInt(EvidenceLockedKey, 0) == 1;

        public static void MarkCompromisedComputer()
        {
            PlayerPrefs.SetInt(CompromisedComputerKey, 1);
            PlayerPrefs.Save();
        }

        public static void MarkPlaybookSolved()
        {
            PlayerPrefs.SetInt(PlaybookKey, 1);
            PlayerPrefs.Save();
        }

        public static void MarkEvidenceLocked()
        {
            PlayerPrefs.SetInt(EvidenceLockedKey, 1);
            PlayerPrefs.Save();
        }

        public static void StoreEvidence(SocEvidenceRecord record)
        {
            if (record == null) return;
            PlayerPrefs.SetString(EvidenceJsonKey, JsonUtility.ToJson(record));
            PlayerPrefs.SetInt(CompromisedComputerKey, 1);
            PlayerPrefs.SetInt(ChainOfCustodyKey, 1);
            // Freshly collected evidence has not been through the locker yet.
            PlayerPrefs.SetInt(EvidenceLockedKey, 0);
            PlayerPrefs.Save();
        }

        public static bool TryGetEvidence(out SocEvidenceRecord record)
        {
            record = null;
            string json = PlayerPrefs.GetString(EvidenceJsonKey, "");
            if (string.IsNullOrEmpty(json)) return false;
            try
            {
                record = JsonUtility.FromJson<SocEvidenceRecord>(json);
                return record != null && !string.IsNullOrEmpty(record.computer);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public static string MissingDfKeysText()
        {
            var missing = new System.Collections.Generic.List<string>();
            if (!HasCompromisedComputer) missing.Add("possible compromised computer");
            if (!HasChainOfCustody) missing.Add("chain of custody");
            if (!HasSolvedPlaybook) missing.Add("solved IR playbook");
            return string.Join(", ", missing.ToArray());
        }

    }
}
