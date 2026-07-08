using UnityEngine;

namespace Cyverse.Level
{
    /// <summary>
    /// Runtime entry point: builds the whole of Level 1 at Play time via
    /// <see cref="Level1SceneFactory"/>, so the shipped Level1.unity scene only
    /// needs this one component. Global state resets, the fade-in, and level
    /// flow are handled by Level1Manager (created by the factory), so
    /// hand-built scenes behave identically. Prefer the editor menu
    /// (CyVerse > Build Level 1 Scene) when you want to hand-tweak a
    /// persistent scene — that scene must NOT also contain this object.
    /// </summary>
    public class Level1Bootstrap : MonoBehaviour
    {
        void Awake()
        {
            Level1SceneFactory.BuildAll();
        }
    }
}
