using System.Collections.Generic;
using Cyverse.Dialogue;

namespace Cyverse.Level
{
    /// <summary>
    /// All of Level 0's narration text in one place, lifted from the "CyVerse
    /// Script" doc. Kept as plain data so writers can edit copy without touching
    /// gameplay code, and so it can later be moved into ScriptableObjects or a
    /// localization table. Attach AudioClips here once voiceover is recorded.
    /// </summary>
    public static class Level0Content
    {
        public static List<DialogueLine> Intro() => new List<DialogueLine>
        {
            new DialogueLine("Security Guard",
                "Welcome to CyberVerse! As a new employee, you'll go through the steps of Identification and Access Management."),
            new DialogueLine("Security Guard",
                "Every time you enter the building or log into a device, app, or website, security controls verify who you are and what you're allowed to access. This process is called Identification and Access Management, or I/AM."),
            new DialogueLine("Security Guard",
                "Walk up to a glowing station and press E to learn each concept. Press Esc at any time for accessibility settings."),
        };

        public static List<DialogueLine> IAM() => new List<DialogueLine>
        {
            new DialogueLine("I/AM",
                "Identification is the process of claiming an identity — how you tell a system who you are. A username, employee ID, or email can all serve as identifiers. Your employee ID is Cy95192."),
            new DialogueLine("I/AM",
                "Authentication is proving you are who you claim to be: something you know (a password), something you have (a token or device), or something you are (a fingerprint or face scan)."),
            new DialogueLine("I/AM",
                "Authorization: once your identity is verified, the system decides what you're allowed to do. Being allowed into a system doesn't mean you can access everything inside it."),
            new DialogueLine("I/AM",
                "Accountability: users are responsible for their actions. Activity is logged and audited so actions can be traced to a specific user — a digital trail that supports investigations and compliance."),
        };

        public static List<DialogueLine> CIA() => new List<DialogueLine>
        {
            new DialogueLine("CIA Triad",
                "Confidentiality means only authorized people can access information. If an unauthorized user tries to access personally identifiable information, the system blocks the request instantly."),
            new DialogueLine("CIA Triad",
                "Integrity guards against the improper modification or destruction of information — for example, an attacker trying to alter financial records."),
            new DialogueLine("CIA Triad",
                "Availability ensures information and resources are accessible to authorized users when needed, reliably and without disruption."),
        };

        public static List<DialogueLine> Nice() => new List<DialogueLine>
        {
            new DialogueLine("NICE Roles",
                "The National Initiative for Cybersecurity Education — NICE — defines the workforce roles across cybersecurity."),
            new DialogueLine("NICE Roles",
                "Oversight & Governance provides leadership and manages cybersecurity risk. Design & Development researches and builds secure systems."),
            new DialogueLine("NICE Roles",
                "Implementation & Operation runs and maintains systems. Protection & Defense identifies and analyzes threats. Investigation handles cybercrime and digital evidence."),
        };

        public static List<DialogueLine> AllReviewed() => new List<DialogueLine>
        {
            new DialogueLine("Security Guard",
                "Excellent work — you've reviewed every station. Head to the Security Scanner and authenticate with a face scan to complete your onboarding.", null, 4f),
        };

        public static List<DialogueLine> Complete() => new List<DialogueLine>
        {
            new DialogueLine("System", "Scan successful.", null, 2.2f),
            new DialogueLine("System",
                "Access Granted — Level: Employee. Welcome aboard, Cy95192.", null, 4f),
        };
    }
}
