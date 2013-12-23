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

using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Rainmeter;

namespace PluginDriveList
{
    internal class Measure
    {
        // Dict to track drive type settings
        private Dictionary<DriveType, bool> listedTypes;
        // GetString() returns this value on error
        private string ErrorString;
        // Array of DriveInfo objects representing all connected drives
        private DriveInfo[] allDrives;
        // List of drive letters that we can return
        private List<string> listedDrives;
        // Index of the "current" element in the above list.
        private int currentIndex;
        // Flag indicating whether or not to update listedDrives this cycle.
        private bool needUpdate;

        /* Measure object constructor initializes class fields
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
            listedDrives = new List<string>();
            currentIndex = 0;
            needUpdate = true;
        }

        /* Runs on load/refresh on on every update cycle if DynamicVariables is set.
         * Reads error string and drive type settings.  For DynamicVariables - if
         * the type settings dictionary from this reload is different from that of the 
         * last reload, then set the update flag.
         */
        internal void Reload(Rainmeter.API rm, ref double maxValue)
        {
            ErrorString = rm.ReadString("ErrorString", "oops.");
            // cache type settings from last Reload
            Dictionary<DriveType, bool> oldSettings = new Dictionary<DriveType, bool>(listedTypes);
            // set type settings in dictionary
            listedTypes[DriveType.Fixed] = (rm.ReadInt("Fixed", 1) == 1 ? true : false);
            listedTypes[DriveType.Removable] = (rm.ReadInt("Removable", 1) == 1 ? true : false);
            listedTypes[DriveType.Network] = (rm.ReadInt("Network", 1) == 1 ? true : false);
            listedTypes[DriveType.CDRom] = (rm.ReadInt("Optical", 0) == 1 ? true : false);
            listedTypes[DriveType.Ram] = (rm.ReadInt("Ram", 0) == 1 ? true : false);
            // if any of the settings changes, then flag for a list update
            if (!oldSettings.Equals(listedTypes)) 
                needUpdate = true;
        }

        /* Runs every update cycle.  Checks connected to drives w/ GetDrives, and
         * sets the update flag to true if the DriveInfo array from this update differs
         * form that of the last update.
         * updateListedDrives() is called if the update flag is set.
         * Returns the number of items in listedDrives.
         */
        internal double Update()
        {
            DriveInfo[] newDrives = DriveInfo.GetDrives();  // get array of DriveInfo objects
            // if the connected drives have changes somehow, copy to allDrives and flag for list update
            if (!newDrives.Equals(allDrives))
            {
                allDrives = newDrives;
                needUpdate = true;
            }
            // if the flag is set, run an update.  
            if (needUpdate) 
                updateListedDrives();
            // if the drive list is empty (error, or settings that exclude all types) then log that
            if (listedDrives.Count < 1)
                API.Log(API.LogType.Warning, "DriveList: no drives listed");
            // return the number of drives in the list
            return (double)listedDrives.Count;
        }

        /* Returns the drive letter of the drive at CurrentIndex in listedDrives,
         * or ErrorString is CurrentIndex is out of bounds (or other error).
         */
        internal string GetString()
        {
            string t;
            try
            {
                t = listedDrives[currentIndex];
            }
            catch
            {
#if DEBUG
                API.Log(API.LogType.Warning, "DriveList: caught exception in GetString");
#endif
                t = ErrorString;
            }
            return t;
        }

        /* Accepts "!CommandMeasure" bangs w/ arguments "forward"
         * to move the current index up and "backward" to
         * move the current index down.
         */
        internal void ExecuteBang(string args)
        {
            switch (args.ToLowerInvariant())
            {
                case "forward":
                    currentIndex = ((currentIndex + 1) % listedDrives.Count);
                    break;
                case "backward":
                    currentIndex = (int)( (currentIndex - 1) - ( Math.Floor( (currentIndex - 1D) / listedDrives.Count ) * listedDrives.Count ) );
                    break;
                default:
                    API.Log(API.LogType.Error, "DriveList: Invalid command \"" + args + "\"");
                    break;
            }
        }

        /* Clears and repopulates the list of drive names, 
         * and resets the update flag.
         */
        private void updateListedDrives()
        {
            listedDrives.Clear();
            foreach (DriveInfo d in allDrives)
            {
                if (d.IsReady && listedTypes[d.DriveType])
                    listedDrives.Add(d.Name.Substring(0, 2));
            }
            // make sure the index is not out of bounds.  Will set the index to -1 if there are no drives in the list.
            if (currentIndex >= listedDrives.Count)
                currentIndex = listedDrives.Count - 1;
            // reset the update flag
            needUpdate = false;
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
