using UnityEngine;

namespace Cyverse.Level
{
    /// <summary>Runtime entry point for Level 2 — Cyber Defense.</summary>
    public class Level2Bootstrap : MonoBehaviour
    {
        void Awake()
        {
            Level2SceneFactory.BuildAll();
        }
    }
}
