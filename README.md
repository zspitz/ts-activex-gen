# ts-activex-gen
Library and WPF UI for generating Typescript definitions from COM type libraries, either from the Windows Registry, or from files on disk.

Optionally, definitions can be packaged for publication on DefinitelyTyped, as outlined [here](https://github.com/DefinitelyTyped/DefinitelyTyped) and [here](http://www.typescriptlang.org/docs/handbook/declaration-files/publishing.html).

### UI

![Choosing a registerd COM library](https://raw.githubusercontent.com/zspitz/ts-activex-gen/master/screenshot.png)

### Library

Generating Typescript with the library looks like this:

```csharp
var generator = new TlbInf32Generator();

var args = new {
  tlbid = "{420B2830-E718-11CF-893D-00A0C9054228}",
  majorVersion = 1,
  minorVersion = 1,
  lcid = 0
};
generator.AddFromRegistry(args.tlbid, args.majorVersion, args.minorVersion, args.lcid);
//All the arguments except for the TLBID are optional

//Also add a type library from a file
generator.AddFromFile(@"c:\path\to\file.dll");

//multiple files / registered libraries can be added

//TSNamespaceSet describes a set of Typescript namespaces
TSNamespaceSet namespaceSet = generator.NSSet;

var builder = new TSBuilder();
NamespaceOutput output = builder.GetTypescript(namespaceSet);
string typescriptDefinitions = output.MainFile;
string typescriptTestsFileStub = output.TestsFile;
```

### Event handlers and parameterized setters

Standard Javascript doesn't support the Microsoft JScript-specific syntax used for registering event handlers (`objectname::eventname`) and property setters with parameters (`object.Item(1) = 1`). [This library](https://github.com/zspitz/activex-js-helpers) allows the use pf standard JS for these tasks. Generated definitions include overloads that leverage the library.

```
interface ActiveXObject {
    ...
    on(obj: Word.Application, eventName: 'DocumentBeforeClose', eventArgs: ['Doc','Cancel'], handler: (this: Word.Application, parameter: {Doc: Word.Document,Cancel: boolean}) => void): void;
    on(obj: Word.Application, eventName: 'DocumentBeforeSave', eventArgs: ['Doc','SaveAsUI','Cancel'], handler: (this: Word.Application, parameter: {Doc: Word.Document,SaveAsUI: boolean,Cancel: boolean}) => void): void;
    on(obj: Word.Application, eventName: 'DocumentChange', handler: (this: Word.Application, parameter: {}) => void): void;
    on(obj: Word.Application, eventName: 'DocumentOpen', eventArgs: ['Doc'], handler: (this: Word.Application, parameter: {Doc: Word.Document}) => void): void;
    ...
    set(obj: Word.Document, propertyName: 'ActiveWritingStyle', parameterTypes: [any], newValue: string): void;
    ...
```
