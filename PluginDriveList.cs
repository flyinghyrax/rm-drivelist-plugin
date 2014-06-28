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
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Rainmeter;

namespace PluginDriveList
{
    enum MeasureNumberType { Count, Status }

    internal class Measure
    {
        // for logging
        protected readonly string TAG = "DriveList.dll: ";
        
        // the type of numeric return value for this measure
        protected MeasureNumberType numberType;
        
        // index in drive list for this measure
        protected int driveIndex;
        
        // parent measure - a parent's parent is itself.
        protected ParentMeasure parent;
        
        // name of this measure
        internal string measureName;
        
        // pointer to the skin containing this measure
        internal IntPtr skinHandle;

        protected Measure()
        {
            numberType = MeasureNumberType.Status;
            driveIndex = -1;
        }

        internal virtual void Reload(Rainmeter.API api, ref double maxValue)
        {
            // we will use this for logging and parent/child matching
            measureName = api.GetMeasureName();
            skinHandle = api.GetSkin();
            // read the 'NumberType' setting
            string t = api.ReadString("NumberType", "status").ToLowerInvariant();
            if (t.Equals("status"))
            {
                numberType = MeasureNumberType.Status;
            }
            else if (t.Equals("count"))
            {
                numberType = MeasureNumberType.Count;
            }
            else
            {
                API.Log(API.LogType.Warning, TAG + "'NumberType=" + t + "' invalid in " + measureName);
                numberType = MeasureNumberType.Status;
            }
            // read the 'Index' setting
            int idx = api.ReadInt("Index", -1);
            // TODO: validate somehow?
            driveIndex = idx;
        }
        
        internal virtual double Update()
        {
            return (parent != null ? parent.getUpdateValue(numberType, driveIndex) : -1.0);
        }
        
        internal virtual string GetString()
        {
            return (parent != null ? parent.getStringValue(driveIndex) : "");
        }

        internal virtual void Dispose() 
        {
        }
            
    }

    internal class ChildMeasure : Measure
    {
        internal ChildMeasure() : base() {}

        internal override void Reload(API api, ref double maxValue)
        {
            // read measure name, skin, 'NumberType', and 'Index'
            base.Reload(api, ref maxValue);
            // read 'Parent' setting and match with parent measure
            string parentName = api.ReadString("Parent", "");
            parent = null;
            foreach (ParentMeasure p in ParentMeasure.ParentMeasures)
            {
                if (p.skinHandle.Equals(this.skinHandle) 
                    && p.measureName.Equals(parentName))
                {
                    parent = p;
                    break;
                }
            }
            if (parent == null)
            {
                API.Log(API.LogType.Error, "DriveList.dll: 'Parent=" + parentName + "' not valid in " + measureName);
            }
        }
    }

    internal class ParentMeasure : Measure
    {
        // this string will be returned by GetString if it can't get a drive letter at the specified index
        private string defaultString;
        
        // bangs to execute when the parent finishes updating the drive list
        private string finishAction;
       
        // dicitonary of flags specifiying which drive types to keep in the list
        private Dictionary<DriveType, bool> listedTypes;
        
        // the actual list of drive letters
        private List<string> driveLetters;

        // locks the finishAction and listedTypes settings, since they are used on the worker thread
        private readonly object setting_lock = new object();
        
        // locks the driveLetters list, since it is mutated on the worker thread
        private readonly object return_lock = new object();
        
        // flag is true when an an update is running on the worker thread
        private volatile bool queued = false;
        
        // contains every parent measure from every skin, so that child measures can find a reference to their parent
        internal static List<ParentMeasure> ParentMeasures = new List<ParentMeasure>();

        internal ParentMeasure() : base()
        {
            defaultString = "_";
            finishAction = "";
            listedTypes = new Dictionary<DriveType, bool>
                {
                    {DriveType.Fixed, true},
                    {DriveType.Network, true},
                    {DriveType.Removable, true},
                    {DriveType.CDRom, false},
                    {DriveType.Ram, false},
                    {DriveType.NoRootDirectory, false},
                    {DriveType.Unknown, false}
                };
            driveLetters = new List<string>();
            ParentMeasures.Add(this);
            this.parent = this;
        }

        internal override void Dispose()
        {
            this.parent = null;
            ParentMeasures.Remove(this);
        }

