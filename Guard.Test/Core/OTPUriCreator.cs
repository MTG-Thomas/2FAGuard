using Guard.WPF.Core.Export;
using Microsoft.Win32;

namespace Guard.Test.Core
{
    public class OTPUriCreatorTests
    {
        private const string PolicyPath = @"Software\Policies\2FAGuard";
        private const string PolicyName = "PreventUnencryptedExports";

        [Fact]
        public void RejectsQrUriBeforeReadingTokenWhenPolicyBlocksExports()
        {
            using RegistryKey policy =
                Registry.CurrentUser.CreateSubKey(PolicyPath, writable: true)
                ?? throw new InvalidOperationException("Could not open the test policy key.");
            object? previousValue = policy.GetValue(PolicyName);
            RegistryValueKind? previousKind = previousValue == null
                ? null
                : policy.GetValueKind(PolicyName);

            try
            {
                policy.SetValue(PolicyName, 1, RegistryValueKind.DWord);

                Assert.Throws<InvalidOperationException>(() => OTPUriCreator.GetUri(null!));
            }
            finally
            {
                if (previousValue == null)
                {
                    policy.DeleteValue(PolicyName, throwOnMissingValue: false);
                }
                else
                {
                    policy.SetValue(PolicyName, previousValue, previousKind!.Value);
                }
            }
        }
    }
}
