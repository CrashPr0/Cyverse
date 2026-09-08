using UnityEngine;

namespace Cyverse.Level
{
    /// <summary>Runtime entry point for Level 4 — the closed-world Cyber
    /// Attack simulation.</summary>
    public sealed class Level4CyberAttackBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            Level4CyberAttackSceneFactory.BuildAll();
        }
    }
}
