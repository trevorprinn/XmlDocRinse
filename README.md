# XmlDocRinse
Command line app to remove internal information from XML documentation files produced by Visual Studio.

The XML documentation produced by Visual Studio to provide Intellisense about an assembly includes information about classes, methods etc. that are not exported from the assembly. This is useful for someone who is working on the assembly, but not necessarily a great idea when the assembly is being distributed. This program looks through the assembly and removes items from the XML file that aren't exported.

To run it, use
```
XmlDocRinse assembly-filename [xml-filename]
```  
for example
```
XmlDocRinse Babbacombe.Logger.dll
```

It creates a backup of the original xml file.