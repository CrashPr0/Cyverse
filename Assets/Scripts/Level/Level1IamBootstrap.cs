using UnityEngine;

namespace Cyverse.Level
{
    /// <summary>Runtime entry point for the Level 1 (I/AM) scene: builds
    /// everything via Level1IamSceneFactory, mirroring the other bootstraps.</summary>
    public class Level1IamBootstrap : MonoBehaviour
    {
        void Awake()
        {
            Level1IamSceneFactory.BuildAll();
        }
    }
}
