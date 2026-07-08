using System.Collections.Generic;
using Cyverse.Core;
using Cyverse.Dialogue;

namespace Cyverse.Level
{
    /// <summary>
    /// Level 1 narration text: Cyber Defense / SOC Analyst (NICE Protection &amp;
    /// Defense role), per the concept tables — SIEM/EDR tools, AI-driven
    /// detection, and incident response mitigation. Mirrors Level0Content's
    /// shape (data only, no gameplay logic) so writers can edit copy freely.
    /// </summary>
    public static class Level1Content
    {
        public static List<DialogueLine> Intro() => new List<DialogueLine>
        {
            new DialogueLine("SOC Lead",
                "Welcome to the Security Operations Center. You've completed onboarding — now let's put I/AM and the CIA Triad to work defending this network."),
            new DialogueLine("SOC Lead",
                "A SOC Analyst's job is Protection and Defense: watch for threats, understand how they're detected, and know how to respond when something gets through."),
            new DialogueLine("SOC Lead",
                "Walk up to a glowing station and press E to learn each concept. Press G any time for the glossary, Esc for settings."),
        };

        public static List<DialogueLine> Siem() => new List<DialogueLine>
        {
            new DialogueLine("SIEM Console",
                "A SIEM — Security Information and Event Management platform — pulls logs from every system on the network into one place."),
            new DialogueLine("SIEM Console",
                "On its own, a single log entry rarely means much. SIEM correlates entries across systems to reveal patterns a human scanning one log at a time would miss."),
            new DialogueLine("SIEM Console",
                "Not every event is an attack. Analysts triage alerts — separating real threats from noise — before escalating to incident response."),
        };

        public static List<DialogueLine> Edr() => new List<DialogueLine>
        {
            new DialogueLine("EDR Terminal",
                "Endpoint Detection and Response protects individual devices — laptops, servers, workstations — the endpoints where attacks often start."),
            new DialogueLine("EDR Terminal",
                "EDR continuously watches endpoint behavior. If it spots something malicious, it can isolate — contain — that device before the threat spreads."),
            new DialogueLine("EDR Terminal",
                "Modern EDR increasingly relies on AI-driven detection: machine learning models trained to recognize attack patterns faster than manual review, at a scale no human team could match alone."),
        };

        public static List<DialogueLine> Incident() => new List<DialogueLine>
        {
            new DialogueLine("Incident Response Board",
                "When a threat is confirmed, analysts follow the incident response lifecycle: Detect, Contain, Eradicate, Recover."),
            new DialogueLine("Incident Response Board",
                "Mitigation happens throughout — patching the vulnerability, blocking a malicious IP, revoking stolen credentials — anything that reduces harm."),
            new DialogueLine("Incident Response Board",
                "Eradication removes the root cause, not just the symptom. Recovery restores normal operations only once the team is confident the threat is fully gone."),
        };

        public static List<DialogueLine> AllReviewed() => new List<DialogueLine>
        {
            new DialogueLine("SOC Lead",
                "You've covered the core of cyber defense. Head to the Threat Response Console and certify to complete this rotation.", null, 4f),
        };

        public static List<DialogueLine> Complete() => new List<DialogueLine>
        {
            new DialogueLine("System", "Certification confirmed.", null, 2.2f),
            new DialogueLine("System",
                $"Cleared: SOC Analyst, Protection & Defense. Well done, {PlayerIdentity.Callsign}.", null, 4f),
        };
    }
}