        internal override void Reload(API api, ref double maxValue)
        {
            // read measure name, skin, 'Index' setting, and 'NumberType' setting
            base.Reload(api, ref maxValue);

            // read the 'DefaultString' setting
            defaultString = api.ReadString("DefaultString", defaultString);
            
            // these settings are used in worker thread, so explicitly lock
            lock (setting_lock)
            {
                // read the 'FinishAction' setting
                finishAction = api.ReadString("FinishAction", "");
                // read each of the drive type settings
                listedTypes[DriveType.Fixed] = (api.ReadInt("Fixed", 1) == 1 ? true : false);
                listedTypes[DriveType.Removable] = (api.ReadInt("Removable", 1) == 1 ? true : false);
                listedTypes[DriveType.Network] = (api.ReadInt("Network", 1) == 1 ? true : false);
                listedTypes[DriveType.CDRom] = (api.ReadInt("Optical", 0) == 1 ? true : false);
                listedTypes[DriveType.Ram] = (api.ReadInt("Ram", 0) == 1 ? true : false);
            }
        }

        internal override double Update()
        {
            // Update list of drive letters in a worker thread
            if (!queued)
            {
                queued = ThreadPool.QueueUserWorkItem(new WaitCallback(coroutineUpdate));
            }
            return base.Update();
        }

        internal double getUpdateValue(MeasureNumberType type, int idx)
        {
            double returnMe;
            lock (return_lock)
            {
                if (type.Equals(MeasureNumberType.Status))
                {
                    returnMe = (isInBounds(driveLetters, idx) ? 1.0 : 0.0);
                }
                else // if (type.Equals(MeasureNumberType.Count))
                {
                    returnMe = (double)driveLetters.Count;
                }
            }
            return returnMe;
        }

        internal string getStringValue(int idx)
        {
            string returnMe;
            lock (return_lock)
            {
                returnMe = safeGet(driveLetters, idx, defaultString);
            }
            return returnMe;
        }

        private T safeGet<T>(List<T> list, int index, T def)
        {
            return (isInBounds(list, index) ? list[index] : def);
        }
        
        private bool isInBounds<T>(List<T> list, int index)
        {
            return (list != null && list.Count > 0 && index >= 0 && index < list.Count);
        }

        private void coroutineUpdate(Object stateInfo)
        {
            // read measure settings into local copies
            Dictionary<DriveType, bool> localTypes;
            string localAction;
            lock (setting_lock)
            {
                localTypes = new Dictionary<DriveType, bool>(listedTypes);
                localAction = finishAction;
            }
            // get a list of drive letters using the types we pulled from settings
            List<string> temp = getDriveLetters(localTypes);
            // copy our local list of drive letters out to shared memory
            lock (return_lock)
            {
                driveLetters.Clear();
                driveLetters.AddRange(temp);
            }
            // run FinishAction if specified
            if (!String.IsNullOrEmpty(localAction))
            {
                API.Execute(skinHandle, localAction);
            }
            // reset "queued" flag
            queued = false;
        }

        private List<string> getDriveLetters(Dictionary<DriveType, bool> driveTypes)
        {
            List<string> temp = new List<string>();
            DriveInfo[] drives = DriveInfo.GetDrives();
            // iterate through drives and add to the list as needed
            foreach (DriveInfo d in drives)
            {
                // if the type is set to true and it is an optical drive or it is "ready"
                if (driveTypes[d.DriveType] && (d.DriveType == DriveType.CDRom || d.IsReady))
                {
                    temp.Add(d.Name.Substring(0, 2));
                }
            }
            return temp;
        }
    }

    /// <summary>
    /// Binds the Measure class to the low-level Rainmeter C-based API.
    /// Rainmeter calls methods in the static plugin class w/ a measure id,
    /// and the Plugin class calls the methods of the correct Measure instance.
    /// </summary>
    public static class Plugin
    {
        static IntPtr StringBuffer = IntPtr.Zero;

        [DllExport]
        public static void Initialize(ref IntPtr data, IntPtr rm)
        {
            API api = new Rainmeter.API(rm);
            string parent = api.ReadString("Parent", "");
            Measure m = (String.IsNullOrEmpty(parent) ? (Measure) new ParentMeasure() : new ChildMeasure());
            data = GCHandle.ToIntPtr(GCHandle.Alloc(m));
        }

        [DllExport]
        public static void Finalize(IntPtr data)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
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
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            measure.Reload(new Rainmeter.API(rm), ref maxValue);
        }

        [DllExport]
        public static double Update(IntPtr data)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            return measure.Update();
        }
        
        [DllExport]
        public static IntPtr GetString(IntPtr data)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
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