/* Copyright (c) 2014 Matthew Seiler
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
 */

using Rainmeter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace PluginDriveList
{
    /* Defines behavior for parent measures.  All the actual update logic is in this subclass,
     * since child measures retrieve their return values from their parent.
     */
    internal class ParentMeasure : Measure
    {
        // collection of all known parent measures, to match children to parents
        internal static List<ParentMeasure> ParentMeasures = new List<ParentMeasure>();
        
        // locks for the concurrent update thread
        private readonly object setting_lock = new object();
        private readonly object return_lock = new object();

        // flag for the concurrent update thread
        private volatile bool queued = false;
        
        // Rainmeter action (bangs) to execute when the worker thread finishes an update cycle
        private string finishAction;

        // our list of drive letters
        private List<string> driveLetters;

        // defines which kinds of drives to include in the above list
        private Dictionary<DriveType, bool> listedTypes;

        /* Construct a parent measure.  Defines some default settings 
         * and adds self to the parent measure collection.
         */
        internal ParentMeasure() : base()
        {
            this.finishAction = "";
            this.driveLetters = new List<string>();
            this.listedTypes = new Dictionary<DriveType, bool> {
                {DriveType.Fixed, true},
                {DriveType.Network, true},
                {DriveType.Removable, true},
                {DriveType.CDRom, false},
                {DriveType.Ram, false},
                {DriveType.NoRootDirectory, false},
                {DriveType.Unknown, false}
            };

            ParentMeasures.Add(this);
            this.parent = this;
        }

        /* Clean up - removes self from the collection of parent measures 
         */
        internal override void Dispose()
        {
            ParentMeasures.Remove(this);
        }

        /* Parent measure reload behavior.
         */
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

        /* Parent measure update behavior - kicks off a background thread to handle the real work
         * before calling up the superclass implementation.
         */
        internal override double Update()
        {
            // Update list of drive letters in a worker thread
            if (!queued)
            {
                queued = ThreadPool.QueueUserWorkItem(new WaitCallback(concurrentUpdate));
            }
            return base.Update();
        }

        /* Allows child measures to retrieve their numeric values from the parent.
         */
        internal double getUpdateValue(MeasureNumberType type, int idx)
        {
            double returnMe;
            lock (return_lock)
            {
                switch (type)
                {
                    case MeasureNumberType.Status:
                        returnMe = (Helpers.isInBounds(driveLetters, idx) ? 1.0 : 0.0);
                        break;
                    case MeasureNumberType.Count:
                    // see default
                    default:
                        returnMe = (double)driveLetters.Count;
                        break;
                }
            }
            return returnMe;
        }

        /* Allows child measures to retrieve their string values from the parent
         */
        internal string getStringValue(int idx, string def)
        {
            string returnMe;
            lock (return_lock)
            {
                returnMe = Helpers.safeGet(driveLetters, idx, def);
            }
            return returnMe;
        }

        /* Does the actual list updating.  Executed in a worker thread to avoid hanging Rainmeter
         */
        private void concurrentUpdate(Object stateInfo)
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

        /* Helper method for concurrentUpdate that will return a list of drive letter strings,
         * given a dictionary defining which types to include.
         */
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
}
