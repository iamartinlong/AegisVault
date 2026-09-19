using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace AegisVault.Platform;

/// <summary>
/// Windows credential UI on the secure desktop (experimental). The system
/// prompt cannot be intercepted by userland key loggers running on the
/// normal desktop.
/// </summary>
public static partial class WindowsSecureInput
{
    private const uint CredUiWinGeneric = 0x0000_0001;
    private const uint CredUiWinSecurePrompt = 0x0000_1000;
    private const uint ErrorCancelled = 1223;

    private const int BufferBytes = 2048;

    public static bool IsAvailable => OperatingSystem.IsWindows();

    /// <summary>Shows the secure desktop prompt and returns the entered password, or null when cancelled.</summary>
    [SupportedOSPlatform("windows")]
    public static string? PromptForPassword(string caption, string message, IntPtr parentWindow = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Secure input is only available on Windows.");
        }

        var captionPointer = Marshal.StringToHGlobalUni(caption);
        var messagePointer = Marshal.StringToHGlobalUni(message);
        IntPtr outputBuffer = IntPtr.Zero;
        uint outputSize = 0;

        try
        {
            var info = new CredUiInfo
            {
                Size = Marshal.SizeOf<CredUiInfo>(),
                ParentWindow = parentWindow,
                MessageText = messagePointer,
                CaptionText = captionPointer,
                Banner = IntPtr.Zero,
            };

            var authPackage = 0u;
            var save = 0;
            var result = CredUIPromptForWindowsCredentials(
                ref info,
                0,
                ref authPackage,
                IntPtr.Zero,
                0,
                out outputBuffer,
                out outputSize,
                ref save,
                CredUiWinGeneric | CredUiWinSecurePrompt);

            if (result == ErrorCancelled || result != 0 || outputBuffer == IntPtr.Zero)
            {
                return null;
            }

            return UnpackPassword(outputBuffer, outputSize);
        }
        finally
        {
            Marshal.FreeHGlobal(captionPointer);
            Marshal.FreeHGlobal(messagePointer);

            // The packed credential buffer holds the password; wipe it before
            // handing the memory back.
            FreeBuffer(outputBuffer, outputSize);
        }
    }

    /// <summary>Zeros the buffer contents (used before freeing credential memory).</summary>
    internal static void ZeroBuffer(IntPtr buffer, uint size)
    {
        if (buffer == IntPtr.Zero || size == 0)
        {
            return;
        }

        Marshal.Copy(new byte[size], 0, buffer, (int)size);
    }

    private static void FreeBuffer(IntPtr buffer, uint size)
    {
        if (buffer == IntPtr.Zero)
        {
            return;
        }

        ZeroBuffer(buffer, size);
        Marshal.FreeCoTaskMem(buffer);
    }

    private static string? UnpackPassword(IntPtr authBuffer, uint authBufferSize)
    {
        var userName = new byte[BufferBytes];
        var domain = new byte[BufferBytes];
        var password = new byte[BufferBytes];

        var userNameChars = (uint)(userName.Length / 2);
        var domainChars = (uint)(domain.Length / 2);
        var passwordChars = (uint)(password.Length / 2);

        try
        {
            var ok = CredUnPackAuthenticationBuffer(
                0,
                authBuffer,
                authBufferSize,
                userName,
                ref userNameChars,
                domain,
                ref domainChars,
                password,
                ref passwordChars);

            if (ok == 0)
            {
                return null;
            }

            var length = (int)passwordChars * 2;
            while (length >= 2 && password[length - 1] == 0 && password[length - 2] == 0)
            {
                length -= 2;
            }

            var plain = Encoding.Unicode.GetString(password, 0, length);
            return string.IsNullOrEmpty(plain) ? null : plain;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(password);
            CryptographicOperations.ZeroMemory(userName);
            CryptographicOperations.ZeroMemory(domain);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CredUiInfo
    {
        public int Size;
        public IntPtr ParentWindow;
        public IntPtr MessageText;
        public IntPtr CaptionText;
        public IntPtr Banner;
    }

    [LibraryImport("credui.dll", EntryPoint = "CredUIPromptForWindowsCredentialsW")]
    private static partial uint CredUIPromptForWindowsCredentials(
        ref CredUiInfo info,
        uint authError,
        ref uint authPackage,
        IntPtr inAuthBuffer,
        uint inAuthBufferSize,
        out IntPtr outAuthBuffer,
        out uint outAuthBufferSize,
        ref int save,
        uint flags);

    [LibraryImport("credui.dll", EntryPoint = "CredUnPackAuthenticationBufferW")]
    private static partial int CredUnPackAuthenticationBuffer(
        uint flags,
        IntPtr authBuffer,
        uint authBufferSize,
        [Out] byte[] userName,
        ref uint userNameChars,
        [Out] byte[] domainName,
        ref uint domainChars,
        [Out] byte[] password,
        ref uint passwordChars);
}
