using UnityEngine;
using Cyverse.Settings;

namespace Cyverse.Level
{
    /// <summary>
    /// One-shot particle bursts for celebratory moments (station reviewed,
    /// scan passed, level complete). Fully code-driven; the emitter destroys
    /// itself when done. Under Reduce Motion the burst is smaller and slower
    /// rather than removed — feedback stays, spectacle shrinks.
    /// </summary>
    public static class BurstFX
    {
        public static void Spawn(Vector3 position, Color color, int count = 30,
            float speed = 2.6f, float life = 0.9f)
        {
            Shader glowShader = Shader.Find("Cyverse/GlowSprite");
            if (glowShader == null) return;

            if (AccessibilitySettings.ReduceMotion)
            {
                count = Mathf.Max(4, count / 4);
                speed *= 0.4f;
            }

            var go = new GameObject("BurstFX");
            go.transform.position = position;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.5f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.35f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.10f);
            main.startColor = color;
            main.maxParticles = count;
            main.gravityModifier = 0.25f;

            var emission = ps.emission;
            emission.enabled = false; // burst only, via Emit below

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;

            var psRenderer = go.GetComponent<ParticleSystemRenderer>();
            var mat = new Material(glowShader);
            mat.SetColor("_Color", Color.white);
            mat.SetFloat("_Intensity", 1.4f);
            psRenderer.material = mat;

            ps.Emit(count);
            Object.Destroy(go, life + 0.5f);
        }
    }
}
