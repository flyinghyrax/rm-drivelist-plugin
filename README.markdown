## DriveList.dll #
*C# Rainmeter plugin via the [Rainmeter Plugin API][api-link]*

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
Per the main [plugin measure documentation][plugindoc-link], all general measure options are valid.  `UpdateDivider` in particular is *recommended* to keep from polling your disks too often.  Plugin specific options:

**All DriveList measures:**

+ `Index` - the measure will return the drive letter at this position in the list.  (First position is 0.)
+ `DefaultString` - string value returned by the measure when it cannot retrieve a drive letter for the specified index (e.g.,if the index is outside the bounds of the list).  When set on a parent measure, that value will be used as the DefaultString for all child measures that do not specify their own DefaultString value.
+ `NumberType` - the kind of numeric value returned by the measure.  `NumberType=Status` is the default; in that case the measure will return 1 if its index is in the bounds of the list of drives and 0 if not.  `NumberType=Count` will cause the numeric measure value to be the total number of drives in the list.  Most useful on a parent measure, but valid on any type.

**Child measure options:**

+ `Parent` - the name of this child's parent measure.  (Not specifying this option will cause the plugin to make the measure a new parent measure.)

**Parent measure options:**

+ `FinishAction` - Rainmeter [action/bang(s)][bangdoc-link] that will execute every time the parent measure finishes updating the list of drives
+ `Fixed` - 1 to include fixed drives in the list, 0 to exclude (default 1)
+ `Removable` - 1 to include removable drives in the list, 0 to exclude (default 1)
+ `Network` - 1 to include mapped network drives in the list, 0 to exclude (default 1)
+ `Optical` - 1 to include attached optical drives in the list, 0 to exclude (default 0)
+ `Ram` - 1 to include ram disks inthe list, 0 to exclude (default 0)

### Full example skin #
See [usage_example.ini](/usage_example.ini)

[sectionvar-link]: http://docs.rainmeter.net/manual/variables/section-variables
[freediskspace-link]: http://docs.rainmeter.net/manual/measures/freediskspace
[api-link]: https://github.com/rainmeter/rainmeter-plugin-sdk
[webparser-link]: http://docs.rainmeter.net/manual/plugins/webparser
[nowplaying-link]: http://docs.rainmeter.net/manual/plugins/nowplaying
[plugindoc-link]: http://docs.rainmeter.net/manual/measures/plugin
[bangdoc-link]: http://docs.rainmeter.net/manual/bangs
