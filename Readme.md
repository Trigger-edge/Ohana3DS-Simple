## **Ohana3DS Simple**

### **What is Ohana3DS-Simple?**

Ohana3DS is a work in progress tool used to ONLY extract data from decrypted 3DS roms.

This version is a revamp of Rebirth, but in console application form, to ONLY be used to extract data.

This way you could do cool stuff like writing batch scripts so you can easily intergrate it into your own stuff using only the executable.

Example of exporting a '.bcres' model file to '.dae' using a batch script.
```
@echo off
"Ohana3DS Simple" -e test_model.bcres -model
pause
```

Example of exporting texture files ('.png') from a '.bcres' model file using a batch script.
```
@echo off
"Ohana3DS Simple" -e test_model.bcres -texture
pause
```

Example of exporting a '.bclim' texture file to '.png' using a batch script.
```
@echo off
"Ohana3DS Simple" -e test_texture.bclim -texture
pause
```

### **What to know**

Models are default hardcoded to export as '.dae' files as of first commit.

The default export directory is the same directory the executable is in. This is hardcoded.

You might need to make heavy modifications in order to export certain stuff or exporting it in a different directory. Animation exporting is untested.
