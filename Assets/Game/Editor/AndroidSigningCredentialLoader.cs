#if UNITY_EDITOR_WIN
using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace FortressFrontier.Editor
{
    [InitializeOnLoad]
    internal static class AndroidSigningCredentialLoader
    {
        private const string StorePasswordTarget = "RampartRivals.Android.Release.StorePass";
        private const string AliasPasswordTarget = "RampartRivals.Android.Release.KeyPass";
        private const uint GenericCredentialType = 1;

        static AndroidSigningCredentialLoader()
        {
            TryApply();
            EditorApplication.delayCall += () => TryApply();
        }

        internal static bool TryApply()
        {
            if (!TryReadGenericCredential(StorePasswordTarget, out var storePassword)
                || !TryReadGenericCredential(AliasPasswordTarget, out var aliasPassword))
                return false;

            PlayerSettings.Android.keystorePass = storePassword;
            PlayerSettings.Android.keyaliasPass = aliasPassword;
            return true;
        }

        private static bool TryReadGenericCredential(string target, out string password)
        {
            password = string.Empty;
            if (!CredRead(target, GenericCredentialType, 0, out var credentialPointer)
                && !CredRead("LegacyGeneric:target=" + target, GenericCredentialType, 0, out credentialPointer))
                return false;
            try
            {
                var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
                if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0) return false;
                var bytes = new byte[credential.CredentialBlobSize];
                Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
                password = Encoding.Unicode.GetString(bytes).TrimEnd('\0');
                return password.Length > 0;
            }
            finally
            {
                CredFree(credentialPointer);
            }
        }

        [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credentialPointer);

        [DllImport("Advapi32.dll", SetLastError = true)]
        private static extern void CredFree(IntPtr credentialPointer);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NativeCredential
        {
            public uint Flags;
            public uint Type;
            public IntPtr TargetName;
            public IntPtr Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            public IntPtr TargetAlias;
            public IntPtr UserName;
        }
    }

    internal sealed class AndroidSigningBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == UnityEditor.BuildTarget.Android
                && !AndroidSigningCredentialLoader.TryApply())
                throw new BuildFailedException(
                    "Android release signing credentials are missing from Windows Credential Manager.");
        }
    }
}
#endif
