using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Cyverse.Tests
{
    /// <summary>
    /// The office network diagram on the Level 4 pinball and in the Network
    /// Attack round is drawn from code (NetworkDiagramArt). These check it
    /// renders at the expected size. With CYVERSE_DIAGRAM_CAPTURE=1 it also
    /// writes the render (2x) to CYVERSE_DIAGRAM_OUTPUT for comparing
    /// against the original export.
    /// </summary>
    public sealed class NetworkDiagramTests
    {
        private static Type DiagramType()
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = asm.GetType("Cyverse.UI.NetworkDiagram");
                if (t != null) return t;
            }
            return null;
        }

        [Test]
        public void GlassCutIsBolderAndCropped()
        {
            Type type = DiagramType();
            Assert.That(type, Is.Not.Null, "Cyverse.UI.NetworkDiagram is missing.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("No graphics device: the diagram renders on a GPU.");
            var tex = (Texture2D)type.GetMethod("RenderGlass").Invoke(null, new object[] { 2 });
            // View 1162 x 540 diagram pixels from (18, -30).
            Assert.That(tex.width, Is.EqualTo(2324));
            Assert.That(tex.height, Is.EqualTo(1080));
            // The access switch (diagram 210, 236) is still blue.
            Color blue = tex.GetPixel((210 - 18) * 2, 1080 - (236 + 30) * 2);
            Assert.That(blue.b - blue.r, Is.GreaterThan(0.3f), "The switch should be drawn in blue.");
            // No grid: a major grid line (diagram x 74.7) in empty LAN space is paper white.
            Color gap = tex.GetPixel(Mathf.RoundToInt((75f - 18f) * 2f), 1080 - (300 + 30) * 2);
            Assert.That(gap.r + gap.g + gap.b, Is.GreaterThan(2.9f), "The glass cut should have no grid.");

            string output = Environment.GetEnvironmentVariable("CYVERSE_DIAGRAM_OUTPUT");
            if (Environment.GetEnvironmentVariable("CYVERSE_DIAGRAM_CAPTURE") == "1" && !string.IsNullOrWhiteSpace(output))
            {
                Directory.CreateDirectory(output);
                File.WriteAllBytes(Path.Combine(output, "network-diagram-glass-2x.png"), tex.EncodeToPNG());
            }
            UnityEngine.Object.Destroy(tex);
        }

        [Test]
        public void RendersAtTwiceTheOriginalSize()
        {
            Type type = DiagramType();
            Assert.That(type, Is.Not.Null, "Cyverse.UI.NetworkDiagram is missing.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("No graphics device: the diagram renders on a GPU.");
            var tex = (Texture2D)type.GetMethod("Render").Invoke(null, new object[] { 2 });
            Assert.That(tex.width, Is.EqualTo(2398));
            // 535 px of the original plus 32 px of paper above it for the
            // remote employee's head, which the original export cut off.
            Assert.That(tex.height, Is.EqualTo(1134));
            // Paper is white; the access switch body (diagram 210, 236) is blue.
            Color paper = tex.GetPixel(4, 4);
            Color blue = tex.GetPixel(210 * 2, 1134 - (236 + 32) * 2);
            Color head = tex.GetPixel(380 * 2, 1134 - (32 - 20) * 2);
            Assert.That(head.b - head.r, Is.GreaterThan(0.3f), "The remote employee should have a head.");
            Assert.That(paper.r, Is.GreaterThan(0.9f), "Paper should be white.");
            Assert.That(blue.b - blue.r, Is.GreaterThan(0.3f), "The switch should be drawn in blue.");

            string output = Environment.GetEnvironmentVariable("CYVERSE_DIAGRAM_OUTPUT");
            if (Environment.GetEnvironmentVariable("CYVERSE_DIAGRAM_CAPTURE") == "1" && !string.IsNullOrWhiteSpace(output))
            {
                Directory.CreateDirectory(output);
                File.WriteAllBytes(Path.Combine(output, "network-diagram-2x.png"), tex.EncodeToPNG());
            }
            UnityEngine.Object.Destroy(tex);
        }
    }
}
