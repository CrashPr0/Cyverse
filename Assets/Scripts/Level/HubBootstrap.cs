using UnityEngine;

namespace Cyverse.Level
{
    /// <summary>Runtime entry point for the Hub scene: builds everything via
    /// HubSceneFactory, mirroring the other levels' bootstrap pattern.</summary>
    public class HubBootstrap : MonoBehaviour
    {
        void Awake()
        {
            HubSceneFactory.BuildAll();
        }
    }
}
