Be sure to include the following lines in the .wapproj file:

  <Target Name="ResolveIkvmRuntimeAssembly" />
  <Target Name="_UpdateIkvmReferenceItemsMetadata" />

This will ensure that the IKVN runtime assemblies (IKVM.Runtime.dll) are included in the package when using MPXJ.Net.

See:
https://github.com/joniles/MPXJ.Net/issues/27

Also, be sure to test the resulting package using the Windows App Certification Kit (WACK) to ensure that it meets the necessary requirements for distribution in the Microsoft Store.
https://learn.microsoft.com/en-gb/windows/uwp/debug-test-perf/windows-app-certification-kit
