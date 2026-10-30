using System.Collections.Generic;
using Cyverse.Level;

namespace Cyverse.Forensics
{
    /// <summary>One message in a witness-phone conversation. Outgoing
    /// messages were sent by the phone's owner (Jamie).</summary>
    public sealed class WitnessMessage
    {
        public readonly string time;    // "07-15 12:10", same format as the evidence
        public readonly bool outgoing;
        public readonly string sender;  // shown above incoming bubbles in a group chat
        public readonly string text;

        public WitnessMessage(string time, bool outgoing, string text, string sender = null)
        {
            this.time = time;
            this.outgoing = outgoing;
            this.text = text;
            this.sender = sender;
        }

        /// <summary>A "You blocked this contact" style line, centred, no bubble.</summary>
        public bool IsSystem => sender == WitnessPhoneContent.System;
    }

    public sealed class WitnessContact
    {
        public readonly string name;
        public readonly string detail;   // number or account, under the name in the header
        public readonly bool group;
        public readonly List<WitnessMessage> messages = new List<WitnessMessage>();

        public WitnessContact(string name, string detail, bool group = false)
        {
            this.name = name;
            this.detail = detail;
            this.group = group;
        }

        public WitnessMessage Last => messages.Count > 0 ? messages[messages.Count - 1] : null;
    }

    /// <summary>
    /// What is on Jamie Park's phone. Jamie works with the seized phone's owner
    /// (they are the jamie.park in the evidence's lunch texts) and shared the
    /// phone by consent. The same fake "CyVerse IT Support" account tried Jamie
    /// first; Jamie checked with the real help desk and reported it, and tried
    /// to warn the owner. Nothing here is needed to finish the level; it backs
    /// up Case 2 from a witness's side. Names and apps are invented.
    /// </summary>
    public static class WitnessPhoneContent
    {
        public const string OwnerName = "Jamie Park";
        public const string System = "\u0001system";

        /// <summary>Contacts, newest conversation first, as a messages app
        /// lists them.</summary>
        public static List<WitnessContact> Build()
        {
            string coworker = DisplayName(ScenarioRoster.Current.payloadEmployee);

            var friend = new WitnessContact(coworker, "Coworker  ·  +1 408 555 0188");
            Add(friend, "07-14 12:15", true, "lunch at the quad? 12:30");
            Add(friend, "07-14 12:16", false, "omw");
            Add(friend, "07-14 12:41", true, "did you get that Spartan Gold text this morning? I got one too lol");
            Add(friend, "07-14 12:42", false, "yeah... I might have clicked it. it wanted me to install an app to claim the prize");
            Add(friend, "07-14 12:43", true, "uhh. you should tell IT");
            Add(friend, "07-14 12:44", false, "it's probably fine");
            Add(friend, "07-15 12:10", true, "quad again? they have tacos today");
            Add(friend, "07-15 12:11", false, "can't. IT Support messaged me on Campus Chat, some audit of our department");
            Add(friend, "07-15 12:12", true, "wait, the same \"IT Support\" that messaged me this morning? that account is fake. the help desk said so");
            Add(friend, "07-15 12:13", false, "they said it's confidential and not to tell anyone");
            Add(friend, "07-15 18:50", true, "you heading out?");
            Add(friend, "07-15 18:52", false, "home for dinner, then back to the office. IT needs some files tonight");
            Add(friend, "07-15 18:53", true, "at NIGHT? please call the help desk first");
            Add(friend, "07-16 00:20", false, "done. sent them everything. going home");
            Add(friend, "07-16 00:21", true, "sent WHAT? call me in the morning");
            Add(friend, "07-16 08:05", true, "I reported that account to the SOC. please talk to them today");

            var helpDesk = new WitnessContact("CyVerse Help Desk", "Short code 30411");
            Add(helpDesk, "07-15 09:40", true, "Got a chat from \"CyVerse IT Support\" asking about an audit. Is that you?");
            Add(helpDesk, "07-15 09:52", false, "No, that isn't us. CyVerse IT never asks for files over chat. Ticket #4471 is open and sent to the SOC.");
            Add(helpDesk, "07-15 09:53", false, "Don't reply to it. If anyone on your team was contacted, ask them to call us.");
            Add(helpDesk, "07-16 08:10", false, "Update on #4471: the SOC has opened an investigation. Thank you for reporting it.");

            var fake = new WitnessContact("CyVerse IT Support", "it.support.cyverse@freechat.example");
            Add(fake, "07-15 09:14", false, "Hi, this is IT. Your phone flagged a security alert yesterday. We're auditing your department, stand by.");
            Add(fake, "07-15 09:24", true, "what's the ticket number? I'll call the help desk to confirm");
            Add(fake, "07-15 09:25", false, "No ticket needed, this is a confidential audit. Please don't involve the help desk.");
            Add(fake, "07-15 09:26", true, "the real help desk always gives a ticket. who is this?");
            Add(fake, "07-15 09:31", false, "URGENT: we need the payroll sheet tonight. Photos of the screen are fine.");
            Add(fake, "07-15 09:33", true, "no. reporting this account.");
            fake.messages.Add(new WitnessMessage("07-15 09:34", false, "You blocked this contact.", System));

            var mom = new WitnessContact("Mom", "+1 408 555 0131");
            Add(mom, "07-13 19:25", false, "Are you coming Sunday? Grandma is making soup.");
            Add(mom, "07-13 19:40", true, "yes! bringing dessert");
            Add(mom, "07-15 21:02", false, "Did you remember to call the dentist?");
            Add(mom, "07-15 21:30", true, "tomorrow, promise");

            var study = new WitnessContact("Sec+ Study Group", "4 members", group: true);
            Add(study, "07-14 20:00", false, "quiz on hashing friday. anyone have notes?", "Riley");
            Add(study, "07-14 20:05", true, "SHA-256: same input, same hash. change one bit and the whole hash changes");
            Add(study, "07-14 20:06", false, "so that's how they prove a copy wasn't changed?", "Avery");
            Add(study, "07-14 20:07", true, "yep, hash the original and the copy and compare");
            Add(study, "07-14 20:09", false, "lifesaver", "Riley");

            var contacts = new List<WitnessContact> { friend, helpDesk, fake, mom, study };
            contacts.Sort((a, b) => string.CompareOrdinal(b.Last.time, a.Last.time));
            return contacts;
        }

        private static void Add(WitnessContact c, string time, bool outgoing, string text, string sender = null) =>
            c.messages.Add(new WitnessMessage(time, outgoing, text, sender));

        /// <summary>"devon.james" → "Devon James".</summary>
        public static string DisplayName(string handle)
        {
            if (string.IsNullOrEmpty(handle)) return "Coworker";
            string[] parts = handle.Split('.');
            for (int i = 0; i < parts.Length; i++)
                if (parts[i].Length > 0)
                    parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
            return string.Join(" ", parts);
        }
    }
}
