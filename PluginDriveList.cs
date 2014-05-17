/* ~~ Rainmeter DriveList plugin ~~
 * Maintains a list of connected drives, and returns one of the drive letters from the list
 * as a string that can be used as the "Drive" value of a FreeDiskSpace measure.
 * In this way a skin can be made that can cycle through all mounted drives with no hardcoded drive letters.
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
 * - Default return value as a measure option
 * - csv list of drive letters that will always be included in the list
 *   regardless of whether or not they are connected
 * - Child measure capability where the nth child measure returns the nth drive
 *   (so many connected drives could be displayed at once in individual meters)
 */

#define DLLEXPORT_GETSTRING
#define DLLEXPORT_EXECUTEBANG

using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Rainmeter;

namespace PluginDriveList
{
    /// <summary>
    /// Defines a measure object instance for this plugin.
    /// </summary>
    internal class Measure
    {
        /* Constants */
        private readonly string DEFAULT_RETURN = "_";

        /* Measure instance settings */
        private IntPtr skinHandle;      // reference to the skin this measure is part of
        private string finishAction;    // FinishAction setting
        private Dictionary<DriveType, bool> listedTypes;    // types of drives that this measure will list
        
        /* Measure drive list state */
        private List<string> driveLetters;
        private int currentIndex = 0;

        /* Locks for concurrency and a 'working' flag */
        private readonly object setting_lock = new object();
        private readonly object return_lock = new object();
        private bool queued = false;

        /// <summary>
        /// Measure object constructor initializes type settings dict 
        /// and empty list for drive letters.
        /// </summary>
        internal Measure()
        {
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
        }

        /// <summary>
        /// Runs on load/refresh, or on every update cycle if DynamicVariables is set.
        /// Reads FinishAction and drive type settings.
        /// </summary>
        /// <param name="rm">Rainmeter API instance</param>
        /// <param name="maxValue">Ref to MaxValue (unused here)</param>
        internal void Reload(Rainmeter.API rm, ref double maxValue)
        {
            skinHandle = rm.GetSkin();

            lock (setting_lock)
            {
                finishAction = rm.ReadString("FinishAction", "");
                listedTypes[DriveType.Fixed] = (rm.ReadInt("Fixed", 1) == 1 ? true : false);
                listedTypes[DriveType.Removable] = (rm.ReadInt("Removable", 1) == 1 ? true : false);
                listedTypes[DriveType.Network] = (rm.ReadInt("Network", 1) == 1 ? true : false);
                listedTypes[DriveType.CDRom] = (rm.ReadInt("Optical", 0) == 1 ? true : false);
                listedTypes[DriveType.Ram] = (rm.ReadInt("Ram", 0) == 1 ? true : false);
            }
#if DEBUG
            API.Log(API.LogType.Notice, "DriveList FinishAction: " + finishAction);
#endif
        }

