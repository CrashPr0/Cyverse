using System;
using System.Collections.Generic;
using Cyverse.Level;

namespace Cyverse.Forensics
{
    /// <summary>The artifact categories the Evidence Explorer lists on its
    /// left rail. Order here is the order on screen.</summary>
    public enum ArtifactCategory
    {
        Messages,
        Chats,
        Email,
        WebHistory,
        InstalledApps,
        Network,
        WifiLocation,
        Photos,
    }

    /// <summary>
    /// One parsed record from the seized phone: what a forensic tool shows
    /// after processing an image. Fields are label/value pairs for the detail
    /// pane; <see cref="sourcePath"/> is where on the phone the record came
    /// from, so the player sees that every artifact traces back to a file.
    /// </summary>
    public sealed class PhoneArtifact
    {
        public readonly string id;
        public readonly ArtifactCategory category;
        public readonly string timestamp;
        public readonly string title;
        public readonly string summary;
        public readonly string sourcePath;
        public readonly string[] fieldNames;
        public readonly string[] fieldValues;

        public bool Tagged;

        // What the optional processing passes find. Null/false = nothing.
        /// <summary>Hash matched the known-bad list (only shown when the hash
        /// pass ran).</summary>
        public string KnownBad;
        /// <summary>Words printed in a photo (only readable when the OCR pass ran).</summary>
        public string PhotoText;
        /// <summary>Why AI triage flagged this record. One flag is wrong on
        /// purpose: AI flags are leads, not proof.</summary>
        public string AiFlag;
        /// <summary>How the tool got the record back: parsed from a live
        /// database, or carved from deleted space.</summary>
        public string Recovery = "Parsed";

        public PhoneArtifact(string id, ArtifactCategory category, string timestamp,
            string title, string summary, string sourcePath, params string[] fields)
        {
            if (fields.Length % 2 != 0)
                throw new ArgumentException($"{id}: fields must be label/value pairs");
            this.id = id;
            this.category = category;
            this.timestamp = timestamp;
            this.title = title;
            this.summary = summary;
            this.sourcePath = sourcePath;
            fieldNames = new string[fields.Length / 2];
            fieldValues = new string[fields.Length / 2];
            for (int i = 0; i < fieldNames.Length; i++)
            {
                fieldNames[i] = fields[i * 2];
                fieldValues[i] = fields[i * 2 + 1];
            }
        }

