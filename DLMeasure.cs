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

using System;
using System.Collections.Generic;
using Rainmeter;

namespace PluginDriveList
{
    internal class DLMeasure
    {
        protected static List<DLParentMeasure> ParentMeasures = new List<DLParentMeasure>();

        internal string measureName;
        
        internal IntPtr skinHandle;

        protected DLParentMeasure parent;

        private MeasureNumberType numberType;

        private int driveIndex;

        internal string defaultString;

        protected DLMeasure()
        {
            this.numberType = MeasureNumberType.Status;
            this.driveIndex = -1;
            this.defaultString = "";
        }

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

        internal virtual double Update()
        {
            return parent != null ? parent.getUpdateValue(this.numberType, this.driveIndex) : 0d;
        }

        internal virtual string GetString()
        {
            return parent != null ? parent.getStringValue(this.driveIndex) : this.defaultString;
        }

        internal virtual void Dispose() { }
    }
}
