Include these lines in the .wapproj file:

  <Target Name="ResolveIkvmRuntimeAssembly" />
  <Target Name="_UpdateIkvmReferenceItemsMetadata" />

These lines put the IKVM runtime assemblies (IKVM.Runtime.dll) in the package. This is necessary when the application uses MPXJ.Net.

For more information, see:
https://github.com/joniles/MPXJ.Net/issues/27

For the checks for app certification, see:
https://learn.microsoft.com/en-gb/windows/uwp/debug-test-perf/windows-app-certification-kit
