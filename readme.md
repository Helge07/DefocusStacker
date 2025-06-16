# DefocusStacker 
Creates images with blurred background from a stack of images - even for lenses with small aperture 

## Table of contents
- [Introduction](#Introduction)
- [Requirements](#Requirements)
- [Compilation_and_Installation](#Compilation_and_Installation)
- [Usage](#usage)
- [License](#license)

## Introduction
Defocus stacking is a method to produce images with blurred background from a stack of images.The stack
includes a sharp picture of the motif at the top layer and additional pictures beneath which are
increasingly defocused by shifting the focus plane towards the camera. A detailed description of the underlying algorith was published here: https://www.thephotoargus.com/defocus-stacking-how-to-get-bokeh-without-fast-lenses/


`DefocusStacker` is an implementation of the algorithm demonstrating the feasability of the method.
There are two versions: 

– an implemetation for Windows and 

– an implementation for Photoshop


## Requirements
`DefocusStacker` was developed with Visual Studio as a WPF-application based on Net 4.8.

## Compilation_and_Installation
To compile and install the application, follow these steps:
1. Get the repository from https://github.com/Helge07/DefocusStacker/tree/master

2. Unpack the five .zip files (unpacke they are too large for upload into the repository)
   - DFS\DFS\bin\Debug\dll\x64\OpenCvSharpExtern.zip
   - DFS\DFS\bin\Debug\dll\x86\OpenCvSharpExtern.zip
   - DFS\DFS\bin\Release\dll\x64\OpenCvSharpExtern.zip
   - DFS\DFS\bin\Release\dll\x86\OpenCvSharpExtern.zip
   - DFS\DFS\bin\Release\OpenCvSharpExtern.zip

3. Open the project file  EosMonitor\EosMonitor.sln

4. Compile the project `DFS`. This generates the directories
   DFS\DFS\bin\Debug   resp.  DFS\DFS\bin\Release

5. Start EosMonitor.exe  from the Debug- resp. Release-directory 

9. Compiling the project `DFS_Setup` will produce a .msi installation file which can be used to install the application as a Windows application.

## Usage
The user manual `DefocusStacker manual.pdf` for the `DefocusStacker` can be downloaded from the repository.

## License
EosMonitor is published under the GPL-3.0 license. See the LICENSE file for more information. 


