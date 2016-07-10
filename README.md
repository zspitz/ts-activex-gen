# ts-activex-gen
Library and WPF UI for generating Typescript definitions from COM type libraries or WMI classes

(screenshot here)

# Some caveats
1. ActiveX objects can have property getters and setters with parameters:

  ```javascript
  var dict = new ActiveXObject('Scripting.Dictionary');
  dict.Add('a',1);
  dict.Add('b',2);
  
  //getter with parameters
  WScript.Echo(dict.Item('a')); //prints 1
  
  //setter with parameters
  dict.Item('a') = 1;
  ```

  The syntax for using property setters with parameters is not valid Typescript, and thus are not included in the definitions. (Parameterized property getters use invocation syntax, which is not a problem.)

2. There is no simple, environment independent technique for handling Automation events in JScript, and so this is also not reflected in the generated definitions. See [Scripting Events](https://msdn.microsoft.com/en-us/library/ms974564.aspx?f=255&MSPPError=-2147217396) for further details.
