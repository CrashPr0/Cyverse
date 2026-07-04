namespace Cyverse.Level
{
    /// <summary>One glossary entry.</summary>
    public class GlossaryEntry
    {
        public readonly string Term;
        public readonly string Definition;

        public GlossaryEntry(string term, string definition)
        {
            Term = term;
            Definition = definition;
        }
    }

    /// <summary>
    /// In-game glossary, curated from the CyVerse concepts list (NICCS / NIST
    /// derived definitions, condensed for gameplay). Data only — edit copy here
    /// without touching UI code.
    /// </summary>
    public static class GlossaryContent
    {
        public static readonly GlossaryEntry[] Entries =
        {
            new GlossaryEntry("Access",
                "The ability to communicate with a system, use its resources, or gain knowledge of the information it contains."),
            new GlossaryEntry("Access Control",
                "Granting or denying specific requests to obtain and use information, services, or entry to physical facilities."),
            new GlossaryEntry("Accountability",
                "Users are answerable for their actions. Activity is logged and audited so actions can be traced to a specific person."),
            new GlossaryEntry("Active Attack",
                "An actual assault by an intentional threat source that attempts to alter a system, its resources, its data, or its operations."),
            new GlossaryEntry("Advanced Persistent Threat (APT)",
                "A sophisticated, well-resourced adversary that uses multiple attack vectors and adapts over a long period to achieve its goals."),
            new GlossaryEntry("Adversary",
                "An individual, group, organization, or government that conducts — or intends to conduct — detrimental activities."),
            new GlossaryEntry("Authentication",
                "Proving you are who you claim to be: something you know (password), something you have (token or device), or something you are (biometric)."),
            new GlossaryEntry("Authorization",
                "Once identity is verified, the system decides what you're allowed to do. Being inside a system doesn't mean you can access everything in it."),
            new GlossaryEntry("Availability",
                "Information and resources are accessible to authorized users when needed, reliably and without disruption."),
            new GlossaryEntry("CIA Triad",
                "The three pillars of information security: Confidentiality, Integrity, and Availability."),
            new GlossaryEntry("Confidentiality",
                "Only authorized people can access information. Unauthorized requests for sensitive data are blocked."),
            new GlossaryEntry("I/AM",
                "Identification and Access Management: the controls that verify who you are and determine what you may access — identification, authentication, authorization, and accountability."),
            new GlossaryEntry("Identification",
                "Claiming an identity — how you tell a system who you are. A username, employee ID, or email address can all serve as identifiers."),
            new GlossaryEntry("Integrity",
                "Guarding against improper modification or destruction of information — for example, an attacker altering financial records."),
            new GlossaryEntry("Multi-Factor Authentication",
                "Verifying identity with two or more different kinds of evidence, such as a password plus a code from your phone."),
            new GlossaryEntry("NICE Framework",
                "The National Initiative for Cybersecurity Education's map of cybersecurity work: the roles, knowledge, and skills of the workforce."),
            new GlossaryEntry("NICE: Oversight & Governance",
                "Provides leadership, management, and direction so an organization can manage cybersecurity risk effectively."),
            new GlossaryEntry("NICE: Design & Development",
                "Researches, designs, develops, and tests secure technology systems, including cloud and perimeter networks."),
            new GlossaryEntry("NICE: Implementation & Operation",
                "Implements, administers, configures, and maintains systems for effective, secure performance."),
            new GlossaryEntry("NICE: Protection & Defense",
                "Protects against, identifies, and analyzes risks and threats to technology systems and networks."),
            new GlossaryEntry("NICE: Investigation",
                "Conducts cybersecurity and cybercrime investigations, including collecting and analyzing digital evidence."),
        };
    }
}
