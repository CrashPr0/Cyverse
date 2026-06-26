using UnityEngine;
using Cyverse.Core;
using Cyverse.UI;

namespace Cyverse.Level
{
    /// <summary>
    /// Runtime entry point: builds the whole of Level 0 at Play time via
    /// <see cref="SceneFactory"/>, so the shipped Level0.unity scene only needs
    /// this one component. Prefer the editor menu (CyVerse > Build Level 0 Scene)
    /// when you want to hand-tweak a persistent scene instead — in that case the
    /// scene should NOT also contain this object.
    /// </summary>
    public class Level0Bootstrap : MonoBehaviour
    {
        void Awake()
        {
            GameState.Reset();
            Time.timeScale = 1f;
            Shader.SetGlobalFloat("_CyMotion", 1f); // settings override if Reduce Motion is on

            SceneFactory.BuildAll();

            if (ScreenFader.Instance != null) ScreenFader.Instance.FadeFromBlack();
            // Level0Manager (created by SceneFactory) discovers stations and
            // starts the intro from its own Start().
        }
    }
}
