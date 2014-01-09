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
using System.Threading;
using Rainmeter;

namespace PluginDriveList
{
    internal class Measure
    {
        // Dict to track drive type settings
        private Dictionary<DriveType, bool> listedTypes;
        // GetString() returns this value on error
        private string errorString;
        // List of drive letters that we can return
        private List<string> driveLetters;
        // Index of the "current" element in the above list.
        private int currentIndex;

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
            errorString = "";
            driveLetters = new List<string>();
            currentIndex = 0;
        }

        /* Runs on load/refresh on on every update cycle if DynamicVariables is set.
         * Reads error string and drive type settings.
         */
        internal void Reload(Rainmeter.API rm, ref double maxValue)
        {
            errorString = rm.ReadString("ErrorString", "oops.");
            // set type settings in dictionary
            listedTypes[DriveType.Fixed] = (rm.ReadInt("Fixed", 1) == 1 ? true : false);
            listedTypes[DriveType.Removable] = (rm.ReadInt("Removable", 1) == 1 ? true : false);
            listedTypes[DriveType.Network] = (rm.ReadInt("Network", 1) == 1 ? true : false);
            listedTypes[DriveType.CDRom] = (rm.ReadInt("Optical", 0) == 1 ? true : false);
            listedTypes[DriveType.Ram] = (rm.ReadInt("Ram", 0) == 1 ? true : false);
        }

        /* Runs every update cycle.  Retrieves connected drives with GetDrives(),
         * and passes that array to a method that builds the list of drive letters.
         * Returns the number of items in that list (driveLetters).
         */
        internal double Update()
        {
            // make list of drive letters (in new thread)
            Thread listThread = new Thread(new ThreadStart(buildDriveLetterList));
            listThread.IsBackground = true;
            listThread.Start();
            // check to make sure CurrentIndex is OKAY
            checkIndexRange();
            // return the number of drives in the list
            return (double)driveLetters.Count;
        }

        /* Returns the drive letter of the drive at CurrentIndex in listedDrives,
         * or ErrorString is CurrentIndex is out of bounds (or other error).
         */
        internal string GetString()
        {
            string t;
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
                    currentIndex = ((currentIndex + 1) % driveLetters.Count);
                    break;
                case "backward":
                    currentIndex = (int)((currentIndex - 1) - (Math.Floor((currentIndex - 1D) / driveLetters.Count) * driveLetters.Count));
                    break;
                default:
                    API.Log(API.LogType.Error, "DriveList: Invalid command \"" + args + "\"");
                    break;
            }
        }

        /* Clears and repopulates the list of drive letters
         * from an array of DriveInfo objects.
         */
        private void buildDriveLetterList()
        {
            List<string> temp = new List<string>();
            // get array of DriveInfo objects
            DriveInfo[] drives = DriveInfo.GetDrives();
            foreach (DriveInfo d in drives)
            {
                // if the type is set to true and it is an optical drive or it is "ready", then add to list
                if ( listedTypes[d.DriveType] && (d.DriveType == DriveType.CDRom || d.IsReady) )
                {
                    temp.Add(d.Name.Substring(0, 2));
                }
            }
            // attempt to limit shared memory access in future threading by only accessing driveLetters in one place?
            driveLetters.Clear();
            driveLetters.AddRange(temp);
        }

        /* Make sure the index is not out of bounds.  
         * Will set the index to -1 if there are no drives in the list.
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
