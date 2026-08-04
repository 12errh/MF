using NUnit.Framework;
using Mate.Core.Models;

namespace Mate.Core.Tests
{
    [TestFixture]
    public class ModelTests
    {
        [Test]
        public void Result_Ok_HasValue()
        {
            var result = Result<string>.Ok("hello");
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("hello", result.Value);
            Assert.IsNull(result.Error);
        }

        [Test]
        public void Result_Fail_HasError()
        {
            var result = Result<string>.Fail("something broke");
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("something broke", result.Error);
        }

        [Test]
        public void Result_Ok_CanBeImplicitlyConverted()
        {
            Result<int> result = 42;
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(42, result.Value);
        }

        [Test]
        public void WindowInfo_RecordEquality()
        {
            var a = new WindowInfo(123, new System.Numerics.Vector2(100, 200), new System.Numerics.Vector2(800, 600), "TestWindow");
            var b = new WindowInfo(123, new System.Numerics.Vector2(100, 200), new System.Numerics.Vector2(800, 600), "TestWindow");
            Assert.AreEqual(a, b);
        }

        [Test]
        public void MonitorInfo_RecordEquality()
        {
            var a = new MonitorInfo(0, "HDMI-1", new Rectangle(0, 0, 1920, 1080));
            var b = new MonitorInfo(0, "HDMI-1", new Rectangle(0, 0, 1920, 1080));
            Assert.AreEqual(a, b);
        }
    }
}