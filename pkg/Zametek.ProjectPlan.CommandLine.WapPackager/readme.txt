Be sure to include the following lines in the .wapproj file:

  <Target Name="ResolveIkvmRuntimeAssembly" />
  <Target Name="_UpdateIkvmReferenceItemsMetadata" />

This will ensure that the IKVN runtime assemblies (IKVM.Runtime.dll) are included in the package when using MPXJ.Net.

See:
https://github.com/joniles/MPXJ.Net/issues/27