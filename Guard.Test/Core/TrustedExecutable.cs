using Guard.Core.Security;

namespace Guard.Test.Core
{
    public class TrustedExecutableTests
    {
        public static TheoryData<string> PublishedCertificateThumbprints =>
            new()
            {
                "43E9DD4D4A06853CBB521EA35E5F337EDB0DBCCD",
                "0839626A858F4D2E44EDC99708362609E432DA5A",
                "B650886E28A685FD840E3DAD97746369A6A8F709",
            };

        [Theory]
        [MemberData(nameof(PublishedCertificateThumbprints))]
        public void AllowsPublishedCertificateThumbprints(string certificateThumbprint)
        {
            Assert.True(
                TrustedExecutable.IsPublisherCertificateThumbprintAllowed(certificateThumbprint)
            );
        }

        [Fact]
        public void RejectsCertificateThumbprintThatDiffersByOneCharacter()
        {
            Assert.False(
                TrustedExecutable.IsPublisherCertificateThumbprintAllowed(
                    "53E9DD4D4A06853CBB521EA35E5F337EDB0DBCCD"
                )
            );
        }

        [Fact]
        public void RejectsMissingCertificateThumbprint()
        {
            Assert.False(TrustedExecutable.IsPublisherCertificateThumbprintAllowed(null));
        }
    }
}
