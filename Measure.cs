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

namespace PluginDriveList
{
    /* Defines behavior common to both parent and child measures.
     * 
     * We could avoid the use of abstract by moving the parent measure list management and dispose()
     * logic up and out into the Plugin class, but it does provide some semantic value (since this
     * should only ever be used as a superclass) and there may also be some value to hiding
     * the parent measure management stuff down in the measure hierarchy instead of putting it in Plugin.
     */
    internal abstract class Measure
    {
        // name of this measure
        internal string measureName;
        
        // skin containing this measure
        internal IntPtr skinHandle;

        // parent for this measure (the parent of a parent is itself)
        protected ParentMeasure parent;

        // type of numeric value to return
        private MeasureNumberType numberType;

        // index in list of drives for this measure
        private int driveIndex;

        // default value to return when index is out of bounds
        internal string defaultString;

        /* Superclass constructor just defines some defaults.
         * Otherwise rather boring.  Unnecessary, even.
         */
        protected Measure()
        {
            this.numberType = MeasureNumberType.Status;
            this.driveIndex = -1;
            this.defaultString = "";
        }

        /* Reload behavior for all measures.
         * Grabs the measure name, current skin, numeric return type, and drive list index.
         */
        internal virtual void Reload(Rainmeter.API api, ref double maxValue)
        {
            // we will use this for logging and parent/child matching
            measureName = api.GetMeasureName();
            skinHandle = api.GetSkin();

            // read the 'NumberType' setting
            string t = api.ReadString("NumberType", "status").ToLowerInvariant();
            switch (t)
            {
                case "status":
                    numberType = MeasureNumberType.Status;
                    break;
                case "count":
                    numberType = MeasureNumberType.Count;
                    break;
                default:
                    API.Log(API.LogType.Warning, Plugin.TAG + "'NumberType=" + t + "' invalid in " + measureName);
                    numberType = MeasureNumberType.Status;
                    break;
            }

            // read the 'Index' setting
            int idx = api.ReadInt("Index", -1);
            driveIndex = idx; // TODO: validate somehow?
        }

        /* Number value returned by the measure.  All measures retrieve this value from their parent.
         */
        internal virtual double Update()
        {
            return parent != null
                ? parent.getUpdateValue(this.numberType, this.driveIndex)
                : 0d;
        }

        /* String value returned by the measure.  Again, all measures retrieve this value from their parent.
         */
        internal string GetString()
        {
            return parent != null
                ? parent.getStringValue(this.driveIndex, this.defaultString)
                : this.defaultString;
        }

        /* Parent measures need this to clean themselves out of the ParentMeasures collection.
         */
        internal abstract void Dispose();
    }
}
