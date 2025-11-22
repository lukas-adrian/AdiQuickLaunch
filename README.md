# Creating Quicklinks in the Startbar of Windows

## Why:
Since the StartMenu of Windows 10 or later I never could add some simple links to files, folder or applications.
OpenShell is nice but I have a lot of links and need more organization. Thats why I created this app just for me and published it if someone having the same problems.

## How to do:
With the AdiQuickLaunchManager you create a QuickLaunch-Profile and add Applications or Folder, change the main icon or change the link name.
After it you create a "link on the desktop". That link is a link to the AdiQuickLaunchItem, which starts a small list of your links.
If you open that list you just say "Pin to Taskbar" in the Taskbar menu (right mouse click on the applicatoin). It will show all link like a StartMenu.

## A little bit technical stuff:
I used Jetbrains Rider and .NET 8. The setup I created with OpenSource Application Inno Setup Compiler ([Inno Setup Homepage](https://jrsoftware.org/isinfo.php)).
All profiles are saved under %APPDATA%\AppData\Roaming\AdiSoft\AdiQuickLauncher\GUID.json with an ID, Name, IconPath and a list with items/links (Name, Path, IsDirectory)

## Still to do:
- Drag & Drop of Files/Applications and Folder
- Find out if Pin to Taskbar can set automaticly
- create a better readme
- (brainstorming idea, dockable somewhere?)
