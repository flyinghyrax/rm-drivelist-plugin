/* ~~ Rainmeter DriveList plugin ~~
 * Keeps a list of connected drives of specified types.
 * Each measure sets an "Index"; that measure will then return the drive letter 
 * from that position in the list of drives.  The string value can then be used 
 * in a FreeDiskSpace measure, so that a skin can return drive info without the
 * user having to specify drive letters in the .ini or a settings file.
 * 
 * ~~ LICENSE ~~
 * Copyright (c) 2014 Matthew Seiler
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy 
 * of this software and associated documentation files (the "Software"), to deal 
 * in the Software without restriction, including without limitation the rights 
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies 
 * of the Software, and to permit persons to whom the Software is furnished to do 
 * so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all 
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, 
 * INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A 
 * PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT 
 * HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF 
 * CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE 
 * OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 * 
 * https://www.tldrlegal.com/l/mit
 *
 * ~~ CREDITS ~~
 * - jsmorley
 * - poiru
 * - cjthompson
 * - brian
 */

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Rainmeter;

namespace PluginDriveList
{
    enum MeasureNumberType { Count, Status }

    public static class Helpers
    {
        public static T safeGet<T>(List<T> list, int index, T def)
        {
            return (isInBounds(list, index) ? list[index] : def);
        }

        public static bool isInBounds<T>(List<T> list, int index)
        {
            return (list != null && list.Count > 0 && index >= 0 && index < list.Count);
        }
    }

    /// <summary>
    /// Binds the Measure class to the low-level Rainmeter C-based API.
    /// Rainmeter calls methods in the static plugin class w/ a measure id,
    /// and the Plugin class calls the methods of the correct Measure instance.
    /// </summary>
    public static class Plugin
    {
        internal static readonly string TAG = "DriveList.dll: ";
        
        static IntPtr StringBuffer = IntPtr.Zero;

        [DllExport]
        public static void Initialize(ref IntPtr data, IntPtr rm)
        {
            API api = new Rainmeter.API(rm);
            string parent = api.ReadString("Parent", "");
            DLMeasure m = (String.IsNullOrEmpty(parent) ? (DLMeasure) new DLParentMeasure() : new DLChildMeasure());
            data = GCHandle.ToIntPtr(GCHandle.Alloc(m));
        }

        [DllExport]
        public static void Finalize(IntPtr data)
        {
            DLMeasure measure = (DLMeasure)GCHandle.FromIntPtr(data).Target;
            measure.Dispose();
            GCHandle.FromIntPtr(data).Free();
            
            if (StringBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(StringBuffer);
                StringBuffer = IntPtr.Zero;
            }
        }

        [DllExport]
        public static void Reload(IntPtr data, IntPtr rm, ref double maxValue)
        {
            DLMeasure measure = (DLMeasure)GCHandle.FromIntPtr(data).Target;
            measure.Reload(new Rainmeter.API(rm), ref maxValue);
        }

        [DllExport]
        public static double Update(IntPtr data)
        {
            DLMeasure measure = (DLMeasure)GCHandle.FromIntPtr(data).Target;
            return measure.Update();
        }
        
        [DllExport]
        public static IntPtr GetString(IntPtr data)
        {
            DLMeasure measure = (DLMeasure)GCHandle.FromIntPtr(data).Target;
            if (StringBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(StringBuffer);
                StringBuffer = IntPtr.Zero;
            }

            string stringValue = measure.GetString();
            if (stringValue != null)
            {
                StringBuffer = Marshal.StringToHGlobalUni(stringValue);
            }

            return StringBuffer;
        }
    }
}