        /// <summary>Case-insensitive keyword match over everything the player
        /// can read on the record, as a forensic tool's keyword filter does.</summary>
        public bool Matches(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword)) return true;
            string k = keyword.Trim();
            if (Contains(timestamp, k) || Contains(title, k) || Contains(summary, k) ||
                Contains(sourcePath, k)) return true;
            foreach (string v in fieldValues)
                if (Contains(v, k)) return true;
            return false;
        }

        private static bool Contains(string s, string k) =>
            s != null && s.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// The processed image of the phone handed over from the SOC: every
    /// artifact the Evidence Explorer can show. One story across both cases:
    /// a prize-scam text leads to a sideloaded app talking to the attacker's
    /// server (case 1); the next day a fake "IT Support" contact on that same
    /// server talks the owner into photographing confidential files after
    /// hours (case 2). The owner comes from the rotating roster. Most records
    /// are ordinary phone life; the player has to pick the evidence out.
    /// </summary>
    public sealed class PhoneImage
    {
        public static readonly ArtifactCategory[] Categories =
            (ArtifactCategory[])Enum.GetValues(typeof(ArtifactCategory));

        public readonly string owner;
        public readonly string ownerEmail;
        public readonly List<PhoneArtifact> artifacts = new List<PhoneArtifact>();

        /// <summary>Terms from the SOC alert that the keyword pass searches for.</summary>
        public static readonly string[] AlertKeywords = { "spartan-rewards", AttackerIp };

        // Set by the Process step at the desk. Examine shows nothing until the
        // phone has been processed, and only the passes that were switched on.
        public bool Processed;
        public bool KeywordPass, HashPass, OcrPass, AiPass;

        public bool IsKeywordHit(PhoneArtifact a)
        {
            if (!KeywordPass) return false;
            foreach (string k in AlertKeywords)
                if (a.Matches(k)) return true;
            return false;
        }

        private PhoneImage(string owner, string ownerEmail)
        {
            this.owner = owner;
            this.ownerEmail = ownerEmail;
        }

        public static string Label(ArtifactCategory category)
        {
            switch (category)
            {
                case ArtifactCategory.Messages: return "TEXT MESSAGES";
                case ArtifactCategory.Chats: return "CHATS";
                case ArtifactCategory.Email: return "EMAIL";
                case ArtifactCategory.WebHistory: return "WEB HISTORY";
                case ArtifactCategory.InstalledApps: return "INSTALLED APPS";
                case ArtifactCategory.Network: return "NETWORK";
                case ArtifactCategory.WifiLocation: return "WI-FI & LOCATION";
                case ArtifactCategory.Photos: return "PHOTOS";
                default: return category.ToString().ToUpperInvariant();
            }
        }

        public PhoneArtifact Find(string id)
        {
            foreach (PhoneArtifact a in artifacts)
                if (a.id == id) return a;
            return null;
        }

        /// <summary>The filter match, plus text read out of photos once the
        /// OCR pass has run.</summary>
        public bool Matches(PhoneArtifact a, string keyword)
        {
            if (a.Matches(keyword)) return true;
            return OcrPass && !string.IsNullOrEmpty(a.PhotoText) &&
                a.PhotoText.IndexOf(keyword.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public int CountIn(ArtifactCategory category, string keyword = null)
        {
            int n = 0;
            foreach (PhoneArtifact a in artifacts)
                if (a.category == category && Matches(a, keyword)) n++;
            return n;
        }

        /// <summary>Artifacts in one category that pass the keyword filter,
        /// oldest first.</summary>
        public List<PhoneArtifact> List(ArtifactCategory category, string keyword = null)
        {
            var list = new List<PhoneArtifact>();
            foreach (PhoneArtifact a in artifacts)
                if (a.category == category && Matches(a, keyword)) list.Add(a);
            list.Sort((x, y) => string.CompareOrdinal(x.timestamp, y.timestamp));
            return list;
        }

        private void Add(PhoneArtifact artifact) => artifacts.Add(artifact);

        // ---- Content ----------------------------------------------------------

        // Illustrative source paths: real phones keep each of these in an app's
        // own database, which is the point the detail pane makes.
        private const string SmsDb = "/data/data/com.android.providers.telephony/databases/mmssms.db";
        private const string MailDb = "/data/data/com.cyverse.mail/databases/mail.db";
        private const string ChromeHistory = "/data/data/com.android.chrome/app_chrome/Default/History";
        private const string Packages = "/data/system/packages.xml";
        private const string DnsLog = "/data/misc/net/dns_cache.log";
        private const string NetStats = "/data/system/netstats/app_traffic.db";
        private const string ChatDb = "/data/data/edu.campus.chat/databases/chat.db";
        private const string WifiStore = "/data/misc/wifi/WifiConfigStore.xml";
        private const string Camera = "/sdcard/DCIM/Camera/";

        public const string PhishSender = "prizes@spartan-rewards.com";
        public const string PhishLink = "http://spartan-rewards.com/claim-now";
        public const string AttackerIp = "45.133.7.22";

        public static PhoneImage Build()
        {
            ScenarioRoster.Profile roster = ScenarioRoster.Current;
            var phone = new PhoneImage(roster.payloadEmployee, roster.payloadEmail);

            // MESSAGES
            phone.Add(new PhoneArtifact("msg_family", ArtifactCategory.Messages, "07-13 19:20",
                "SMS from Mom", "Dinner Sunday? Bring the good bread.", SmsDb,
                "From", "Mom (+1 408 555 0142)", "Direction", "Received", "Read", "Yes"));
            phone.Add(new PhoneArtifact("msg_bank", ArtifactCategory.Messages, "07-14 07:30",
                "SMS from CyBank", "Your CyBank sign-in code is 482911. Never share it.", SmsDb,
                "From", "CyBank (short code 22395)", "Direction", "Received", "Read", "Yes"));
            phone.Add(new PhoneArtifact("msg_lure", ArtifactCategory.Messages, "07-14 08:02",
                "SMS from +1 408 555 0199",
                "SPARTAN REWARDS: You've won a Spartan Gold reward! Claim within 24h: spartan-rewards.com/claim-now",
                SmsDb,
                "From", "+1 408 555 0199 (not in contacts)", "Direction", "Received", "Read", "Yes",
                "Link", PhishLink));
            phone.Add(new PhoneArtifact("msg_patch", ArtifactCategory.Messages, "07-14 09:10",
                "SMS from CyVerse IT", "Reminder: patch window tonight at 22:00. Save your work.", SmsDb,
                "From", "CyVerse IT (short code 30411)", "Direction", "Received", "Read", "Yes"));
            phone.Add(new PhoneArtifact("msg_lunch", ArtifactCategory.Messages, "07-14 12:15",
                "SMS from jamie.park", "lunch at the quad? 12:30", SmsDb,
                "From", "jamie.park (+1 408 555 0170)", "Direction", "Received", "Read", "Yes"));
            phone.Add(new PhoneArtifact("msg_reply", ArtifactCategory.Messages, "07-14 12:16",
                "SMS to jamie.park", "omw", SmsDb,
                "To", "jamie.park (+1 408 555 0170)", "Direction", "Sent"));

            // EMAIL
            phone.Add(new PhoneArtifact("em_allhands", ArtifactCategory.Email, "07-14 07:45",
                "Monday all-hands at 10", "From amber.kelly@cyverse.edu to all@cyverse.edu", MailDb,
                "From", "amber.kelly@cyverse.edu", "To", "all@cyverse.edu", "Link", "-"));
            phone.Add(new PhoneArtifact("em_onboard", ArtifactCategory.Email, "07-14 07:58",
                "Intern onboarding forms", "From taylor.reed@cyverse.edu", MailDb,
                "From", "taylor.reed@cyverse.edu", "To", roster.payloadEmail, "Link", "-"));
            phone.Add(new PhoneArtifact("em_phish", ArtifactCategory.Email, "07-14 08:03",
                "Claim Your Spartan Gold Reward", "From " + PhishSender, MailDb,
                "From", PhishSender, "To", roster.payloadEmail, "Link", PhishLink,
                "Note", "Sender domain is not cyverse.edu"));
            phone.Add(new PhoneArtifact("em_pride", ArtifactCategory.Email, "07-14 08:20",
                "Spartan Pride Week schedule", "From news@cyverse.edu to all@cyverse.edu", MailDb,
                "From", "news@cyverse.edu", "To", "all@cyverse.edu",
                "Link", "https://cyverse.edu/spartan-pride-week"));
            phone.Add(new PhoneArtifact("em_patch", ArtifactCategory.Email, "07-14 08:30",
                "Patch window tonight 22:00", "From sam.ortiz@cyverse.edu to all@cyverse.edu", MailDb,
                "From", "sam.ortiz@cyverse.edu", "To", "all@cyverse.edu", "Link", "-"));

            // WEB HISTORY
            phone.Add(new PhoneArtifact("web_news", ArtifactCategory.WebHistory, "07-14 07:50",
                "news.sjsu.edu", "SJSU campus news", ChromeHistory,
                "URL", "https://news.sjsu.edu", "Visit count", "4"));
            phone.Add(new PhoneArtifact("web_pride", ArtifactCategory.WebHistory, "07-14 08:21",
                "cyverse.edu/spartan-pride-week", "Spartan Pride Week", ChromeHistory,
                "URL", "https://cyverse.edu/spartan-pride-week", "Visit count", "1",
                "Opened from", "CyVerse Mail"));
            phone.Add(new PhoneArtifact("web_claim", ArtifactCategory.WebHistory, "07-14 08:15",
                "spartan-rewards.com/claim-now", "Claim your Spartan Gold", ChromeHistory,
                "URL", PhishLink, "Visit count", "1", "Opened from", "Messages (SMS link)"));
            phone.Add(new PhoneArtifact("web_apk", ArtifactCategory.WebHistory, "07-14 08:15",
                "spartan-rewards.com/get/GoldClaim.apk", "Download: GoldClaim.apk (4.1 MB)", ChromeHistory,
                "URL", "http://spartan-rewards.com/get/GoldClaim.apk", "Visit count", "1",
                "Saved to", "/sdcard/Download/GoldClaim.apk"));
            phone.Add(new PhoneArtifact("web_maps", ArtifactCategory.WebHistory, "07-14 12:20",
                "maps.example.com", "Directions to the campus quad", ChromeHistory,
                "URL", "https://maps.example.com/?q=campus+quad", "Visit count", "2"));

            // INSTALLED APPS
            phone.Add(new PhoneArtifact("app_mail", ArtifactCategory.InstalledApps, "06-01 10:02",
                "CyVerse Mail", "com.cyverse.mail", Packages,
                "Installed from", "App store", "Installed", "06-01 10:02", "Permissions", "Contacts, Network"));
            phone.Add(new PhoneArtifact("app_auth", ArtifactCategory.InstalledApps, "06-01 10:05",
                "Authenticator", "com.cyverse.authenticator", Packages,
                "Installed from", "App store", "Installed", "06-01 10:05", "Permissions", "Camera"));
            phone.Add(new PhoneArtifact("app_chat", ArtifactCategory.InstalledApps, "06-02 09:40",
                "Campus Chat", "edu.campus.chat", Packages,
                "Installed from", "App store", "Installed", "06-02 09:40",
                "Permissions", "Contacts, Microphone, Network"));
            phone.Add(new PhoneArtifact("app_flashlight", ArtifactCategory.InstalledApps, "07-10 21:12",
                "Bright Flashlight", "com.brightlabs.flashlight", Packages,
                "Installed from", "App store", "Installed", "07-10 21:12", "Permissions", "Camera"));
            phone.Add(new PhoneArtifact("app_goldclaim", ArtifactCategory.InstalledApps, "07-14 08:16",
                "Gold Claim", "com.goldclaim.reward", Packages,
                "Installed from", "Browser download (unknown source)", "Installed", "07-14 08:16",
                "Package file", "/sdcard/Download/GoldClaim.apk",
                "Permissions", "Read SMS, Contacts, Storage, Network, Run at startup")
            { KnownBad = "GoldClaim.apk matches the known-bad hash list: SMS-stealing malware" });

            // NETWORK
            phone.Add(new PhoneArtifact("net_mail", ArtifactCategory.Network, "07-14 07:45",
                "CyVerse Mail -> mail.cyverse.edu", "App traffic", NetStats,
                "App", "CyVerse Mail", "Remote", "mail.cyverse.edu (10.20.0.6):443", "Sent", "18 KB"));
            phone.Add(new PhoneArtifact("dns_news", ArtifactCategory.Network, "07-14 07:50",
                "DNS news.sjsu.edu", "Lookup", DnsLog,
                "Domain", "news.sjsu.edu", "Resolved IP", "130.65.255.11"));
            phone.Add(new PhoneArtifact("dns_spartan", ArtifactCategory.Network, "07-14 08:15",
                "DNS spartan-rewards.com", "Lookup", DnsLog,
                "Domain", "spartan-rewards.com", "Resolved IP", AttackerIp));
            phone.Add(new PhoneArtifact("net_goldclaim", ArtifactCategory.Network, "07-14 08:17",
                "Gold Claim -> " + AttackerIp, "App traffic", NetStats,
                "App", "Gold Claim", "Remote", AttackerIp + ":443", "Sent", "2.4 MB",
                "Note", "Sent far more than it received"));
            phone.Add(new PhoneArtifact("dns_goldupdates", ArtifactCategory.Network, "07-14 08:18",
                "DNS gold-updates.net", "Lookup by Gold Claim", DnsLog,
                "Domain", "gold-updates.net", "Resolved IP", AttackerIp, "Requested by", "Gold Claim"));
            phone.Add(new PhoneArtifact("net_chat", ArtifactCategory.Network, "07-14 12:15",
                "Campus Chat -> chat.campus.edu", "App traffic", NetStats,
                "App", "Campus Chat", "Remote", "chat.campus.edu (52.8.14.20):443", "Sent", "6 KB"));
            phone.Add(new PhoneArtifact("dns_maps", ArtifactCategory.Network, "07-14 12:20",
                "DNS maps.example.com", "Lookup", DnsLog,
                "Domain", "maps.example.com", "Resolved IP", "93.184.216.34"));

            // ---- Case 2 (07-15): the fake IT contact and the document photos ----

            // CHATS (Campus Chat)
            phone.Add(new PhoneArtifact("chat_contact", ArtifactCategory.Chats, "07-15 09:12",
                "Chat contact added: CyVerse IT Support", "New contact in Campus Chat", ChatDb,
                "Display name", "CyVerse IT Support", "Account", "it.support.cyverse@freechat.example",
                "In CyVerse directory", "No", "Account created", "07-14 08:20",
                "Last seen from IP", AttackerIp));
            phone.Add(new PhoneArtifact("chat_intro", ArtifactCategory.Chats, "07-15 09:13",
                "Chat from CyVerse IT Support",
                "Hi, this is IT. Your phone flagged a security alert yesterday. We're auditing your department, stand by.",
                ChatDb, "From", "CyVerse IT Support", "Direction", "Received", "App", "Campus Chat")
            { AiFlag = "Claims to be IT from an outside account" });
            phone.Add(new PhoneArtifact("chat_jamie", ArtifactCategory.Chats, "07-15 12:10",
                "Chat from jamie.park", "quad again? they have tacos today", ChatDb,
                "From", "jamie.park", "Direction", "Received", "App", "Campus Chat"));
            phone.Add(new PhoneArtifact("chat_ask", ArtifactCategory.Chats, "07-15 18:30",
                "Chat from CyVerse IT Support",
                "To close the audit we need the payroll sheet, client list, board salaries and the merger draft TONIGHT. Photos of the screen are fine. Keep this confidential.",
                ChatDb, "From", "CyVerse IT Support", "Direction", "Received", "App", "Campus Chat")
            { AiFlag = "Urgent request for confidential files, asks for secrecy" });
            phone.Add(new PhoneArtifact("chat_ok", ArtifactCategory.Chats, "07-15 18:33",
                "Chat to CyVerse IT Support", "ok, I'll go back to the office after dinner", ChatDb,
                "To", "CyVerse IT Support", "Direction", "Sent", "App", "Campus Chat"));
            phone.Add(new PhoneArtifact("chat_sent", ArtifactCategory.Chats, "07-16 00:11",
                "Chat to CyVerse IT Support", "sent all 4. can I go home now?", ChatDb,
                "To", "CyVerse IT Support", "Direction", "Sent", "App", "Campus Chat",
                "Attachments", "IMG_0412.jpg, IMG_0413.jpg, IMG_0414.jpg, IMG_0415.jpg"));
            phone.Add(new PhoneArtifact("chat_delete", ArtifactCategory.Chats, "07-16 00:12",
                "Chat from CyVerse IT Support", "Perfect. Delete this chat. Don't tell anyone.", ChatDb,
                "From", "CyVerse IT Support", "Direction", "Received", "App", "Campus Chat",
                "Note", "Recovered: the owner deleted this thread at 00:14")
            { AiFlag = "Asks the user to delete evidence", Recovery = "Carved from deleted space" });

            // WI-FI & LOCATION
            phone.Add(new PhoneArtifact("wifi_home_am", ArtifactCategory.WifiLocation, "07-15 07:10",
                "Joined HomeNet-5G", "Home network", WifiStore,
                "Network", "HomeNet-5G", "Event", "Connected", "Place", "Home"));
            phone.Add(new PhoneArtifact("wifi_office_am", ArtifactCategory.WifiLocation, "07-15 08:20",
                "Joined CyVerse-Staff", "Office network", WifiStore,
                "Network", "CyVerse-Staff", "Event", "Connected", "Place", "CyVerse office, Bldg 2"));
            phone.Add(new PhoneArtifact("wifi_cafe", ArtifactCategory.WifiLocation, "07-15 12:05",
                "Joined QuadCafe-Guest", "Cafe network", WifiStore,
                "Network", "QuadCafe-Guest", "Event", "Connected", "Place", "Campus quad"));
            phone.Add(new PhoneArtifact("wifi_office_leave", ArtifactCategory.WifiLocation, "07-15 18:40",
                "Left CyVerse-Staff", "Office network", WifiStore,
                "Network", "CyVerse-Staff", "Event", "Disconnected", "Place", "CyVerse office, Bldg 2"));
            phone.Add(new PhoneArtifact("wifi_home_pm", ArtifactCategory.WifiLocation, "07-15 19:05",
                "Joined HomeNet-5G", "Home network", WifiStore,
                "Network", "HomeNet-5G", "Event", "Connected", "Place", "Home"));
            phone.Add(new PhoneArtifact("wifi_office_night", ArtifactCategory.WifiLocation, "07-15 23:40",
                "Joined CyVerse-Staff", "Office network", WifiStore,
                "Network", "CyVerse-Staff", "Event", "Connected", "Place", "CyVerse office, Bldg 2",
                "Note", "Office hours end at 19:00"));

            // PHOTOS
            phone.Add(new PhoneArtifact("img_dog", ArtifactCategory.Photos, "07-14 19:30",
                "IMG_0398.jpg", "A dog asleep on a couch", Camera + "IMG_0398.jpg",
                "Taken", "07-14 19:30", "Location (GPS)", "Home", "Camera", "Rear"));
            phone.Add(new PhoneArtifact("img_lunch", ArtifactCategory.Photos, "07-15 12:20",
                "IMG_0405.jpg", "Tacos on a picnic table", Camera + "IMG_0405.jpg",
                "Taken", "07-15 12:20", "Location (GPS)", "Campus quad", "Camera", "Rear"));
            phone.Add(new PhoneArtifact("img_sunset", ArtifactCategory.Photos, "07-15 20:15",
                "IMG_0410.jpg", "Sunset from a balcony", Camera + "IMG_0410.jpg",
                "Taken", "07-15 20:15", "Location (GPS)", "Home", "Camera", "Rear")
            { AiFlag = "Possible deepfake (61% confidence). Check the metadata before trusting this" });
            phone.Add(new PhoneArtifact("img_payroll", ArtifactCategory.Photos, "07-15 23:52",
                "IMG_0412.jpg", "A monitor showing payroll_2026.xlsx", Camera + "IMG_0412.jpg",
                "Taken", "07-15 23:52", "Location (GPS)", "CyVerse office, Bldg 2", "Camera", "Rear",
                "Shared to", "Campus Chat")
            { PhotoText = "payroll_2026.xlsx  Name | Dept | Salary | Bank account", AiFlag = "Photo of a document on a screen" });
            phone.Add(new PhoneArtifact("img_clients", ArtifactCategory.Photos, "07-15 23:57",
                "IMG_0413.jpg", "A monitor showing clients_2026.xlsx", Camera + "IMG_0413.jpg",
                "Taken", "07-15 23:57", "Location (GPS)", "CyVerse office, Bldg 2", "Camera", "Rear",
                "Shared to", "Campus Chat")
            { PhotoText = "clients_2026.xlsx  Client | Contract value | Contact", AiFlag = "Photo of a document on a screen" });
            phone.Add(new PhoneArtifact("img_salaries", ArtifactCategory.Photos, "07-16 00:03",
                "IMG_0414.jpg", "A monitor showing salaries_board.pdf", Camera + "IMG_0414.jpg",
                "Taken", "07-16 00:03", "Location (GPS)", "CyVerse office, Bldg 2", "Camera", "Rear",
                "Shared to", "Campus Chat")
            { PhotoText = "salaries_board.pdf  CONFIDENTIAL  Board compensation 2026", AiFlag = "Photo of a document on a screen" });
            phone.Add(new PhoneArtifact("img_mna", ArtifactCategory.Photos, "07-16 00:09",
                "IMG_0415.jpg", "A monitor showing mna_draft.docx", Camera + "IMG_0415.jpg",
                "Taken", "07-16 00:09", "Location (GPS)", "CyVerse office, Bldg 2", "Camera", "Rear",
                "Shared to", "Campus Chat")
            { PhotoText = "mna_draft.docx  DRAFT  Merger terms  DO NOT DISTRIBUTE", AiFlag = "Photo of a document on a screen" });

            return phone;
        }
    }
}
