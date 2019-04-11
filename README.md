# Unity-Experimental-DXR

This repository contains the Experimental DXR project.
This project use large file storage. Please check https://git-lfs.github.com/ first step to configure large file storage on your system.

The Experimental DXR project is a Unity custom version with binaries based on the 2019.2a5 version of Unity, enhanced with DXR support and version 5.8.0 of HDRP enhanced with DXR support. It is a Windows 10 (64 bit) only version with DX12 API.

This project is a sandbox in which you can  play with real time ray tracing features in Unity. This is a prototype and the final implementation of DXR will be different from this version. This project can not be used to do any production work.

Requirements:
- NVIDIA RTX series card with the latest drivers [here](https://www.nvidia.com/Download/index.aspx?lang=com)
- Windows 10 RS5 (Build 1809) or later


Install step:
Download the project from Github in the release section, unzip.
Launch Unity.exe
Create a new project and select DXR High Definition RP (Preview)

See usage here: https://github.com/Unity-Technologies/Unity-Experimental-DXR/blob/master/documentation/The%20Experimental%20DXR%20project%20manual.pdf

FAQ:
- This repository use git LFS for large file. Please use "git lfs clone https://github.com/Unity-Technologies/Unity-Experimental-DXR.git" or a git lfs client that is LFS aware to clone repository.
- Downloading source code from repository will not work. Please use https://github.com/Unity-Technologies/Unity-Experimental-DXR/releases zip file instead.
- Windows can't handle more than 260 character for filename. Be sure to install file in a short path (like C:\ or D:\)
