using UnityEngine;

namespace Cyverse.Level
{
    /// <summary>Keeps a floor light pool (see LightingDirector) in step with
    /// its lamp: colour, brightness and on/off.</summary>
    public class LightPool : MonoBehaviour
    {
        private Light lamp;
        private Material pool;
        private float falloff;
        private float lastIntensity = -1f;
        private Color lastColor;

        public void Bind(Light light, Material material, float distanceFalloff)
        {
            lamp = light;
            pool = material;
            falloff = distanceFalloff;
            Sync();
        }

        void LateUpdate() => Sync();

        private void Sync()
        {
            if (lamp == null || pool == null) return;
            float intensity = lamp.enabled && lamp.gameObject.activeInHierarchy ? lamp.intensity : 0f;
            if (Mathf.Approximately(intensity, lastIntensity) && lamp.color == lastColor) return;
            lastIntensity = intensity;
            lastColor = lamp.color;
            Color c = lamp.color;
            c.a = 1f;
            pool.SetColor("_Color", c);
            // Gentle: a lamp of intensity 2 at its best distance tops out
            // around a third of full glow, so pools read as light, not decals.
            pool.SetFloat("_Intensity", Mathf.Clamp(intensity * 0.16f * falloff, 0f, 0.45f));
        }
    }
}
