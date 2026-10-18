DroneDDW - Pull session files
=============================

Copies the session results (Sessions.csv and Deleted Sessions.csv) from the Quest headset to this laptop.
Nothing needs to be installed, and nothing on the headset is changed or deleted.

How to use
1. Plug the Quest into the laptop with a USB-C cable (one that carries data, not only power).
2. First time on this laptop only: put the headset on. It asks "Allow USB debugging?".
   Tick "Always allow from this computer" and press Allow.
3. Double-click "Pull Sessions.bat".

The files are copied to Documents\DroneDDW Sessions\<date and time>\ and that folder opens.
Every pull gets its own folder, so earlier pulls are never overwritten.

- Excel folder: Sessions.xlsx and Deleted Sessions.xlsx, ready to read in Excel, with columns and dates.
- CSV folder: the files exactly as the headset saved them (; between columns).
  Use those for R, SPSS, Python and the like.

If the headset isn't found
- Try another cable or USB port.
- Make sure the headset is switched on and you pressed Allow inside it.
- Some Windows laptops need Meta's "Oculus ADB Driver" to see the Quest. Download it from Meta's developer
  site (search "Oculus ADB driver"), unzip it, right-click android_winusb.inf and choose Install.

Keep the "adb" folder next to Pull Sessions.bat. It holds the script that makes the Excel files and adb, Google's Android Debug Bridge
(Android SDK Platform Tools 36.0.0, Apache License 2.0, see adb\NOTICE.txt).
