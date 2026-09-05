using Chess4D.Core;
using Chess4D.Engine;
using NUnit.Framework;

namespace Chess4D.Core.Tests
{
    /// <summary>Stage 0 gate: the shared-source packages compile and link under .NET.</summary>
    public class Stage0SmokeTests
    {
        [Test]
        public void CoreAndEngineLink()
        {
            Assert.That(EngineInfo.CoreVersion, Is.EqualTo(CoreInfo.Version));
            Assert.That(CoreInfo.MaxDimensions, Is.EqualTo(6));
        }

        [Test]
        public void CoreAssemblyDoesNotReferenceUnity()
        {
            var refs = typeof(CoreInfo).Assembly.GetReferencedAssemblies();
            Assert.That(refs, Has.None.Matches<System.Reflection.AssemblyName>(a => a.Name.StartsWith("UnityEngine")));
        }
    }
}