        /// <summary>
        /// Runs every update cycle.  If no background work is queued (there is no background update
        /// thread already runnning) then enqueue a new background work item w/ the coroutineUpdate method.
        /// </summary>
        /// <returns>double - number of items in list of drive letters</returns>
        internal double Update()
        {
            // Update list of drive letters in a worker thread
            if (!queued)
            {
                queued = ThreadPool.QueueUserWorkItem(new WaitCallback(coroutineUpdate));
            }
            
            // Return the number of drives in the list
            double localCount = 0;
            lock (return_lock)
            {
                localCount = (double)driveLetters.Count;
            }
            return localCount;
        }
 
#if DLLEXPORT_GETSTRING
        /// <summary>
        /// Called as-needed, provides string value for the measure. In this case, 
        /// the current drive letter in the driveLetters list, specified by currentIndex.
        /// </summary>
        /// <returns>string - drive letter of drive at currenIndex in driveLetters, or a default</returns>
        internal string GetString()
        {
            string returnMe = DEFAULT_RETURN;
            lock (return_lock)
            {
                if (driveLetters.Count != 0 && currentIndex >= 0)
                {
                    returnMe = driveLetters[currentIndex];
                }
            }
            return returnMe;
        }
#endif

#if DLLEXPORT_EXECUTEBANG
        /// <summary>
        /// Accepts "!CommandMeasure" bangs w/ arguments "forward"
        /// to move the current index up and "backward" to move the current index down.
        /// Locks return value to read number of items in driveLetters and mutate currentIndex.
        /// </summary>
        /// <param name="args">string - the arguments to a !CommandMeasure bang</param>
        internal void ExecuteBang(string args)
        {
            lock (return_lock)  // locks driveLetters and currentIndex
            {
                int localCount = driveLetters.Count;
                switch (args.ToLowerInvariant())
                {
                    case "forward":
                        currentIndex = (localCount == 0 ? 0 : ((currentIndex + 1) % localCount));
                        break;
                    case "backward":
                        currentIndex = (localCount == 0 ? 0 : (int)((currentIndex - 1) - (Math.Floor((currentIndex - 1D) / localCount) * localCount)));
                        break;
                    default:
                        API.Log(API.LogType.Error, "DriveList: Invalid command \"" + args + "\"");
                        break;
                }
            }   
        }
#endif

        /// <summary>
        /// Performs drive list update functions in the background via QueueUserWorkItem.
        /// Could probably stand to be broken into subroutines.
        /// Should consider implementing a lock on the 'queued' flag.
        /// </summary>
        /// <param name="stateInfo">object - state parameter for WaitCallBack</param>
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
                checkIndexRange();
            }
            // run FinishAction if specified
            if (!String.IsNullOrEmpty(localAction))
            {
#if DEBUG
                API.Log(API.LogType.Notice, "DriveList: Executing FinishAction");
#endif
                API.Execute(skinHandle, localAction);
            }
            // reset "queued" flag
            queued = false;
        }

        /// <summary>
        /// Uses DriveInfo to get all connected drives, then filters based on type and if the drive is in a ready state
        /// </summary>
        /// <param name="driveTypes">Dictionary of flags specifiying which types to include in the list</param>
        /// <returns>A list of drive letters</returns>
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

        /// <summary>
        /// Make sure currentIndex is not out-of-bounds for driveLetters.
        /// - Sets the index to -1 if there are no drives in the list.
        /// - Sets the index to 0 if the index was -1 but the list is no longer empty.
        /// Locked from outside in coroutineUpdate().
        /// </summary>
        private void checkIndexRange()
        {
            int c = driveLetters.Count;
            if (currentIndex >= c)
            {
                currentIndex = c - 1;
            }
            else if (currentIndex < 0 && c > 0)
            {
                currentIndex = 0;
            }
        }

    }

    /// <summary>
    /// Binds the Measure class to the low-level Rainmeter C-based API.
    /// Rainmeter calls methods in the static plugin class w/ a measure id,
    /// and the Plugin class calls the methods of the correct Measure instance.
    /// </summary>
    public static class Plugin
    {
#if DLLEXPORT_GETSTRING
        static IntPtr StringBuffer = IntPtr.Zero;
#endif

        [DllExport]
        public static void Initialize(ref IntPtr data, IntPtr rm)
        {
            data = GCHandle.ToIntPtr(GCHandle.Alloc(new Measure()));
        }

        [DllExport]
        public static void Finalize(IntPtr data)
        {
            GCHandle.FromIntPtr(data).Free();
            
#if DLLEXPORT_GETSTRING
            if (StringBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(StringBuffer);
                StringBuffer = IntPtr.Zero;
            }
#endif
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
        
#if DLLEXPORT_GETSTRING
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
#endif

#if DLLEXPORT_EXECUTEBANG
        [DllExport]
        public static void ExecuteBang(IntPtr data, IntPtr args)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            measure.ExecuteBang(Marshal.PtrToStringUni(args));
        }
#endif
    }
}