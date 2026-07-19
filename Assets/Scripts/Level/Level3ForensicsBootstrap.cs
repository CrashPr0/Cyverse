using UnityEngine;

namespace Cyverse.Level
{
    /// <summary>Runtime entry point for Level 3 — Digital Forensics: builds
    /// the whole level in Awake (same pattern as every other bootstrap).</summary>
    public class Level3ForensicsBootstrap : MonoBehaviour
    {
        void Awake()
        {
            Level3ForensicsSceneFactory.BuildAll();
        }
    }
}
