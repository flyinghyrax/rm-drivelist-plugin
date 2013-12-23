DriveList Plugin
===================
Works with the [Rainmeter Plugin API](https://github.com/rainmeter/rainmeter-plugin-sdk "rainmeter-plugin-sdk")

Simple C# plugin for Rainmeter that creates a list of drive letters for all currently mounted drives.  A DriveList plugin measure returns a String value of the "current" drive in the form "C:\", so you can use the value of the plugin measure as the "Drive" setting in a FreeDiskSpace measure (with DynamicVariables=1).

##### Options 
Set to 1 or 0 to change the drive types to include in the list
* `Network`   - include the letters of mapped network drives
* `Removable` - include the letters of mounted removable drives
* `Optical`   - include the letters of mounted optical "CDRom" type drives

##### Bangs
Cycle through the list of drive letters
* `[!CommandMeasure "yourDriveListMeasure" "Forward"]`
* `[!CommandMeasure "yourDriveListMeasure" "Backward"]`
 
##### Issues / To-do:
- [ ] Error codes?
- [ ] MOAR TESTING
- [ ] Switching to a Network drive seems oddly laggy
- [ ] Measure does not immediately return a string value; it appears that Measure.GetString() is actually called before Reload() and Update() are done populating the list if drives.
 
##### Simple example skin
```
[Rainmeter]
Update=1000

[measureDriveList]
Measure=PLUGIN
Plugin=DriveList.dll
Optical=1
Removable=1
Network=0
UpdateDivider=20

[measureDiskLabel]
Measure=FREEDISKSPACE
Drive=[measureDriveList]
Label=1
DynamicVariables=1
UpdateDivider=10
Group=DiskMsrs

[measureDiskSpace]
Measure=FREEDISKSPACE
Drive=[measureDriveList]
DynamicVariables=1
UpdateDivider=10
Group=DiskMsrs

[meterDriveName]
Meter=STRING
MeasureName=measureDiskLabel
FontFace=Segoe UI
FontSize=12
FontColor=250,250,250
AntiAlias=1
SolidColor=0,0,0,200
X=0
Y=0
Text="[measureDriveList] %1"
DynamicVariables=1
LeftMouseUpAction=[!CommandMeasure "measureDriveList" "forward"][!UpdateMeasureGroup DiskMsrs][!Redraw]
RightMouseUpAction=[!CommandMeasure "measureDriveList" "backward"][!UpdateMeasureGroup DiskMsrs][!Redraw]

[meterDriveSpace]
Meter=STRING
MeterStyle=meterDriveName
MeasureName=measureDiskSpace
AutoScale=1
X=r
Y=R
Text=%1
```


