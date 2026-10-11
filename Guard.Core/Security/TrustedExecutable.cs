using Microsoft.Security.Extensions;

namespace Guard.Core.Security
{
    public class TrustedExecutable
    {
        // Keep this rotation set synchronized with the release certificates in SECURITY.md.
        private static readonly string[] AllowedPublisherCertificateThumbprints =
        [
            "43E9DD4D4A06853CBB521EA35E5F337EDB0DBCCD",
            "0839626A858F4D2E44EDC99708362609E432DA5A",
            "B650886E28A685FD840E3DAD97746369A6A8F709",
        ];

        public static bool IsFileTrusted(string path, bool strict)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            using FileStream fs = File.OpenRead(path);
            FileSignatureInfo sigInfo = FileSignatureInfo.GetFromFileStream(fs);

            if (sigInfo.State != SignatureState.SignedAndTrusted)
            {
                return false;
            }

            if (!strict)
            {
                return true;
            }

            return IsPublisherCertificateThumbprintAllowed(
                sigInfo.SigningCertificate.Thumbprint
            );
        }

        internal static bool IsPublisherCertificateThumbprintAllowed(string? certificateThumbprint)
        {
            return certificateThumbprint != null
                && AllowedPublisherCertificateThumbprints.Contains(
                    certificateThumbprint,
                    StringComparer.OrdinalIgnoreCase
                );
        }
    }
}
