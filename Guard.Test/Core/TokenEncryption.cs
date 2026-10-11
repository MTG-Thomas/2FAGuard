using System.Security.Cryptography;
using System.Text;
using Guard.Core.Models;
using Guard.Core.Security;

namespace Guard.Test.Core
{
    public class TokenEncryption
    {
        [Fact]
        public void LegacyTokenIsUpgradedAndBoundToItsContext()
        {
            EncryptionHelper encryption = CreateEncryptionHelper();
            DBTOTPToken token = CreateLegacyToken(encryption);

            Assert.True(token.UpgradeEncryption(encryption));
            Assert.Equal(DBTOTPToken.CurrentEncryptionVersion, token.EncryptionVersion);
            Assert.Equal("JBSWY3DPEHPK3PXP", token.DecryptSecret(encryption));
            Assert.Equal("alice@example.com", token.DecryptUsername(encryption));
            Assert.Equal("notes", Encoding.UTF8.GetString(token.DecryptNotes(encryption)!));
            Assert.False(token.UpgradeEncryption(encryption));

            AssertSecretRejected(encryption, token, copy => copy.Id++);
            AssertSecretRejected(encryption, token, copy => copy.Issuer = "Other");
            AssertSecretRejected(
                encryption,
                token,
                copy => copy.Algorithm = TOTPAlgorithm.SHA512
            );
            AssertSecretRejected(encryption, token, copy => copy.Digits = 8);
            AssertSecretRejected(encryption, token, copy => copy.Period = 60);
        }

        [Fact]
        public void EncryptedFieldsCannotBeTransplanted()
        {
            EncryptionHelper encryption = CreateEncryptionHelper();
            DBTOTPToken source = CreateLegacyToken(encryption);
            source.UpgradeEncryption(encryption);

            DBTOTPToken other = CreateLegacyToken(encryption);
            other.Id = source.Id + 1;
            other.UpgradeEncryption(encryption);
            other.EncryptedSecret = source.EncryptedSecret;

            Assert.Throws<CryptographicException>(() => other.DecryptSecret(encryption));

            DBTOTPToken wrongField = Clone(source);
            wrongField.EncryptedUsername = source.EncryptedSecret;
            Assert.Throws<CryptographicException>(() => wrongField.DecryptUsername(encryption));
        }

        private static void AssertSecretRejected(
            EncryptionHelper encryption,
            DBTOTPToken source,
            Action<DBTOTPToken> tamper
        )
        {
            DBTOTPToken copy = Clone(source);
            tamper(copy);
            Assert.Throws<CryptographicException>(() => copy.DecryptSecret(encryption));
        }

        private static EncryptionHelper CreateEncryptionHelper()
        {
            return new EncryptionHelper(
                Encoding.UTF8.GetBytes("token-encryption-test-password"),
                Encoding.UTF8.GetBytes("0123456789abcdef")
            );
        }

        private static DBTOTPToken CreateLegacyToken(EncryptionHelper encryption)
        {
            return new DBTOTPToken
            {
                Id = 42,
                Issuer = "Example",
                EncryptedSecret = encryption.EncryptStringToBytes("JBSWY3DPEHPK3PXP"),
                EncryptedUsername = encryption.EncryptStringToBytes("alice@example.com"),
                EncryptedNotes = encryption.EncryptStringToBytes("notes"),
                Algorithm = TOTPAlgorithm.SHA256,
                Digits = 6,
                Period = 30,
                CreationTime = new DateTime(2026, 10, 11, 0, 0, 0, DateTimeKind.Utc),
            };
        }

        private static DBTOTPToken Clone(DBTOTPToken token)
        {
            return new DBTOTPToken
            {
                Id = token.Id,
                Issuer = token.Issuer,
                EncryptedSecret = [.. token.EncryptedSecret],
                EncryptedUsername =
                    token.EncryptedUsername == null ? null : [.. token.EncryptedUsername],
                EncryptedNotes = token.EncryptedNotes == null ? null : [.. token.EncryptedNotes],
                Algorithm = token.Algorithm,
                Digits = token.Digits,
                Period = token.Period,
                Icon = token.Icon,
                IconType = token.IconType,
                UpdatedTime = token.UpdatedTime,
                CreationTime = token.CreationTime,
                EncryptionVersion = token.EncryptionVersion,
            };
        }
    }
}
