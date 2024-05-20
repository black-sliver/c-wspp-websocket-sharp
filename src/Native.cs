using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace WebSocketSharp
{
    /// <summary>
    /// Collection of helper functions for native interop.
    /// </summary>
    internal class Native
    {
        /// <summary>
        /// Convert .net string to native UTF8 byte array and length. Use Marshal.FreeHGlobal (in finally block) to free it once done.
        /// </summary>
        internal static IntPtr StringToHGlobalUTF8(string s, out int length)
        {
            // NOTE: currently we do string -> UTF8 in C#, but it might be better to change that.
            if (s == null)
            {
                length = 0;
                return IntPtr.Zero;
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(s);
            var ptr = Marshal.AllocHGlobal(bytes.Length + 1);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            Marshal.WriteByte(ptr, bytes.Length, 0);
            length = bytes.Length;

            return ptr;
        }

        /// <summary>
        /// Convert .net string to native UTF8 byte array. Use Marshal.FreeHGlobal (in finally block) to free it once done.
        /// </summary>
        internal static IntPtr StringToHGlobalUTF8(string s)
        {
            int temp;
            return StringToHGlobalUTF8(s, out temp);
        }

        /// <summary>
        /// Convert native IntPtr + Length to managed byte array
        /// </summary>
        internal static byte[] ToByteArray(IntPtr data, int len)
        {
            byte[] bytes = new byte[len];
            if (len > 0)
            {
                Marshal.Copy(data, bytes, 0, len);
            }
            return bytes;
        }

        /// <summary>
        /// Convert cstr to managed String, or returns fallback if cstr is NULL.
        /// Should be UTF-8, but currently uses whatever "ANSI" is set to.
        /// </summary>
        internal static string ToString(IntPtr data, string fallback)
        {
            if (data == IntPtr.Zero)
            {
                return fallback;
            }
            return Marshal.PtrToStringAnsi(data);
        }
    }
}
