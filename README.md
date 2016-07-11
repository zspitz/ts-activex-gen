# ts-activex-gen
Library and WPF UI for generating Typescript definitions from COM type libraries or WMI classes

![Choosing a registerd COM library, and previewing the definitions](https://raw.githubusercontent.com/zspitz/ts-activex-gen/master/screenshot-wia.png)

### Library

Generating Typescript with the library looks like this:

```csharp
var args = new {
  tlbid = "{420B2830-E718-11CF-893D-00A0C9054228}",
  majorVersion = 1,
  minorVersion = 1,
  lcid = 0
};
//TSNamespace describes the Typescript types in a single namespace
TSNamespace ns = new TlbInf32Generator(args.tlbid, args.majorVersion, args.minorVersion, args.lcid);
//alternative overload allows passing the path to a COM server

var builder = new TSBuilder();
string ts = builder.GetTypescript(ns, null);
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

2. There is no simple, environment-independent technique for handling Automation events in JScript, and so this is also not reflected in the generated definitions. See [Scripting Events](https://msdn.microsoft.com/en-us/library/ms974564.aspx?f=255&MSPPError=-2147217396) for further details.

3. Typescript enums are currently number-based, pending [#3192](https://github.com/Microsoft/TypeScript/issues/3192); there are some workarounds for string-based enums. This project does [the following](https://github.com/Microsoft/TypeScript/issues/3192#issuecomment-181363162) (@dsherret):
  ```typescript
  type CommandID = 
    "{04E725B0-ACAE-11D2-A093-00C04F72DC3C}" //wiaCommandChangeDocument
    | "{E208C170-ACAD-11D2-A093-00C04F72DC3C}" //wiaCommandDeleteAllItems
    | "{9B26B7B2-ACAD-11D2-A093-00C04F72DC3C}" //wiaCommandSynchronize
    | "{AF933CAC-ACAD-11D2-A093-00C04F72DC3C}" //wiaCommandTakePicture
    | "{1F3B3D8E-ACAE-11D2-A093-00C04F72DC3C}" //wiaCommandUnloadDocument
  const CommandID: {
      wiaCommandChangeDocument: CommandID, 
      wiaCommandDeleteAllItems: CommandID, 
      wiaCommandSynchronize: CommandID, 
      wiaCommandTakePicture: CommandID, 
      wiaCommandUnloadDocument: CommandID
  }
  ```
