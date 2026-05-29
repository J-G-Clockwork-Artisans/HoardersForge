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
        [TestCase(100.0, 0.5, 50.0)]    // 50% of 100 = 50.0
        [TestCase(55.0, 0.5, 25.0)]     // 50% of 55 = 27.5 -> 25.0 (rounds down to mult of 5)
        [TestCase(100.0, 0.12, 10.0)]   // 12% of 100 = 12.0 -> 10.0
        [TestCase(100.0, 1.5, 100.0)]   // Clamped durability ratio > 1.0 -> 100.0
        [TestCase(100.0, -0.2, 0.0)]    // Clamped durability ratio < 0.0 -> 0.0
        public void CalculateDurabilityYield(double baseUnits, double durabilityRatio, double expectedYield)
        {
            double result = ForgeMath.CalculateDurabilityYield(baseUnits, durabilityRatio);
            Assert.That(result, Is.EqualTo(expectedYield));
        }
    }
}
