/*
  Copyright (C) 2013 Matthew Seiler

  This program is free software; you can redistribute it and/or
  modify it under the terms of the GNU General Public License
  as published by the Free Software Foundation; either version 2
  of the License, or (at your option) any later version.

  This program is distributed in the hope that it will be useful,
  but WITHOUT ANY WARRANTY; without even the implied warranty of
  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
  GNU General Public License for more details.

  You should have received a copy of the GNU General Public License
  along with this program; if not, write to the Free Software
  Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
*/

/* TODO:
 * - Version with only one thread that loops and is reused
 * - Version with BackgroundWorker
 * ...for the heck of it.
 */
using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Rainmeter;

namespace PluginDriveList
{
    internal class Measure
    {
        private IntPtr skinHandle;
        // measure settings
        private string finishAction = "";
        private string errorString = "";
        private Dictionary<DriveType, bool> listedTypes;
        // return values (sort of)
        private List<string> driveLetters;
        private int currentIndex = 0;

        // lock for measure settings
        private readonly object setting_lock = new object();
        // lock for driveLetters and currentIndex:
        private readonly object return_lock = new object();
        
        // queued work item flag
        private bool queued = false;

        /* Measure object constructor initializes type settings dict
         * and empty list for drive letters.
         */
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

        /* Runs on load/refresh on on every update cycle if DynamicVariables is set.
         * Reads ErrorString, FinishAction, and drive type settings.
         */
        internal void Reload(Rainmeter.API rm, ref double maxValue)
        {
            skinHandle = rm.GetSkin();

            lock (setting_lock)
            {
                finishAction = rm.ReadString("FinishAction", "");
                errorString = rm.ReadString("ErrorString", "");
                // set type settings in dictionary
                listedTypes[DriveType.Fixed] = (rm.ReadInt("Fixed", 1) == 1 ? true : false);
                listedTypes[DriveType.Removable] = (rm.ReadInt("Removable", 1) == 1 ? true : false);
                listedTypes[DriveType.Network] = (rm.ReadInt("Network", 1) == 1 ? true : false);
                listedTypes[DriveType.CDRom] = (rm.ReadInt("Optical", 0) == 1 ? true : false);
                listedTypes[DriveType.Ram] = (rm.ReadInt("Ram", 0) == 1 ? true : false);
            }
#if DEBUG
            API.Log(API.LogType.Notice, "DriveList FinishAction: " + finishAction);
            API.Log(API.LogType.Notice, "DriveList ErrorString: " + errorString);
#endif
        }

        /* Runs every update cycle.  If no background work is queued (there is no background update
         * thread already runnning) then enqueue a new background work item w/ the coroutineUpdate method.
         * Returns the number of items in the drive letter list (questionably useful).
         */
        internal double Update()
        {
            // make list of drive letters (in new thread)
            if (!queued)
            {
                queued = ThreadPool.QueueUserWorkItem(new WaitCallback(coroutineUpdate));
            }
            
            // return the number of drives in the list
            double localCount = 0;
            lock (return_lock)
            {
                localCount = (double)driveLetters.Count;
            }
            return localCount;
        }

        /* Returns the drive letter of the drive at CurrentIndex in listedDrives,
         * or ErrorString if CurrentIndex is out of bounds (or other error).
         * TODO: because we EXPECT that driveLetters will be empty when GetString is first called,
         * we should NOT be using exception handling.
         * Could eliminate ErrorString and reduce number of locks taken by this method.
         */
        internal string GetString()
        {
            string t;
            Monitor.Enter(return_lock);
            Monitor.Enter(setting_lock);
            try
            {
                t = driveLetters[currentIndex];
            }
            catch
            {
#if DEBUG
                API.Log(API.LogType.Warning, "DriveList: caught exception in GetString");
#endif
                t = errorString;
            }
            finally
            {
                Monitor.Exit(return_lock);
                Monitor.Exit(setting_lock);
            }
            return t;
        }

        /* Accepts "!CommandMeasure" bangs w/ arguments "forward"
         * to move the current index up and "backward" to
         * move the current index down.
         * Locks return value to read number of items in driveLetters and
         * mutate currentIndex.
         */
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

        /* Method that will run in a background thread and create the list of drive letters.
         * Could probably stand to be broken into subroutines.
         * Should consider implementing lock on 'queued' flag.
         */
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
            // make an empty local list and retrieve array of Drives
            List<string> temp = new List<string>();
            DriveInfo[] drives = DriveInfo.GetDrives();
            // iterate through drives and add to the list as needed
            foreach (DriveInfo d in drives)
            {
                // if the type is set to true and it is an optical drive or it is "ready"
                if ( localTypes[d.DriveType] && (d.DriveType == DriveType.CDRom || d.IsReady) )
                {
                    temp.Add(d.Name.Substring(0, 2));
                }
            }
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
                API.Log(API.LogType.Notice, "DriveList - Executing FinishAction");
#endif
                API.Execute(skinHandle, localAction);
            }
            // reset "queued" flag
            queued = false;
        }

        /* Make sure the index is not out of bounds.  
         * Will set the index to -1 if there are no drives in the list.
         * Locked from outside in coroutineUpdate().
         */
        private void checkIndexRange()
        { 
            if (currentIndex >= driveLetters.Count)
            {
                currentIndex = driveLetters.Count - 1;
            }
        }

    }

    /* Binds the Measure class to the low-level Rainmeter C-based API.
     * Rainmeter calls methods in the static plugin class w/ a measure id,
     * and the Plugin class calls the methods of the correct Measure instance.
     */
    public static class Plugin
    {
        [DllExport]
        public unsafe static void Initialize(void** data, void* rm)
        {
            uint id = (uint)((void*)*data);
            Measures.Add(id, new Measure());
        }

        [DllExport]
        public unsafe static void Finalize(void* data)
        {
            uint id = (uint)data;
            Measures.Remove(id);
        }

        [DllExport]
        public unsafe static void Reload(void* data, void* rm, double* maxValue)
        {
            uint id = (uint)data;
            Measures[id].Reload(new Rainmeter.API((IntPtr)rm), ref *maxValue);
        }

        [DllExport]
        public unsafe static double Update(void* data)
        {
            uint id = (uint)data;
            return Measures[id].Update();
        }

        [DllExport]
        public unsafe static char* GetString(void* data)
        {
            uint id = (uint)data;
            fixed (char* s = Measures[id].GetString()) return s;
        }

        [DllExport]
        public unsafe static void ExecuteBang(void* data, char* args)
        {
            uint id = (uint)data;
            Measures[id].ExecuteBang(new string(args));
        }

        // Maps measure IDs to Measure objects
        internal static Dictionary<uint, Measure> Measures = new Dictionary<uint, Measure>();
    }
}
