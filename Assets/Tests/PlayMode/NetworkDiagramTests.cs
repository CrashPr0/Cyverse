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
        public void RendersAtTwiceTheOriginalSize()
        {
            Type type = DiagramType();
            Assert.That(type, Is.Not.Null, "Cyverse.UI.NetworkDiagram is missing.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("No graphics device: the diagram renders on a GPU.");
            var tex = (Texture2D)type.GetMethod("Render").Invoke(null, new object[] { 2 });
            Assert.That(tex.width, Is.EqualTo(2398));
            Assert.That(tex.height, Is.EqualTo(1070));
            // Paper is white; the access switch body (diagram 210, 236) is blue.
            Color paper = tex.GetPixel(4, 4);
            Color blue = tex.GetPixel(210 * 2, 1070 - 236 * 2);
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
