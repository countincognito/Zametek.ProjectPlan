Before you build the package, publish the Windows x64 binaries of the desktop application and the command line tool. Use the makefile:

  make publish-desktop publish-cli ARCH=x64 OS=win

Then build this project with the Release configuration and the x64 platform. The installer contains the x64 binaries of the publish, whatever the platform is. Thus, do not use the platforms x86 and ARM64.
