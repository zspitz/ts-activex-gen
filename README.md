# ts-activex-gen
Library and WPF UI for generating Typescript definitions from COM type libraries or WMI classes

### UI

![Choosing a registerd COM library, and previewing the definitions](https://raw.githubusercontent.com/zspitz/ts-activex-gen/master/screenshot.png)

### Library

Generating Typescript with the library looks like this:

```csharp
ITSNamespaceGenerator generator = new TlbInf32Generator();

var args = new {
  tlbid = "{420B2830-E718-11CF-893D-00A0C9054228}",
  majorVersion = 1,
  minorVersion = 1,
  lcid = 0
};
generator.AddFromRegistry(args.tlbid, args.majorVersion, args.minorVersion, args.lcid);

//alternative overload allows passing the path to a COM server
//generator.AddFromFile(@"c:\path\to\file.dll");

//multiple files / registry entries can be added

//TSNamespaceSet describes a set of Typescript namespaces
TSNamespaceSet namespaceSet = generator.Generate();

var builder = new TSBuilder();
string ts = builder.GetTypescript(namespaceSet);
```

### Some caveats
1. ActiveX objects can have parameterized properties, both getters and setters:

  ```javascript
  var dict = new ActiveXObject('Scripting.Dictionary');
  dict.Add('a',1);
  dict.Add('b',2);
  
  //getter with parameters
  WScript.Echo(dict.Item('a')); //prints 1
  
  //setter with parameters
  dict.Item('a') = 1;
  ```

  The syntax for using property setters with parameters is not valid Typescript, and thus are not included in the definitions. (Parameterized property getters use invocation syntax, which is not a problem.) See [here](https://github.com/Microsoft/TypeScript/issues/956#issuecomment-230396498) for a possible workaround.

2. There is no simple, environment-independent technique for handling Automation events in JScript, and so this is also not reflected in the generated definitions. See [Scripting Events](https://msdn.microsoft.com/en-us/library/ms974564.aspx?f=255&MSPPError=-2147217396) for further details, and [here](https://github.com/zspitz/activex-js-events) for a summary of the issues and a library to work around the,=m.

3. Enum values (and constants in modules) are not really accessible via Javascript; only instance members of an object created via `new ActiveXObject(progID)` are accessible. When writing code in Javascript, the actual values must be used:

  ```javascript
  var dict=new ActiveXObject('Scripting.Dictionary');
  //The CompareMode property is defined as type CompareMethod, with values BinaryCompare = 0, DatabaseCompare = 2 and TextCompare = 1
  //However, there is no way to access these values from Javascript; we have to use the numeric literals instead
  dict.CompareMode=1;
  ```

  For numeric types we can work around this with `const enum`, which can be assigned values in ambient contexts. The actual values will be used on compilation:

  ```typescript
  //microsoft-scripting-runtime.d.ts
  declare namespace Scripting {
    const enum CompareMethod {
      BinaryCompare = 0;
      DatabaseCompare = 2;
      TextCompare = 1;
    }
  }
  //test.ts
  dict.CompareMode = Scripting.CompareMethod.TextCompare;
  ```

  will compile to Javascript as:

  ```javascript
  dict.CompareMode = 1;
  ```

  Currently, for other enum types, and modules with constants, the cloeset we can come is a union type:

  ```typescript
  //declaration
  type CommandID = 
    "{04E725B0-ACAE-11D2-A093-00C04F72DC3C}" //wiaCommandChangeDocument
    | "{E208C170-ACAD-11D2-A093-00C04F72DC3C}" //wiaCommandDeleteAllItems
    | "{9B26B7B2-ACAD-11D2-A093-00C04F72DC3C}" //wiaCommandSynchronize
    | "{AF933CAC-ACAD-11D2-A093-00C04F72DC3C}" //wiaCommandTakePicture
    | "{1F3B3D8E-ACAE-11D2-A093-00C04F72DC3C}"; //wiaCommandUnloadDocument
  
  //if cmd is not one of the values, the code will not compile
  var cmd: CommandID = "{E208C170-ACAD-11D2-A093-00C04F72DC3C}";
  ```
