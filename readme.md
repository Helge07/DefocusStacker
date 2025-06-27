# DefocusStacker 
The `DefocusStacker` algorithm creates images with blurred background from a stack of images - even for lenses with small aperture 

## Table of contents
- [Introduction](#Introduction)
- [Requirements](#Requirements)
- [Compilation_and_Installation](#Compilation_and_Installation)
- [Usage](#usage)
- [License](#license)

## Introduction
Defocus stacking is a method to produce images with blurred background from a stack of images.The stack
includes a sharp picture of the motif at the top layer and additional pictures beneath which are
increasingly defocused by shifting the focus plane towards the camera. A detailed description of the underlying algorithm was published here: https://www.thephotoargus.com/defocus-stacking-how-to-get-bokeh-without-fast-lenses/


`DefocusStacker` is an implementation of the algorithm demonstrating the feasability of the method.
There are two versions: 

– an implemetation for Windows
  downloadable from this repository

– an implementation for Photoshop downloadable from  https://www.dropbox.com/s/4h896z36wimsfcr/DefocusStacker%28PS%29.zip?dl=0


## Requirements
`DefocusStacker` was developed with Visual Studio as a WPF-application based on Net 4.8.

## Compilation_and_Installation
To compile and install the application, follow these steps:
1. Get the repository from https://github.com/Helge07/DefocusStacker/tree/master.
   Unpack the archive 'DefocusStacker-master.zip' to a directory p.e. 'DefocusStacker-master'

3. Unpack the five .zip files (unpacked they are too large for upload into the repository)
   - DefocusStacker-master\DFS\bin\Debug\dll\x64\OpenCvSharpExtern.zip
   - DefocusStacker-master\DFS\bin\Debug\dll\x86\OpenCvSharpExtern.zip
   - DefocusStacker-master\DFS\bin\Release\dll\x64\OpenCvSharpExtern.zip
   - DefocusStacker-master\DFS\bin\Release\dll\x86\OpenCvSharpExtern.zip
   - DefocusStacker-master\DFS\bin\Release\OpenCvSharpExtern.zip

4. To Open the project in Visual Studio click on the file  'DefocusStacker-master\DFS.sln' then select the 'Debug' or 'Release' version.

5. Compile the project `DFS`. This generates the compiled program in the directory  'DefocusStacker-master\DFS\bin\Debug'   resp.  'DefocusStacker-master\DFS\bin\Release'

6. Start DFS.exe  from the directory
   'DefocusStacker-master\DFS\bin\Debug'   resp.  'DefocusStacker-master\DFS\bin\Release'

10. Compiling the project `DFS_Setup` will produce .msi installation files which can be used to install the application as a Windows application.

## Usage
The user manual `DefocusStacker manual.pdf` can be downloaded from the repository.

## DefocusStacker on GitHub Pages
- https://helge07.github.io/DefocusStacker/

## License
`DefocusStacker` is published under the GPL-3.0 license. See the LICENSE file for more information. 


