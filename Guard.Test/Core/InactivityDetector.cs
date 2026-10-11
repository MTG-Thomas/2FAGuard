using Guard.WPF.Core.Security;

namespace Guard.Test.Core
{
    public class InactivityDetectorTests
    {
        [Fact]
        public void CalculatesElapsedTimeBeforeSignedHighBit()
        {
            TimeSpan elapsed = InactivityDetector.GetElapsedInputTime(100_000, 90_000);

            Assert.Equal(TimeSpan.FromSeconds(10), elapsed);
        }

        [Fact]
        public void CalculatesElapsedTimeAcrossSignedHighBit()
        {
            TimeSpan elapsed = InactivityDetector.GetElapsedInputTime(0x80000010, 0x7FFFFF00);

            Assert.Equal(TimeSpan.FromMilliseconds(272), elapsed);
        }

        [Fact]
        public void CalculatesElapsedTimeAcrossTickCounterWrap()
        {
            TimeSpan elapsed = InactivityDetector.GetElapsedInputTime(0x00000020, 0xFFFFFFF0);

            Assert.Equal(TimeSpan.FromMilliseconds(48), elapsed);
        }
    }
}
