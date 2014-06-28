/* ~~ Rainmeter DriveList plugin ~~
 * Keeps a list of connected drives of specified types.
 * Each measure sets an "Index"; that measure will then return the drive letter 
 * from that position in the list of drives.  The string value can then be used 
 * in a FreeDiskSpace measure, so that a skin can return drive info without the
 * user having to specify drive letters in the .ini or a settings file.
 * 
 * ~~ LICENSE ~~
 * Copyright (C) 2014 Matthew Seiler
 * 
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 * 
 * ~~ CREDITS ~~
 * - jsmorley
 * - poiru
 * - cjthompson
 * - brian
 * 
 * ~~ TODO ~~
 * - test parent/child
 * - add the old cycling list features back in as a type?
 *   (I'm not sure if that's even possible without making a huge mess, but I can
 *   see use cases for both plugin styles...)
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Rainmeter;

namespace PluginDriveList
{
    // properties or methods which apply to ALL measure regardless of type go here
    internal class Measure
    {
        protected readonly string TAG = "DriveList.dll: ";

        protected int driveIndex;
        protected ParentMeasure parent;

        protected Measure()
        {
            driveIndex = -1;
        }

        internal virtual void Reload(Rainmeter.API api, ref double maxValue)
        {
            int idx = api.ReadInt("Index", 0);
            if (idx == -1)
            {
                API.Log(API.LogType.Warning, TAG + "'Index' setting invalid in " + api.GetMeasureName());
            }
            driveIndex = idx;
        }
        
        internal virtual double Update()
        {
            return (parent != null ? parent.getUpdateValue(driveIndex) : -1.0);
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
            base.Reload(api, ref maxValue);
            string parentName = api.ReadString("Parent", "");
            IntPtr mySkin = api.GetSkin();
            parent = null;
            foreach (ParentMeasure p in ParentMeasure.ParentMeasures)
            {
                if (p.Skin.Equals(mySkin) && p.Name.Equals(parentName))
                {
                    parent = p;
                    break;
                }
            }
            if (parent == null)
            {
                API.Log(API.LogType.Error, "DriveList.dll: Parent=" + parentName + " not valid");
            }
        }
    }


    internal class ParentMeasure : Measure
    {
        /* parent measure settings */
        private string defaultString;
        private string finishAction;
        private Dictionary<DriveType, bool> listedTypes;

        /* list of drives associated with this parent measure */
        private List<string> driveLetters;

        /* Locks and flag for concurrency */
        private readonly object setting_lock = new object();
        private readonly object return_lock = new object();
        private volatile bool queued = false;

        /* for parent/child relationships */
        internal static List<ParentMeasure> ParentMeasures = new List<ParentMeasure>();
        internal string Name;
        internal IntPtr Skin;

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
            base.Reload(api, ref maxValue);

            Name = api.GetMeasureName();
            Skin = api.GetSkin();

            defaultString = api.ReadString("DefaultString", defaultString);
            
            // these settings are used in worker thread, so explicitly lock
            lock (setting_lock)
            {
                finishAction = api.ReadString("FinishAction", "");
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

        internal double getUpdateValue(int idx)
        {
            double inBounds;
            lock (return_lock)
            {
                inBounds = (isInBounds(driveLetters, idx) ? 1.0 : 0.0);
            }
            return inBounds;
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
#if DEBUG
            API.Log(API.LogType.Notice, "DriveList: started coroutine");
#endif
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
            // copy our local list of drive letters out to shared memory and check the value of currentIndex
            lock (return_lock)
            {
                driveLetters.Clear();
                driveLetters.AddRange(temp);
            }
            // run FinishAction if specified
            if (!String.IsNullOrEmpty(localAction))
            {
#if DEBUG
                API.Log(API.LogType.Notice, "DriveList: Executing FinishAction");
#endif
                API.Execute(Skin, localAction);
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