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

namespace PluginDriveList
{
    /* Defines behavior for child measures.  Keeps most things from the superclass, but extends
     * Reload() to handle child-specific options (like finding a parent measure).
     */
    internal class ChildMeasure : Measure
    {
        /* Construct a child measure.  (Just passes through to the base class.)
         */
        internal ChildMeasure() : base() { }

        /* Handles "Parent" and "DefaultString" measure options.
         */
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
                return;
            }

            // read the defaultString setting, and use the value from the parent measure if this child doesn't specify one.
            // defaultString = api.ReadString("DefaultString", parent.defaultString);
            defaultString = api.ReadString("DefaultString", parent.defaultString);
        }

        /* no-op */
        internal override void Dispose() { }
    }
}
