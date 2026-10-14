# DAEnforcersSaveTool
Usage: DAEnforcersSaveTool.exe (-dec/-enc) path-to-file

## How to edit save files
1. Go to /storage/emulated/0/Android/data/com.mobirate.dacom/files/, then move the .enc file to your PC.
2. Unzip the tool, move the .enc files to the same folder as the tool, then type:
```DAEnforcersSaveTool.exe -dec <your-file>```
3. Edit the output .json, then encode using:
```DAEnforcersSaveTool.exe -enc <your-edited-file>```
4. Move them back to your phone, then replace the original file. Open MT Manager, then hold your .enc file and go to "Properties...";
5. Click "Modify" near permissions, and untick "Write" and "Exec" permissions everywhere. In result it should be set to r--r--r-- (444), press OK.
6. Open the game, press "Start". Game should crash, wait a bit then exit it. Switch back to MT Manager and set the permissions back to rwxrwxrwx (777).
7. Relaunch the game, do NOT send the crash logs. Now you should have your changes fully applied.
