# ts-activex-gen
Library and WPF UI for generating Typescript definitions from COM type libraries, either from the Windows Registry, or from files on disk. Also allows generating definitions for the LibreOffice API (WIP).

Optionally, definitions can be packaged for publication on DefinitelyTyped, as outlined [here](https://github.com/DefinitelyTyped/DefinitelyTyped) and [here](http://www.typescriptlang.org/docs/handbook/declaration-files/publishing.html).

## UI

![Choosing a registerd COM library](https://raw.githubusercontent.com/zspitz/ts-activex-gen/master/screenshot.png)

## Library

The first step is to generate an instance of `TSNamespaceSet`, which is a data structure describing a set of TypeScript namespaces, interfaces, enums, and their members (see below).
```csharp
TSNamespaceSet nsset = ...
```
That instance is passed to the `GetTypescript` method:
```csharp
var builder = new TSBuilder();
List<KeyValuePair<string, NamespaceOutput>> output = builder.GetTypescript(nsset);
foreach (var x in output) {
  var name = x.Key;
  string typescriptDefinitions = x.Value.MainFile;
  string typescriptTestsFileStub = x.Value.TestsFile;
  ...
}
```

### Generating a `TSNamespaceSet` for COM type libraries

To generate a` TSNamespaceSet` from a COM type library, use `TlbInf32Generator`:
```csharp
var generator = new TlbInf32Generator();
```
To add a library from the registry:
```csharp
var args = new {
  tlbid = "{420B2830-E718-11CF-893D-00A0C9054228}",
  majorVersion = 1,
  minorVersion = 1,
  lcid = 0
};
generator.AddFromRegistry(args.tlbid, args.majorVersion, args.minorVersion, args.lcid);
```
All arguments except for the TLBID are optional.
The highest registered version in the registry with the matching details will be used.

To add a library from a file:
```csharp
generator.AddFromFile(@"c:\path\to\file.dll");
```

To add a library based on keywords in the name (case-insensitive)
```csharp
generator.AddFromKeywords(new [] {"microsoft word", "microsoft excel"});
```

Multiple files / registered libraries can be added.

If a library references an external library, the external library will also be added to the namespace set. For example, since the Microsoft Word obejct library uses types from the Microsoft Office shared object library, the namespace set will contain both Microsoft Word and Microsoft Office object libraries.

To get the `TSNamespaceSet`:
```csharp
TSNamespaceSet nsset = generator.NSSet;
```

### Event handlers and parameterized setters

Standard Javascript doesn't support the Microsoft JScript-specific syntax used for registering event handlers on ActiveX objects (`objectname::eventname`) and for property setters with parameters (`object.Item(1) = 1`). [activex-js-helpers](https://github.com/zspitz/activex-js-helpers) allows the use of standard JS for these tasks. Generated definitions include overloads that leverage the library.

```typescript
interface ActiveXObject {
    ...
    on(obj: Word.Application, eventName: 'DocumentBeforeClose', eventArgs: ['Doc','Cancel'], handler: (
        this: Word.Application, parameter: {Doc: Word.Document,Cancel: boolean}) => void): void;
    on(obj: Word.Application, eventName: 'DocumentBeforeSave', eventArgs: ['Doc','SaveAsUI','Cancel'], handler: (
        this: Word.Application, parameter: {Doc: Word.Document,SaveAsUI: boolean,Cancel: boolean}) => void): void;
    on(obj: Word.Application, eventName: 'DocumentChange', handler: (
        this: Word.Application, parameter: {}) => void): void;
    on(obj: Word.Application, eventName: 'DocumentOpen', eventArgs: ['Doc'], handler: (
        this: Word.Application, parameter: {Doc: Word.Document}) => void): void;
    ...
    set(obj: Word.Document, propertyName: 'ActiveWritingStyle', parameterTypes: [any], newValue: string): void;
    ...
```

### Generating a `TSNamespace` for the LibreOffice API

LibreOffice supports generating documentation using Doxygen, which provides an intermediate XML format. Using the outputted XML, the following is possible:
```csharp
var generator = new DoxygenIDLBuilder(@"c:\path\to\xml\files", Context.Automation);
TSNamespaceSet nsset = generator.NSSet;
var builder = new TSBuilder();
NamespaceOutput output = builder.GetTypescript(nsset);
string typescriptDefinitions = output.MainFile;
```
Theoretically, definitions could be useful in three contexts:
* JScript via Automation under WSH
* Javascript macros embedded in LibreOffice documents, or the local LibreOffice instance
* Document / application manipulation under NodeJS (I don't know if this is even possible)

Each context has specific details of the available type mappings. For example, under Automation a method which expects the native `sequence<int>` can also take a `SafeArray<number>`; the Automation bridge is responsible for converting between the two types. However, in embedded Javascript macros, there is no concept of a `SafeArray<T>`, and therefore the definitions will be different.

Currently only definitions for the Automation context are implemented.
