using UnityEngine;
using Cyverse.Settings;

namespace Cyverse.Level
{
    /// <summary>
    /// A server cabinet's activity lights (see CabinetProps.ServerRack): each
    /// set of LEDs is one batched renderer that flicks on and off on its own
    /// random rhythm, which reads as disk and network traffic. The lights hold
    /// steady under Reduce Motion.
    /// </summary>
    public class RackLights : MonoBehaviour
    {
        public Renderer[] sets = new Renderer[0];

        private float[] next;

        void Update()
        {
            if (AccessibilitySettings.ReduceMotion)
            {
                foreach (Renderer r in sets)
                    if (r != null && !r.enabled) r.enabled = true;
                return;
            }

            if (next == null || next.Length != sets.Length) next = new float[sets.Length];
            float now = Time.time;
            for (int i = 0; i < sets.Length; i++)
            {
                if (sets[i] == null || now < next[i]) continue;
                bool on = !sets[i].enabled;
                sets[i].enabled = on;
                next[i] = now + (on ? Random.Range(0.08f, 0.6f) : Random.Range(0.04f, 0.16f));
            }
        }
    }
}
