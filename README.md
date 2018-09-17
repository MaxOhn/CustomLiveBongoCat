Inspired by reddit user [/u/ZeCryptic](https://www.reddit.com/user/ZeCryptic) in his recent [post](https://www.reddit.com/r/osugame/comments/9gah62/i_made_a_bongo_cat_cam_for_osu_that_works_in_real/) I was just playing around with a custom version of a realtime bongo cat.

Preview: https://streamable.com/3by5o

A working .exe can be found in live_cam/bin/Release/live_cam.exe

The project was done with Visual Studio in C#.

Cursor and keyboard tracking takes place every \~50ms.

The screen is split into evenly sized sectors. Each sector gets its own three sprites i.e. tap left, tap right and no tap.

To use custom image sprites, one has to 
- put the files in the live_cam/Resources folder,
- add them to Resources.resx and Resources.Designer.cs accordingly,
- change the xSectors and ySectors variables in live_cam/LifeCam.cs accordingly (how many sectors in x and y directions),
- and initialize the imageMap dictionary in live_cam/LifeCam.cs's InitializeVariables function according to the new resources

The sprite's size can be accustomed to in LifeCam.cs's Designer


Disclaimer:
The executable does track the keyboard! However, the only reason for this is to differentiate the two osu! keys from each other and all other buttons.
Nothing is being logged, sent, or processed in any other way.