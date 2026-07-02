using UnityEngine;

namespace Cyverse.Level
{
    /// <summary>
    /// Runtime entry point: builds the whole of Level 0 at Play time via
    /// <see cref="SceneFactory"/>, so the shipped Level0.unity scene only needs
    /// this one component. Global state resets, the fade-in, and level flow are
    /// handled by Level0Manager (created by the factory), so hand-built scenes
    /// behave identically. Prefer the editor menu (CyVerse > Build Level 0
    /// Scene) when you want to hand-tweak a persistent scene — that scene must
    /// NOT also contain this object.
    /// </summary>
    public class Level0Bootstrap : MonoBehaviour
    {
        void Awake()
        {
            SceneFactory.BuildAll();
        }
    }
}
