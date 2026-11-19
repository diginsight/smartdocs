# smartdocs

diginsight smartdocs provides **hyper scale**, **dynamic**, **multisource** markdown rendering 

please check 
[Diginsight SmartDocs](https://docs-testmc-app-itn-01.azurewebsites.net/)

<img width="1347" height="1020" alt="image" src="https://github.com/user-attachments/assets/8f5732f7-0d1e-4a64-a807-c1700654e8a2" />

## ⚙️ External role providers

Set `Site__Authorization__RoleProvider__IsEnabled=false` to turn off a configured external role-provider plugin without clearing its assembly, module, or settings. SmartDocs uses Entra token roles instead, while sign-in and repository permission policies remain enforced. The [provider options](src/Diginsight.SmartDocs.Web/Authorization/RoleProviderOptions.cs) default to enabled for backward compatibility; a plugin still requires an `Assembly`. The switch is applied at startup. Restart and sign out/sign in to replace existing role snapshots; it doesn't revoke existing cookies.
