// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using JSObject = Interop.JavaScript.JSObject;
using JSException = Interop.JavaScript.JSException;

internal static partial class Interop
{
    internal static partial class Runtime
    {
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern string InvokeJS(string str, out int exceptionalResult);
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern object CompileFunction(string str, out int exceptionalResult);
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern object InvokeJSWithArgs(int jsObjHandle, string method, object?[] parms, out int exceptionalResult);
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern object GetObjectProperty(int jsObjHandle, string propertyName, out int exceptionalResult);
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern object SetObjectProperty(int jsObjHandle, string propertyName, object value, bool createIfNotExists, bool hasOwnProperty, out int exceptionalResult);
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern object GetByIndex(int jsObjHandle, int index, out int exceptionalResult);
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern object SetByIndex(int jsObjHandle, int index, object? value, out int exceptionalResult);
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern object GetGlobalObject(string? globalName, out int exceptionalResult);

        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern object ReleaseHandle(int jsObjHandle, out int exceptionalResult);
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern object ReleaseObject(int jsObjHandle, out int exceptionalResult);
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern object NewObjectJS(int jsObjHandle, object[] parms, out int exceptionalResult);
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern object BindCoreObject(int jsObjHandle, int gcHandle, out int exceptionalResult);
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern object BindHostObject(int jsObjHandle, int gcHandle, out int exceptionalResult);
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern object New(string className, object[] parms, out int exceptionalResult);
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern object TypedArrayToArray(int jsObjHandle, out int exceptionalResult);
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern object TypedArrayCopyTo(int jsObjHandle, int arrayPtr, int begin, int end, int bytesPerElement, out int exceptionalResult);
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern object TypedArrayFrom(int arrayPtr, int begin, int end, int bytesPerElement, int type, out int exceptionalResult);
        [MethodImplAttribute(MethodImplOptions.InternalCall)]
        internal static extern object TypedArrayCopyFrom(int jsObjHandle, int arrayPtr, int begin, int end, int bytesPerElement, out int exceptionalResult);

        // / <summary>
        // / Execute the provided string in the JavaScript context
        // / </summary>
        // / <returns>The js.</returns>
        // / <param name="str">String.</param>
        public static string InvokeJS(string str)
        {
            var res = InvokeJS(str, out int exception);
            if (exception != 0)
                throw new JSException(res);
            return res;
        }

        public static Interop.JavaScript.Function? CompileFunction(string snippet)
        {
            var res = CompileFunction(snippet, out int exception);
            if (exception != 0)
                throw new JSException((string)res);
            return res as Interop.JavaScript.Function;
        }

        public static int New<T>(params object[] parms)
        {
            var res = New(typeof(T).Name, parms, out int exception);
            if (exception != 0)
                throw new JSException((string)res);
            return (int)res;
        }

        public static int New(string hostClassName, params object[] parms)
        {
            var res = New(hostClassName, parms, out int exception);
            if (exception != 0)
                throw new JSException((string)res);
            return (int)res;
        }

        public static JSObject? NewJSObject(JSObject? jsFuncPtr = null, params object[] parms)
        {
            var res = NewObjectJS(jsFuncPtr?.JSHandle ?? 0, parms, out int exception);
            if (exception != 0)
                throw new JSException((string)res);
            return res as JSObject;
        }

        public static void FreeObject(object obj)
        {
            if (_rawToJS.TryGetValue(obj, out JSObject? jsobj))
            {
                //raw_to_js [obj].RawObject = null;
                _rawToJS.Remove(obj);
                if (jsobj != null)
                {
                    int exception;
                    Runtime.ReleaseObject(jsobj.JSHandle, out exception);
                    if (exception != 0)
                        throw new JSException($"Error releasing object on (raw-obj)");

                    jsobj.JSHandle = -1;
                    jsobj.RawObject = null;
                    jsobj.IsDisposed = true;
                    jsobj.Handle.Free();
                }
            }
            else
            {
                throw new JSException($"Error releasing object on (obj)");
            }
        }

        public static object GetGlobalObject(string? str = null)
        {
            int exception;
            var globalHandle = Runtime.GetGlobalObject(str, out exception);

            if (exception != 0)
                throw new JSException($"Error obtaining a handle to global {str}");

            return globalHandle;
        }

    }
}
