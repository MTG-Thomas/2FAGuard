using System.Security.Cryptography;
using System.Text;
using Guard.Core.Security;

namespace Guard.Core.Models
{
    public enum TOTPAlgorithm
    {
        SHA1,
        SHA256,
        SHA512,
    }

    public class DBTOTPToken
    {
        public const int CurrentEncryptionVersion = 2;

        public required int Id { get; set; }
        public required string Issuer { get; set; }
        public byte[]? EncryptedUsername { get; set; }
        public required byte[] EncryptedSecret { get; set; }
        public TOTPAlgorithm? Algorithm { get; set; }
        public int? Digits { get; set; }
        public int? Period { get; set; }
        public string? Icon { get; set; }
        public IconType? IconType { get; set; }
        public byte[]? EncryptedNotes { get; set; }
        public DateTime? UpdatedTime { get; set; }
        public required DateTime CreationTime { get; set; }
        public int? EncryptionVersion { get; set; }

        public string DecryptSecret(EncryptionHelper encryption)
        {
            return encryption.DecryptBytesToString(
                EncryptedSecret,
                GetAssociatedData(EncryptedField.Secret)
            );
        }

        public string? DecryptUsername(EncryptionHelper encryption)
        {
            return EncryptedUsername == null
                ? null
                : encryption.DecryptBytesToString(
                    EncryptedUsername,
                    GetAssociatedData(EncryptedField.Username)
                );
        }

        public byte[]? DecryptNotes(EncryptionHelper encryption)
        {
            return EncryptedNotes == null
                ? null
                : encryption.DecryptBytes(
                    EncryptedNotes,
                    GetAssociatedData(EncryptedField.Notes)
                );
        }

        public bool UpgradeEncryption(EncryptionHelper encryption)
        {
            if (EncryptionVersion == CurrentEncryptionVersion)
            {
                return false;
            }
            if (EncryptionVersion != null)
            {
                throw new CryptographicException(
                    $"Unsupported token encryption version {EncryptionVersion}"
                );
            }

            byte[] secret = encryption.DecryptBytes(EncryptedSecret);
            byte[]? username =
                EncryptedUsername == null ? null : encryption.DecryptBytes(EncryptedUsername);
            byte[]? notes = EncryptedNotes == null ? null : encryption.DecryptBytes(EncryptedNotes);

            byte[] encryptedSecret = encryption.EncryptBytes(
                secret,
                BuildAssociatedData(EncryptedField.Secret)
            );
            byte[]? encryptedUsername =
                username == null
                    ? null
                    : encryption.EncryptBytes(
                        username,
                        BuildAssociatedData(EncryptedField.Username)
                    );
            byte[]? encryptedNotes =
                notes == null
                    ? null
                    : encryption.EncryptBytes(notes, BuildAssociatedData(EncryptedField.Notes));

            EncryptedSecret = encryptedSecret;
            EncryptedUsername = encryptedUsername;
            EncryptedNotes = encryptedNotes;
            EncryptionVersion = CurrentEncryptionVersion;
            return true;
        }

        private byte[]? GetAssociatedData(EncryptedField field)
        {
            return EncryptionVersion switch
            {
                null => null,
                CurrentEncryptionVersion => BuildAssociatedData(field),
                _ => throw new CryptographicException(
                    $"Unsupported token encryption version {EncryptionVersion}"
                ),
            };
        }

        private byte[] BuildAssociatedData(EncryptedField field)
        {
            using MemoryStream stream = new();
            using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true);
            writer.Write("2FAGuard/token-field");
            writer.Write(CurrentEncryptionVersion);
            writer.Write((byte)field);
            writer.Write(Id);
            writer.Write(Issuer);
            WriteNullable(writer, Algorithm == null ? null : (int)Algorithm.Value);
            WriteNullable(writer, Digits);
            WriteNullable(writer, Period);
            writer.Flush();
            return stream.ToArray();
        }

        private static void WriteNullable(BinaryWriter writer, int? value)
        {
            writer.Write(value.HasValue);
            if (value.HasValue)
            {
                writer.Write(value.Value);
            }
        }

        private enum EncryptedField : byte
        {
            Secret = 1,
            Username = 2,
            Notes = 3,
        }
    }
}
