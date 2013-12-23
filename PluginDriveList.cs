using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Rainmeter;

namespace PluginDriveList
{
    internal class Measure
    {
        private Dictionary<DriveType, bool> listedTypes;
        private string ErrorString;
        private DriveInfo[] allDrives;
        private List<string> listedDrives;
        private int currentIndex;
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

        internal void Reload(Rainmeter.API rm, ref double maxValue)
        {
            // cache type settings from before laste Reload
            Dictionary<DriveType, bool> oldSettings = new Dictionary<DriveType, bool>(listedTypes);
            // set type settings
            listedTypes[DriveType.Fixed] = (rm.ReadInt("Fixed", 1) == 1 ? true : false);
            listedTypes[DriveType.Removable] = (rm.ReadInt("Removable", 0) == 1 ? true : false);
            listedTypes[DriveType.Network] = (rm.ReadInt("Network", 0) == 1 ? true : false);
            listedTypes[DriveType.CDRom] = (rm.ReadInt("Optical", 0) == 1 ? true : false);
            listedTypes[DriveType.Ram] = (rm.ReadInt("Ram", 0) == 1 ? true : false);
            // if any of the settings changes, then flag for a list update
            if (!oldSettings.Equals(listedTypes)) needUpdate = true;

        }

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

            if (listedDrives.Count < 1)
                API.Log(API.LogType.Error, "DriveList: no drives?!");

            return (double)listedDrives.Count;
        }

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
                API.Log(API.LogType.Warning, "Caught an IndexOOB exception");
#endif
                t = ErrorString;
            }
            return t;
        }

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

        /* Clears and repopulates the list of drive names.
         */
        private void updateListedDrives()
        {
            listedDrives.Clear();
            foreach (DriveInfo d in allDrives)
            {
#if DEBUG
                API.Log(API.LogType.Debug, "Found drive " + d.Name);
#endif
                if (d.IsReady && listedTypes[d.DriveType])
                {
                    listedDrives.Add(d.Name);
#if DEBUG
                    API.Log(API.LogType.Debug, "Added drive " + d.Name);
#endif
                }
            }

            if (currentIndex >= listedDrives.Count)
            {
                currentIndex = listedDrives.Count - 1;
            }

            needUpdate = false; // reset the update flag
        }

    }

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

        internal static Dictionary<uint, Measure> Measures = new Dictionary<uint, Measure>();
    }
}
