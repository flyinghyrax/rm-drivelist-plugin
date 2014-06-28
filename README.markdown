## DriveList.dll #
*C# Rainmeter plugin via the [Rainmeter Plugin API][api-link]*

> Warning - an unfinished readme for unfinished software

This plugin maintains a list of connected drives and returns drive letters from the list, so that the plugin measures can be used as a [Section Variable][sectionvar-link] in the Drive setting of a [FreeDiskSpace measure][freediskspace-link].  In this way a skin can automatically display stats for the system's first *n* connected drives without the user having to manually enter drive letters anywhere.

The plugin uses the parent/child measure model (like [WebParser][webparser-link] or [NowPlaying][nowplaying-link]).  One master measure maintains the list, and each measure returns a letter from a specific index in the list.  Example:
```INI
[MeasureDriveListParent]
Measure=Plugin
Plugin=DriveList.dll
UpdateDivider=5
Index=0

[MeasureChild1]
Measure=Plugin
Plugin=DriveList.dll
Parent=MeasureDriveListParent
Index=1

[MeasureChild2]
Measure=Plugin
Plugin=DriveList.dll
Parent=MeasureDriveListParent
Index=2
```

### Measure Options #
Per the main [plugin measure documentation][plugindoc-link], all general measure options are valid.  `UpdateDivider` in particular is *recommended* to keep from thrashing your disks.  Plugin specific options:

**All DriveList measures:**

+ `Index` - the measure will return the drive letter at this position in the list.  (First position is 0.)
+ `NumberType` - the kind of numeric value returned by the measure.  `NumberType=Status` is the default; in that case the measure will return 1 if its Index is in the bounds of the list of drives and 0 if not.  `NumberType=Count` will cause the numeric measure value to be the total number of drives in the list.  Most useful on a parent measure, but valid on any type.

**Child measure options:**

+ `Parent` - the name of this child's parent measure.  (Not specifying this option will cause the plugin to make the measure a new parent measure.)

**Parent measure options:**

+ `FinishAction` - Rainmeter [action/bang(s)][bangdoc-link] that will execute every time the parent measure finished updating the list of drives
+ `DefaultString` - string value that will be returned by this measure or any of its children if the measure cannot retrieve a drive letter from the list, e.g. if that measure's index is currently larger than the length of the list
+ `Fixed` - 1 to include fixed drives in the list, 0 to exclude (default 1)
+ `Removable` - 1 to include removable drives in the list, 0 to exclude (default 1)
+ `Network` - 1 to include mapped network drives in the list, 0 to exclude (default 1)
+ `Optical` - 1 to include attached optical drives in the list, 0 to exclude (default 0)
+ `Ram` - 1 to include ram disks inthe list, 0 to exclude (default 0)

### Full sample skin #
Written on Rainmeter 3.1.0 r2290
```INI
[Rainmeter]

[Metadata]
Name=MultiDrive.ini
Author=Flying Hyrax
Information=A simple test skin for the DriveList plugin
Version=2014-06-28
License=CC BY-NC-SA 4.0

[Variables]

; == MEASURES ==========================================================

; Plugin measures to return drive letters
[MeasureDriveCount]
Measure=Plugin
Plugin=DriveList.dll
UpdateDivider=5
DefaultString="Default!"
NumberType=Count
Fixed=1
Removable=1
Network=0
Optical=1
Ram=0
FinishAction=[!UpdateMeasureGroup "fdsGroup"][!UpdateMeter *]

[MeasureDriveLetter1]
Measure=Plugin
Plugin=DriveList.dll
Parent=MeasureDriveCount
Index=0

[MeasureDriveLetter2]
Measure=Plugin
Plugin=DriveList.dll
Parent=MeasureDriveCount
Index=1

[MeasureDriveLetter3]
Measure=Plugin
Plugin=DriveList.dll
Parent=MeasureDriveCount
Index=2

; to use the numeric value of a plugin measure w/out Dynamic Variables
;[MeasureDriveCountNum]
; Measure=Calc
; Formula=MeasureDriveCount

; Disk measures for first disk
[MeasureDiskLabel1]
Measure=FreeDiskSpace
Drive=[MeasureDriveLetter1]
Label=1
DynamicVariables=1
UpdateDivider=-1
Group=fdsGroup

[MeasureDiskFree1]
Measure=FreeDiskSpace
Drive=[MeasureDriveLetter1]
DynamicVariables=1
UpdateDivider=-1
Group=fdsGroup

; Disk measures for second disk
[MeasureDiskLabel2]
Measure=FreeDiskSpace
Drive=[MeasureDriveLetter2]
Label=1
DynamicVariables=1
UpdateDivider=-1
Group=fdsGroup

[MeasureDiskFree2]
Measure=FreeDiskSpace
Drive=[MeasureDriveLetter2]
DynamicVariables=1
UpdateDivider=-1
Group=fdsGroup

; Disk measures for third disk
[MeasureDiskLabel3]
Measure=FreeDiskSpace
Drive=[MeasureDriveLetter3]
Label=1
DynamicVariables=1
UpdateDivider=-1
Group=fdsGroup

[MeasureDiskFree3]
Measure=FreeDiskSpace
Drive=[MeasureDriveLetter3]
DynamicVariables=1
UpdateDivider=-1
Group=fdsGroup

; == METERS ============================================================

[StyleAllString]
AntiAlias=1
FontFace=Segoe UI
FontSize=14
FontColor=250,250,250
SolidColor=0,0,0,200
AutoScale=1
Text="%1 %2B"
X=r
Y=R

[MeterDriveCount]
Meter=String
MeterStyle=StyleAllString
X=0
Y=0
Text="Count: [MeasureDriveCount:]"
DynamicVariables=1

[MeterDisk1]
Meter=String
MeterStyle=StyleAllString
MeasureName=MeasureDiskLabel1
MeasureName2=MeasureDiskFree1

[MeterDisk2]
Meter=String
MeterStyle=StyleAllString
MeasureName=MeasureDiskLabel2
MeasureName2=MeasureDiskFree2

[MeterDisk3]
Meter=String
MeterStyle=StyleAllString
MeasureName=MeasureDiskLabel3
MeasureName2=MeasureDiskFree3

```

[sectionvar-link]: http://docs.rainmeter.net/manual/variables/section-variables
[freediskspace-link]: http://docs.rainmeter.net/manual/measures/freediskspace
[api-link]: https://github.com/rainmeter/rainmeter-plugin-sdk
[webparser-link]: http://docs.rainmeter.net/manual/plugins/webparser
[nowplaying-link]: http://docs.rainmeter.net/manual/plugins/nowplaying
[plugindoc-link]: http://docs.rainmeter.net/manual/measures/plugin
[bangdoc-link]: http://docs.rainmeter.net/manual/bangs
