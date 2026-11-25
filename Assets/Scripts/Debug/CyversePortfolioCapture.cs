#if UNITY_EDITOR
using System.IO;
using UnityEngine;

namespace Cyverse.Testing
{
    /// <summary>One portfolio screenshot the capture toolbar can jump to.</summary>
    public sealed class PortfolioShot
    {
        public readonly string Id;
        public readonly string Title;
        public readonly string FileName;
        public readonly string SceneName;

        public PortfolioShot(string id, string title, string fileName, string sceneName)
        {
            Id = id;
            Title = title;
            FileName = fileName;
            SceneName = sceneName;
        }
    }

    /// <summary>Shared catalog and output path for the editor capture toolbar.</summary>
    public static class CyversePortfolioCapture
    {
        public const int Width = 1920;
        public const int Height = 1080;
        public const string LogPrefix = "[CAPTURE]";

        public static readonly PortfolioShot[] Shots =
        {
            new PortfolioShot("hub", "Hub", "hub.png", "Hub"),
            new PortfolioShot("level1-briefing", "Level 1 briefing", "level1-briefing.png", "Level1_IAM"),
            new PortfolioShot("level2-soc-alert-queue", "Level 2 SOC alert queue", "level2-soc-alert-queue.png", "Level2_CyberDefense"),
            new PortfolioShot("level2-soc-workstations", "Level 2 SOC workstations", "level2-soc-workstations.png", "Level2_CyberDefense"),
            new PortfolioShot("level2-evidence-locker", "Level 2 evidence locker", "level2-evidence-locker.png", "Level2_CyberDefense"),
            new PortfolioShot("level3-imaging-hash", "Level 3 imaging / hash verification", "level3-imaging-hash.png", "Level3_Forensics"),
            new PortfolioShot("level3-evidence-explorer", "Level 3 Evidence Explorer", "level3-evidence-explorer.png", "Level3_Forensics"),
            new PortfolioShot("level3-knowledge-check", "Level 3 knowledge check", "level3-knowledge-check.png", "Level3_Forensics"),
            new PortfolioShot("level4-spot-the-phish", "Level 4 Spot the Phish", "level4-spot-the-phish.png", "Level4_CyberAttack"),
            new PortfolioShot("level4-phish-frenzy", "Level 4 Phish Frenzy", "level4-phish-frenzy.png", "Level4_CyberAttack"),
            new PortfolioShot("level4-access-grabber", "Level 4 Access Grabber", "level4-access-grabber.png", "Level4_CyberAttack"),
            new PortfolioShot("level4-network-attack", "Level 4 Network Attack", "level4-network-attack.png", "Level4_CyberAttack"),
            new PortfolioShot("level4-certificate", "Level 4 results / certificate", "level4-certificate.png", "Level4_CyberAttack"),
        };

        public static string OutputDirectory
        {
            get
            {
                string overridePath = System.Environment.GetEnvironmentVariable("CYVERSE_CAPTURE_OUTPUT");
                if (!string.IsNullOrEmpty(overridePath)) return overridePath;
                string project = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
                return Path.Combine(project, "Captures", "portfolio");
            }
        }

        public static PortfolioShot Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (PortfolioShot shot in Shots)
                if (shot.Id == id) return shot;
            return null;
        }

        public static string PathFor(PortfolioShot shot)
        {
            return Path.Combine(OutputDirectory, shot.FileName);
        }
    }
}
#endif
