DriveList Plugin
----------------
Works with the [Rainmeter Plugin API](https://github.com/rainmeter/rainmeter-plugin-sdk "rainmeter-plugin-sdk")

Simple C# plugin for Rainmeter that creates a list of drive letters for all currently mounted drives.  A DriveList plugin measure returns a String value of the "current" drive in the form "C:", so you can use the value of the plugin measure as the "Drive" setting in a FreeDiskSpace measure (with DynamicVariables=1).

##### Options
* `ErrorString` - the plugin returns this string on error or before the drive list is populated.

Types of drives to include in the list.  Set to 1 to include, or 0 to exclude.  `Fixed`, `Removable`, and `Network` are included by default.
* `Fixed`		- Regular ol' hard drives
* `Network`		- mapped network drives
* `Removable`	- mounted removable drives
* `Optical`		- mounted optical "CDRom" type drives
* `Ram`			- RAM disks

##### Bangs
Cycle through the list of drive letters
* `[!CommandMeasure "yourDriveListMeasure" "Forward"]`
* `[!CommandMeasure "yourDriveListMeasure" "Backward"]`
 
##### Issues / To-do:
- [ ] MOAR TESTING
- [ ] Measure does not immediately return a string value; it appears that Measure.GetString() is actually called before Reload() and Update() are done populating the list if drives.
- [ ] Does not return empty optical drives
 
##### Example Skin
```
[Rainmeter]
Update=1000
DynamicWindowSize=1

[measureDriveList]
Measure=PLUGIN
Plugin=DriveList.dll
ErrorString="C:"
Optical=1
Network=0
UpdateDivider=20

[measureDiskLabel]
Measure=FREEDISKSPACE
Drive=[measureDriveList]
Label=1
DynamicVariables=1
UpdateDivider=-1
Group=fdsGroup

[measureDiskSpace]
Measure=FREEDISKSPACE
Drive=[measureDriveList]
DynamicVariables=1
UpdateDivider=60
Group=fdsGroup

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
Text="[measureDriveList]\ %1"
DynamicVariables=1
LeftMouseUpAction=[!CommandMeasure "measureDriveList" "forward"][!UpdateMeasureGroup fdsGroup][!Update *][!Redraw]
RightMouseUpAction=[!CommandMeasure "measureDriveList" "backward"][!UpdateMeasureGroup fdsGroup][!Update *][!Redraw]

[meterDriveSpace]
Meter=STRING
MeterStyle=meterDriveName
MeasureName=measureDiskSpace
AutoScale=1
X=R
Y=r
Text="%1 free"
```


