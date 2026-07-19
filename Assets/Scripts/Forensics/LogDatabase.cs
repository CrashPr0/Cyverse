using System;
using System.Collections.Generic;

namespace Cyverse.Forensics
{
    /// <summary>One queryable log table: named columns + string rows.</summary>
    public class LogTable
    {
        public readonly string name;
        public readonly string[] columns;
        public readonly List<string[]> rows = new List<string[]>();

        public LogTable(string name, params string[] columns)
        {
            this.name = name;
            this.columns = columns;
        }

        public void Add(params string[] row)
        {
            if (row.Length != columns.Length)
                throw new ArgumentException($"{name}: row has {row.Length} values, expected {columns.Length}");
            rows.Add(row);
        }

        public int ColumnIndex(string col)
        {
            for (int i = 0; i < columns.Length; i++)
                if (string.Equals(columns[i], col, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }
    }

    /// <summary>
    /// The KC7-style investigation dataset: security logs of a small org hit
    /// by a phishing campaign. Authored (not random) so the investigation's
    /// facts are stable: prizes@spartan-rewards.com phishes 6 employees, 3
    /// click, 1 (the intern's machine, WS-DJAMES) runs the payload
    /// gold_claim.exe, and DNS shows the attacker's IP also hosts a second
    /// campaign domain (gold-updates.net) — the final pivot.
    /// Benign rows are realistic noise so filtering actually matters.
    /// </summary>
    public class LogDatabase
    {
        public readonly List<LogTable> tables = new List<LogTable>();

        public LogTable Find(string name)
        {
            foreach (var t in tables)
                if (string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }

        public static LogDatabase Build()
        {
            var db = new LogDatabase();

            var employees = new LogTable("Employees", "name", "email", "ip_addr", "hostname", "role");
            employees.Add("amber.kelly",  "amber.kelly@cyverse.edu",  "10.10.1.11", "WS-AKELLY", "CEO");
            employees.Add("jordan.cruz",  "jordan.cruz@cyverse.edu",  "10.10.1.12", "WS-JCRUZ",  "Finance Manager");
            employees.Add("taylor.reed",  "taylor.reed@cyverse.edu",  "10.10.1.13", "WS-TREED",  "HR Manager");
            employees.Add("sam.ortiz",    "sam.ortiz@cyverse.edu",    "10.10.1.14", "WS-SORTIZ", "SysAdmin");
            employees.Add("riley.chen",   "riley.chen@cyverse.edu",   "10.10.1.15", "WS-RCHEN",  "SOC Analyst");
            employees.Add("casey.fox",    "casey.fox@cyverse.edu",    "10.10.1.16", "WS-CFOX",   "Marketing");
            employees.Add("devon.james",  "devon.james@cyverse.edu",  "10.10.1.17", "WS-DJAMES", "Intern");
            employees.Add("morgan.lee",   "morgan.lee@cyverse.edu",   "10.10.1.18", "WS-MLEE",   "Engineer");
            employees.Add("alex.kim",     "alex.kim@cyverse.edu",     "10.10.1.19", "WS-AKIM",   "Engineer");
            employees.Add("jamie.park",   "jamie.park@cyverse.edu",   "10.10.1.20", "WS-JPARK",  "Sales");
            employees.Add("drew.patel",   "drew.patel@cyverse.edu",   "10.10.1.21", "WS-DPATEL", "Finance Analyst");
            employees.Add("quinn.baker",  "quinn.baker@cyverse.edu",  "10.10.1.22", "WS-QBAKER", "Receptionist");
            db.tables.Add(employees);

            var email = new LogTable("Email", "timestamp", "sender", "recipient", "subject", "link");
            // The campaign: 6 phishing emails, same sender/subject/link.
            string phishSender = "prizes@spartan-rewards.com";
            string phishSubject = "Claim Your Spartan Gold Reward";
            string phishLink = "http://spartan-rewards.com/claim-now";
            email.Add("07-14 08:02", phishSender, "amber.kelly@cyverse.edu", phishSubject, phishLink);
            email.Add("07-14 08:02", phishSender, "jordan.cruz@cyverse.edu", phishSubject, phishLink);
            email.Add("07-14 08:03", phishSender, "casey.fox@cyverse.edu",   phishSubject, phishLink);
            email.Add("07-14 08:03", phishSender, "devon.james@cyverse.edu", phishSubject, phishLink);
            email.Add("07-14 08:04", phishSender, "jamie.park@cyverse.edu",  phishSubject, phishLink);
            email.Add("07-14 08:04", phishSender, "quinn.baker@cyverse.edu", phishSubject, phishLink);
            // Benign traffic.
            email.Add("07-14 07:45", "amber.kelly@cyverse.edu", "all@cyverse.edu",         "Monday all-hands at 10", "-");
            email.Add("07-14 07:58", "taylor.reed@cyverse.edu", "devon.james@cyverse.edu", "Intern onboarding forms", "-");
            email.Add("07-14 08:11", "jordan.cruz@cyverse.edu", "drew.patel@cyverse.edu",  "Q3 budget review", "-");
            email.Add("07-14 08:30", "sam.ortiz@cyverse.edu",   "all@cyverse.edu",         "Patch window tonight 22:00", "-");
            email.Add("07-14 09:05", "riley.chen@cyverse.edu",  "sam.ortiz@cyverse.edu",   "SIEM alert triage notes", "-");
            email.Add("07-14 09:12", "casey.fox@cyverse.edu",   "jamie.park@cyverse.edu",  "Landing page copy", "-");
            email.Add("07-14 09:40", "morgan.lee@cyverse.edu",  "alex.kim@cyverse.edu",    "Code review: auth module", "-");
            email.Add("07-14 10:02", "jamie.park@cyverse.edu",  "amber.kelly@cyverse.edu", "Pipeline update", "-");
            email.Add("07-14 10:21", "quinn.baker@cyverse.edu", "taylor.reed@cyverse.edu", "Visitor list for Tuesday", "-");
            email.Add("07-14 10:44", "drew.patel@cyverse.edu",  "jordan.cruz@cyverz.edu",  "Expense report attached", "-");
            email.Add("07-14 11:15", "alex.kim@cyverse.edu",    "morgan.lee@cyverse.edu",  "Re: Code review: auth module", "-");
            email.Add("07-14 11:32", "taylor.reed@cyverse.edu", "all@cyverse.edu",         "Benefits enrollment reminder", "-");
            email.Add("07-14 13:05", "newsletter@sjsu.edu",     "amber.kelly@cyverse.edu", "SJSU campus weekly", "-");
            email.Add("07-14 14:20", "sam.ortiz@cyverse.edu",   "riley.chen@cyverse.edu",  "Firewall rule change #4411", "-");
            db.tables.Add(email);

            var web = new LogTable("WebVisits", "timestamp", "src_ip", "url");
            // Three employees clicked the phishing link.
            web.Add("07-14 08:09", "10.10.1.16", "http://spartan-rewards.com/claim-now");
            web.Add("07-14 08:15", "10.10.1.17", "http://spartan-rewards.com/claim-now");
            web.Add("07-14 08:31", "10.10.1.22", "http://spartan-rewards.com/claim-now");
            // Benign browsing noise.
            web.Add("07-14 07:50", "10.10.1.11", "https://portal.cyverse.edu/dashboard");
            web.Add("07-14 08:01", "10.10.1.15", "https://siem.cyverse.edu/alerts");
            web.Add("07-14 08:05", "10.10.1.18", "https://docs.unity3d.com/Manual");
            web.Add("07-14 08:12", "10.10.1.13", "https://hr.cyverse.edu/onboarding");
            web.Add("07-14 08:22", "10.10.1.19", "https://stackoverflow.com/questions");
            web.Add("07-14 08:40", "10.10.1.12", "https://finance.cyverse.edu/q3");
            web.Add("07-14 09:02", "10.10.1.16", "https://mail.cyverse.edu/inbox");
            web.Add("07-14 09:18", "10.10.1.21", "https://finance.cyverse.edu/expenses");
            web.Add("07-14 09:45", "10.10.1.14", "https://patch.cyverse.edu/schedule");
            web.Add("07-14 10:05", "10.10.1.20", "https://crm.cyverse.edu/pipeline");
            web.Add("07-14 10:30", "10.10.1.22", "https://mail.cyverse.edu/inbox");
            web.Add("07-14 11:08", "10.10.1.11", "https://news.sjsu.edu");
            web.Add("07-14 11:50", "10.10.1.17", "https://mail.cyverse.edu/inbox");
            db.tables.Add(web);

            var proc = new LogTable("ProcessEvents", "timestamp", "hostname", "process_name", "parent_process");
            // The payload: only on the intern's machine.
            proc.Add("07-14 08:16", "WS-DJAMES", "gold_claim.exe", "chrome.exe");
            proc.Add("07-14 08:17", "WS-DJAMES", "svchost_helper.exe", "gold_claim.exe");
            // Benign process noise.
            proc.Add("07-14 07:45", "WS-AKELLY", "outlook.exe", "explorer.exe");
            proc.Add("07-14 07:52", "WS-RCHEN",  "chrome.exe",  "explorer.exe");
            proc.Add("07-14 08:00", "WS-SORTIZ", "terminal.exe","explorer.exe");
            proc.Add("07-14 08:05", "WS-MLEE",   "code.exe",    "explorer.exe");
            proc.Add("07-14 08:08", "WS-CFOX",   "chrome.exe",  "explorer.exe");
            proc.Add("07-14 08:14", "WS-DJAMES", "chrome.exe",  "explorer.exe");
            proc.Add("07-14 08:25", "WS-JCRUZ",  "excel.exe",   "explorer.exe");
            proc.Add("07-14 08:30", "WS-QBAKER", "chrome.exe",  "explorer.exe");
            proc.Add("07-14 08:44", "WS-TREED",  "word.exe",    "explorer.exe");
            proc.Add("07-14 09:01", "WS-AKIM",   "code.exe",    "explorer.exe");
            proc.Add("07-14 09:15", "WS-JPARK",  "teams.exe",   "explorer.exe");
            proc.Add("07-14 09:33", "WS-DPATEL", "excel.exe",   "explorer.exe");
            proc.Add("07-14 10:10", "WS-SORTIZ", "python.exe",  "terminal.exe");
            proc.Add("07-14 11:27", "WS-RCHEN",  "wireshark.exe", "explorer.exe");
            db.tables.Add(proc);

            var dns = new LogTable("DnsLookups", "domain", "resolved_ip");
            dns.Add("spartan-rewards.com",  "45.133.7.22");
            dns.Add("gold-updates.net",     "45.133.7.22"); // same attacker IP — the pivot
            dns.Add("portal.cyverse.edu",   "10.20.0.5");
            dns.Add("mail.cyverse.edu",     "10.20.0.6");
            dns.Add("siem.cyverse.edu",     "10.20.0.7");
            dns.Add("sjsu.edu",             "130.65.255.10");
            dns.Add("docs.unity3d.com",     "35.163.24.88");
            dns.Add("stackoverflow.com",    "151.101.1.69");
            dns.Add("news.sjsu.edu",        "130.65.255.11");
            dns.Add("crm.cyverse.edu",      "10.20.0.8");
            db.tables.Add(dns);

            return db;
        }
    }
}
