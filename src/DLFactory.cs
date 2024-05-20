// libdl abstraction

using System;
using System.Runtime.InteropServices;

namespace WebSocketSharp
{
    internal interface IDL
    {
        IntPtr Open(string filename, int flags);
        IntPtr Sym(IntPtr handle, string symbol);
        int Close(IntPtr handle);
    }

    namespace DyanmicLinkerImpl
    {

        internal class LibDl2So : IDL
        {
            [DllImport("libdl.so.2")]
            private static extern IntPtr dlopen(string filename, int flags);

            [DllImport("libdl.so.2")]
            private static extern IntPtr dlsym(IntPtr handle, string symbol);

            [DllImport("libdl.so.2")]
            private static extern int dlclose(IntPtr handle);

            public IntPtr Open(string filename, int flags)
            {
                return dlopen(filename, flags);
            }

            public IntPtr Sym(IntPtr handle, string symbol)
            {
                return dlsym(handle, symbol);
            }

            public int Close(IntPtr handle)
            {
                return dlclose(handle);
            }
        }

        internal class LibDlSo : IDL
        {
            [DllImport("libdl.so")]
            private static extern IntPtr dlopen(string filename, int flags);

            [DllImport("libdl.so")]
            private static extern IntPtr dlsym(IntPtr handle, string symbol);

            [DllImport("libdl.so")]
            private static extern int dlclose(IntPtr handle);

            public IntPtr Open(string filename, int flags)
            {
                return dlopen(filename, flags);
            }

            public IntPtr Sym(IntPtr handle, string symbol)
            {
                return dlsym(handle, symbol);
            }

            public int Close(IntPtr handle)
            {
                return dlclose(handle);
            }
        }

        internal class Dl : IDL
        {
            [DllImport("dl")]
            private static extern IntPtr dlopen(string filename, int flags);

            [DllImport("dl")]
            private static extern IntPtr dlsym(IntPtr handle, string symbol);

            [DllImport("dl")]
            private static extern int dlclose(IntPtr handle);

            public IntPtr Open(string filename, int flags)
            {
                return dlopen(filename, flags);
            }

            public IntPtr Sym(IntPtr handle, string symbol)
            {
                return dlsym(handle, symbol);
            }

            public int Close(IntPtr handle)
            {
                return dlclose(handle);
            }
        }

        internal class LibSystemDylib : IDL
        {
            [DllImport("libSystem.dylib")]
            private static extern IntPtr dlopen(string filename, int flags);

            [DllImport("libSystem.dylib")]
            private static extern IntPtr dlsym(IntPtr handle, string symbol);

            [DllImport("libSystem.dylib")]
            private static extern int dlclose(IntPtr handle);

            public IntPtr Open(string filename, int flags)
            {
                return dlopen(filename, flags);
            }

            public IntPtr Sym(IntPtr handle, string symbol)
            {
                return dlsym(handle, symbol);
            }

            public int Close(IntPtr handle)
            {
                return dlclose(handle);
            }
        }
    }

    internal class DynamicLinker
    {
        static public IDL Create()
        {
            IDL res;
            try
            {
                res = new DyanmicLinkerImpl.LibSystemDylib();
                res.Open("", 0);
                return res;
            }
            catch (System.DllNotFoundException)
            { }

            try
            {
                res = new DyanmicLinkerImpl.Dl();
                res.Open("", 0);
                return res;
            }
            catch (System.DllNotFoundException)
            { }

            try
            {
                res = new DyanmicLinkerImpl.LibDl2So();
                res.Open("", 0);
                return res;
            }
            catch (System.DllNotFoundException)
            { }

            try
            {
                res = new DyanmicLinkerImpl.LibDlSo();
                res.Open("", 0);
                return res;
            }
            catch (System.DllNotFoundException)
            { }

            throw new PlatformNotSupportedException("Could not find dlopen");
        }
    }
}
