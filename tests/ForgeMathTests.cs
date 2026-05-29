using NUnit.Framework;

namespace HoardersForge.Tests
{
    [TestFixture]
    public class ForgeMathTests
    {
        [Test]
        [TestCase(42, 100.0)]
        [TestCase(80, 200.0)]
        [TestCase(1, 100.0)]
        [TestCase(0, 0.0)]
        [TestCase(-5, 0.0)]
        public void CalculateBaseUnits_VanillaMode(int voxelCount, double expectedUnits)
        {
            double result = ForgeMath.CalculateBaseUnits(voxelCount, hasSmithingPlus: false);
            Assert.That(result, Is.EqualTo(expectedUnits));
        }

        [Test]
        [TestCase(24, 55.0)]  // 24 * (100/42) = 57.14 -> 55.0
        [TestCase(22, 50.0)]  // 22 * (100/42) = 52.38 -> 50.0
        [TestCase(9, 20.0)]   // 9 * (100/42) = 21.42 -> 20.0
        [TestCase(3, 5.0)]    // 3 * (100/42) = 7.14 -> 5.0 (min guaranteed)
        [TestCase(1, 5.0)]    // 1 * (100/42) = 2.38 -> 5.0 (min guaranteed)
        [TestCase(0, 0.0)]
        [TestCase(-10, 0.0)]
        public void CalculateBaseUnits_SmithingPlusMode(int voxelCount, double expectedUnits)
        {
            double result = ForgeMath.CalculateBaseUnits(voxelCount, hasSmithingPlus: true);
            Assert.That(result, Is.EqualTo(expectedUnits));
        }

        [Test]
        [TestCase(100.0, 0.5, 0.0, 50.0)]    // 50% of 100 with 0% loss = 50.0
        [TestCase(100.0, 0.5, 5.0, 45.0)]    // 50% of 100 with 5% loss = 47.5 -> 45.0
        [TestCase(55.0, 0.5, 5.0, 25.0)]     // 50% of 55 = 27.5, with 5% loss = 26.125 -> 25.0
        [TestCase(100.0, 0.12, 5.0, 10.0)]   // 12% of 100 = 12.0, with 5% loss = 11.4 -> 10.0
        [TestCase(100.0, 1.5, 5.0, 95.0)]    // Clamped durability ratio > 1.0 -> 100.0, with 5% loss = 95.0
        [TestCase(100.0, -0.2, 5.0, 0.0)]    // Clamped durability ratio < 0.0 -> 0.0
        [TestCase(5.0, 1.0, 5.0, 5.0)]       // Pristine arrowhead (5.0): lossy = 4.75 -> rounded = 0. But orig >= 5.0 -> returns 5.0
        [TestCase(5.0, 0.8, 5.0, 0.0)]       // Damaged arrowhead (4.0): lossy = 3.8 -> rounded = 0. Orig < 5.0 -> returns 0.0 (prevents material creation)
        public void CalculateDurabilityYield(double baseUnits, double durabilityRatio, double lossPercentage, double expectedYield)
        {
            double result = ForgeMath.CalculateDurabilityYield(baseUnits, durabilityRatio, lossPercentage);
            Assert.That(result, Is.EqualTo(expectedYield));
        }

        [Test]
        [TestCase("pickaxe-copper", "pickaxehead-copper")]
        [TestCase("axe-tinbronze", "axehead-tinbronze")]
        [TestCase("axe-felling-copper", "axehead-copper")]
        [TestCase("game:axe-felling-tinbronze", "game:axehead-tinbronze")]
        [TestCase("knife-copper", "knifeblade-copper")]
        [TestCase("saw-copper", "sawblade-copper")]
        [TestCase("prospectingpick-copper", "prospectingpickhead-copper")]
        [TestCase("cleaver-tinbronze", "cleaverhead-tinbronze")]
        [TestCase("sword-blackbronze", "swordblade-blackbronze")]
        [TestCase("chutesection-copper", "chutesection-copper")]
        [TestCase("game:pickaxe-copper", "game:pickaxehead-copper")]
        [TestCase("game:chutesection-copper", "game:chutesection-copper")]
        [TestCase("workitem-copper", "workitem-copper")]
        [TestCase("pickaxehead-copper", "pickaxehead-copper")]
        [TestCase("blade-falx-copper", "bladehead-falx-copper")]
        [TestCase("game:blade-falx-copper", "game:bladehead-falx-copper")]
        public void GetRecipePath_TranslatesCorrectly(string path, string expectedPath)
        {
            string result = ForgeMath.GetRecipePath(path);
            Assert.That(result, Is.EqualTo(expectedPath));
        }

        [Test]
        public void GetRecipePath_HandlesNullAndEmpty()
        {
            Assert.That(ForgeMath.GetRecipePath(""), Is.EqualTo(""));
            Assert.That(ForgeMath.GetRecipePath(null), Is.Null);
        }
    }
}
