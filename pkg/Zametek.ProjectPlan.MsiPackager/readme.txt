Before you build the package, publish the Windows x64 binaries of the desktop application and the command line tool. Use the makefile:

  make publish-desktop publish-cli ARCH=x64 OS=win

Then build this project with the Release configuration and the x64 platform. The installer contains the x64 binaries of the publish. Thus, x64 is the only platform of the project. The build stops with an error in these conditions:

- The platform is not x64.
- A file of the publish is not there.
- A file under src, or a build file in the root folder, is newer than the publish. Then the publish is from older code, and you must publish again.
