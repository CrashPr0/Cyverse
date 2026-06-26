using System.Collections.Generic;
using UnityEngine;
using Cyverse.Audio;
using Cyverse.Dialogue;
using Cyverse.Interaction;

namespace Cyverse.Level
{
    /// <summary>
    /// Configures a station from a serialized topic and wires its completion
    /// feedback. Living on the station GameObject (instead of in the bootstrap)
    /// means hand-built / editor-authored scenes work the same as the runtime
    /// procedural scene: pick a topic in the Inspector and it loads its content.
    /// </summary>
    [RequireComponent(typeof(InteractableStation))]
    public class StationSetup : MonoBehaviour
    {
        public enum Topic { IAM, CIA, NICE }

        public Topic topic = Topic.IAM;
        public string prompt = "Inspect";

        [Tooltip("Enabled when the station is reviewed (the green checkmark).")]
        public GameObject reviewedMark;

        [Tooltip("Dimmed to a calmer level once reviewed.")]
        public Light stationLight;
        public float completedLightIntensity = 1.2f;

        void Start()
        {
            var station = GetComponent<InteractableStation>();
            station.Configure(topic.ToString().ToLower(), prompt, Content());
            station.Completed += OnCompleted;
            if (reviewedMark != null) reviewedMark.SetActive(false);
        }

        private void OnCompleted(InteractableStation station)
        {
            if (reviewedMark != null) reviewedMark.SetActive(true);
            if (Sfx.Instance != null) Sfx.Instance.PlayConfirm();
            if (stationLight != null) stationLight.intensity = completedLightIntensity;
        }

        private List<DialogueLine> Content()
        {
            switch (topic)
            {
                case Topic.CIA: return Level0Content.CIA();
                case Topic.NICE: return Level0Content.Nice();
                default: return Level0Content.IAM();
            }
        }
    }
}
