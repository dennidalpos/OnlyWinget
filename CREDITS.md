# Third-party credits

Reviewed 2026-11-19 against the repository's project references and NuGet lockfiles and the self-contained 1.0.3 `win-x64` build from `fc95e1f`. OnlyWinget's own code remains subject to the [project license](LICENSE). The terms below apply to their respective third-party components.

## Redistribution check

- MIT, BSD-3-Clause and Apache-2.0 components can be used in proprietary software under their respective conditions. Redistribute the required license texts and copyright notices, preserve applicable Apache NOTICE attributions, and carry any required change notices. [Apache License 2.0, section 4](https://www.apache.org/licenses/LICENSE-2.0.html)
- The Microsoft Windows App SDK 2.7.0 package license permits redistribution of files placed with the application by its NuGet package, including self-contained deployments, subject to its distribution requirements. Review the separate Windows ML terms and SDK redistributable-file terms for the exact Microsoft files that ship. [Windows App SDK 2.7.0 package](https://www.nuget.org/packages/Microsoft.WindowsAppSDK/2.7.0)
- The setup uses NSIS's LZMA module. Review the CPL-1.0 requirements for that module and retain its applicable license, notices and source availability information. Other NSIS components generally use zlib/libpng terms. [NSIS licenses](https://nsis.sourceforge.io/License)
- The inspected 1.0.3 publish directory and portable ZIP contained no third-party license or notice files. The NSIS setup draws its application files from that publish directory. Before distributing a build, assemble the notices from the exact resolved packages and runtime components and verify their presence in both formats. The inventory in this file records provenance and review scope.

## Resolved NuGet packages

The 83 package/version pairs below come from the locked project graphs and the x64 publish dependency graph. “Transitive app” means a package appears in the publish graph. The exact files in a distribution depend on the target, trimming and native build targets. “Build/test graph” covers versions outside the application publish graph.

| Package | Version | License or terms | Scope |
| --- | --- | --- | --- |
| [`CommunityToolkit.Mvvm`](https://www.nuget.org/packages/CommunityToolkit.Mvvm/8.4.2) | 8.4.2 | MIT | Direct app |
| [`Microsoft.CodeCoverage`](https://www.nuget.org/packages/Microsoft.CodeCoverage/18.12.0) | 18.12.0 | MIT | Build/test graph |
| [`Microsoft.Data.Sqlite.Core`](https://www.nuget.org/packages/Microsoft.Data.Sqlite.Core/10.0.15) | 10.0.15 | MIT | Transitive app |
| [`Microsoft.EntityFrameworkCore`](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore/10.0.15) | 10.0.15 | MIT | Transitive app |
| [`Microsoft.EntityFrameworkCore.Abstractions`](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Abstractions/10.0.15) | 10.0.15 | MIT | Transitive app |
| [`Microsoft.EntityFrameworkCore.Analyzers`](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Analyzers/10.0.15) | 10.0.15 | MIT | Build/test graph |
| [`Microsoft.EntityFrameworkCore.Relational`](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Relational/10.0.15) | 10.0.15 | MIT | Transitive app |
| [`Microsoft.EntityFrameworkCore.Sqlite`](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Sqlite/10.0.15) | 10.0.15 | MIT | Direct app |
| [`Microsoft.EntityFrameworkCore.Sqlite.Core`](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.Sqlite.Core/10.0.15) | 10.0.15 | MIT | Transitive app |
| [`Microsoft.Extensions.Caching.Abstractions`](https://www.nuget.org/packages/Microsoft.Extensions.Caching.Abstractions/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`Microsoft.Extensions.Caching.Memory`](https://www.nuget.org/packages/Microsoft.Extensions.Caching.Memory/11.0.0) | 11.0.0 | MIT | Direct app |
| [`Microsoft.Extensions.Configuration`](https://www.nuget.org/packages/Microsoft.Extensions.Configuration/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`Microsoft.Extensions.Configuration.Abstractions`](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.Abstractions/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`Microsoft.Extensions.Configuration.Abstractions`](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.Abstractions/10.0.15) | 10.0.15 | MIT | Build/test graph |
| [`Microsoft.Extensions.Configuration.Binder`](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.Binder/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`Microsoft.Extensions.Configuration.CommandLine`](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.CommandLine/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`Microsoft.Extensions.Configuration.EnvironmentVariables`](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.EnvironmentVariables/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`Microsoft.Extensions.Configuration.FileExtensions`](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.FileExtensions/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`Microsoft.Extensions.Configuration.Json`](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.Json/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`Microsoft.Extensions.Configuration.UserSecrets`](https://www.nuget.org/packages/Microsoft.Extensions.Configuration.UserSecrets/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`Microsoft.Extensions.DependencyInjection`](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`Microsoft.Extensions.DependencyInjection`](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection/10.0.15) | 10.0.15 | MIT | Build/test graph |
| [`Microsoft.Extensions.DependencyInjection.Abstractions`](https://www.nuget.org/packages/Microsoft.Extensions.DependencyInjection.Abstractions/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`Microsoft.Extensions.DependencyModel`](https://www.nuget.org/packages/Microsoft.Extensions.DependencyModel/10.0.15) | 10.0.15 | MIT | Transitive app |
| [`Microsoft.Extensions.Diagnostics`](https://www.nuget.org/packages/Microsoft.Extensions.Diagnostics/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`Microsoft.Extensions.Diagnostics.Abstractions`](https://www.nuget.org/packages/Microsoft.Extensions.Diagnostics.Abstractions/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`Microsoft.Extensions.FileProviders.Abstractions`](https://www.nuget.org/packages/Microsoft.Extensions.FileProviders.Abstractions/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`Microsoft.Extensions.FileProviders.Physical`](https://www.nuget.org/packages/Microsoft.Extensions.FileProviders.Physical/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`Microsoft.Extensions.FileSystemGlobbing`](https://www.nuget.org/packages/Microsoft.Extensions.FileSystemGlobbing/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`Microsoft.Extensions.Hosting`](https://www.nuget.org/packages/Microsoft.Extensions.Hosting/11.0.0) | 11.0.0 | MIT | Direct app |
| [`Microsoft.Extensions.Hosting.Abstractions`](https://www.nuget.org/packages/Microsoft.Extensions.Hosting.Abstractions/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`Microsoft.Extensions.Logging`](https://www.nuget.org/packages/Microsoft.Extensions.Logging/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`Microsoft.Extensions.Logging`](https://www.nuget.org/packages/Microsoft.Extensions.Logging/10.0.15) | 10.0.15 | MIT | Build/test graph |
| [`Microsoft.Extensions.Logging.Abstractions`](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Abstractions/11.0.0) | 11.0.0 | MIT | Direct app |
| [`Microsoft.Extensions.Logging.Configuration`](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Configuration/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`Microsoft.Extensions.Logging.Console`](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Console/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`Microsoft.Extensions.Logging.Debug`](https://www.nuget.org/packages/Microsoft.Extensions.Logging.Debug/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`Microsoft.Extensions.Logging.EventLog`](https://www.nuget.org/packages/Microsoft.Extensions.Logging.EventLog/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`Microsoft.Extensions.Logging.EventSource`](https://www.nuget.org/packages/Microsoft.Extensions.Logging.EventSource/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`Microsoft.Extensions.Options`](https://www.nuget.org/packages/Microsoft.Extensions.Options/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`Microsoft.Extensions.Options.ConfigurationExtensions`](https://www.nuget.org/packages/Microsoft.Extensions.Options.ConfigurationExtensions/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`Microsoft.Extensions.Primitives`](https://www.nuget.org/packages/Microsoft.Extensions.Primitives/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`Microsoft.NET.ILLink.Tasks`](https://www.nuget.org/packages/Microsoft.NET.ILLink.Tasks/10.0.15) | 10.0.15 | MIT | Build/test graph |
| [`Microsoft.NET.Test.Sdk`](https://www.nuget.org/packages/Microsoft.NET.Test.Sdk/18.12.0) | 18.12.0 | MIT | Direct test |
| [`Microsoft.TestPlatform.ObjectModel`](https://www.nuget.org/packages/Microsoft.TestPlatform.ObjectModel/18.12.0) | 18.12.0 | MIT | Build/test graph |
| [`Microsoft.TestPlatform.TestHost`](https://www.nuget.org/packages/Microsoft.TestPlatform.TestHost/18.12.0) | 18.12.0 | MIT | Build/test graph |
| [`Microsoft.Web.WebView2`](https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.3719.77) | 1.0.3719.77 | BSD-3-Clause terms in LICENSE.txt | Transitive app |
| [`Microsoft.Windows.AI.MachineLearning`](https://www.nuget.org/packages/Microsoft.Windows.AI.MachineLearning/2.5.96) | 2.5.96 | Microsoft Windows ML Runtime terms | Transitive app |
| [`Microsoft.Windows.SDK.BuildTools`](https://www.nuget.org/packages/Microsoft.Windows.SDK.BuildTools/10.0.26100.4654) | 10.0.26100.4654 | Microsoft Windows SDK terms | Build/test graph |
| [`Microsoft.Windows.SDK.BuildTools.MSIX`](https://www.nuget.org/packages/Microsoft.Windows.SDK.BuildTools.MSIX/1.7.251221100) | 1.7.251221100 | Microsoft Windows SDK terms | Build/test graph |
| [`Microsoft.WindowsAppSDK`](https://www.nuget.org/packages/Microsoft.WindowsAppSDK/2.7.0) | 2.7.0 | Microsoft Windows App SDK terms | Direct app |
| [`Microsoft.WindowsAppSDK.AI`](https://www.nuget.org/packages/Microsoft.WindowsAppSDK.AI/2.7.0) | 2.7.0 | Microsoft Windows App SDK terms | Transitive app |
| [`Microsoft.WindowsAppSDK.Base`](https://www.nuget.org/packages/Microsoft.WindowsAppSDK.Base/2.7.0) | 2.7.0 | Microsoft Windows App SDK terms | Build/test graph |
| [`Microsoft.WindowsAppSDK.DWrite`](https://www.nuget.org/packages/Microsoft.WindowsAppSDK.DWrite/2.7.0) | 2.7.0 | Microsoft Windows App SDK terms | Build/test graph |
| [`Microsoft.WindowsAppSDK.Foundation`](https://www.nuget.org/packages/Microsoft.WindowsAppSDK.Foundation/2.7.0) | 2.7.0 | Microsoft Windows App SDK terms | Transitive app |
| [`Microsoft.WindowsAppSDK.InteractiveExperiences`](https://www.nuget.org/packages/Microsoft.WindowsAppSDK.InteractiveExperiences/2.7.0) | 2.7.0 | Microsoft Windows App SDK terms | Transitive app |
| [`Microsoft.WindowsAppSDK.ML`](https://www.nuget.org/packages/Microsoft.WindowsAppSDK.ML/2.7.0) | 2.7.0 | Microsoft Windows App SDK and Windows ML terms | Transitive app |
| [`Microsoft.WindowsAppSDK.Runtime`](https://www.nuget.org/packages/Microsoft.WindowsAppSDK.Runtime/2.7.0) | 2.7.0 | Microsoft Windows App SDK terms | Build/test graph |
| [`Microsoft.WindowsAppSDK.Search`](https://www.nuget.org/packages/Microsoft.WindowsAppSDK.Search/2.7.0) | 2.7.0 | Microsoft Windows App SDK terms | Transitive app |
| [`Microsoft.WindowsAppSDK.Widgets`](https://www.nuget.org/packages/Microsoft.WindowsAppSDK.Widgets/2.7.0) | 2.7.0 | Microsoft Windows App SDK terms | Transitive app |
| [`Microsoft.WindowsAppSDK.WinUI`](https://www.nuget.org/packages/Microsoft.WindowsAppSDK.WinUI/2.7.0) | 2.7.0 | Microsoft Windows App SDK terms | Transitive app |
| [`Serilog`](https://www.nuget.org/packages/Serilog/4.4.0) | 4.4.0 | Apache-2.0 | Transitive app |
| [`Serilog.Extensions.Hosting`](https://www.nuget.org/packages/Serilog.Extensions.Hosting/11.0.0) | 11.0.0 | Apache-2.0 | Direct app |
| [`Serilog.Extensions.Logging`](https://www.nuget.org/packages/Serilog.Extensions.Logging/11.0.0) | 11.0.0 | Apache-2.0 | Transitive app |
| [`SQLite`](https://www.nuget.org/packages/SQLite/3.53.4) | 3.53.4 | Public domain | Transitive app |
| [`SQLitePCLRaw.bundle_e_sqlite3`](https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlite3/3.0.5) | 3.0.5 | Apache-2.0 | Direct app |
| [`SQLitePCLRaw.config.e_sqlite3`](https://www.nuget.org/packages/SQLitePCLRaw.config.e_sqlite3/3.0.5) | 3.0.5 | Apache-2.0 | Transitive app |
| [`SQLitePCLRaw.core`](https://www.nuget.org/packages/SQLitePCLRaw.core/3.0.5) | 3.0.5 | Apache-2.0 | Transitive app |
| [`SQLitePCLRaw.provider.e_sqlite3`](https://www.nuget.org/packages/SQLitePCLRaw.provider.e_sqlite3/3.0.5) | 3.0.5 | Apache-2.0 | Transitive app |
| [`System.Diagnostics.DiagnosticSource`](https://www.nuget.org/packages/System.Diagnostics.DiagnosticSource/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`System.Diagnostics.EventLog`](https://www.nuget.org/packages/System.Diagnostics.EventLog/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`System.IO.Pipelines`](https://www.nuget.org/packages/System.IO.Pipelines/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`System.Numerics.Tensors`](https://www.nuget.org/packages/System.Numerics.Tensors/9.0.0) | 9.0.0 | MIT | Transitive app |
| [`System.Text.Encodings.Web`](https://www.nuget.org/packages/System.Text.Encodings.Web/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`System.Text.Json`](https://www.nuget.org/packages/System.Text.Json/11.0.0) | 11.0.0 | MIT | Transitive app |
| [`xunit`](https://www.nuget.org/packages/xunit/2.9.3) | 2.9.3 | Apache-2.0 | Direct test |
| [`xunit.abstractions`](https://www.nuget.org/packages/xunit.abstractions/2.0.3) | 2.0.3 | Apache-2.0 (legacy license URL) | Build/test graph |
| [`xunit.analyzers`](https://www.nuget.org/packages/xunit.analyzers/1.18.0) | 1.18.0 | Apache-2.0 | Build/test graph |
| [`xunit.assert`](https://www.nuget.org/packages/xunit.assert/2.9.3) | 2.9.3 | Apache-2.0 | Build/test graph |
| [`xunit.core`](https://www.nuget.org/packages/xunit.core/2.9.3) | 2.9.3 | Apache-2.0 | Build/test graph |
| [`xunit.extensibility.core`](https://www.nuget.org/packages/xunit.extensibility.core/2.9.3) | 2.9.3 | Apache-2.0 | Build/test graph |
| [`xunit.extensibility.execution`](https://www.nuget.org/packages/xunit.extensibility.execution/2.9.3) | 2.9.3 | Apache-2.0 | Build/test graph |
| [`xunit.runner.visualstudio`](https://www.nuget.org/packages/xunit.runner.visualstudio/4.0.1) | 4.0.1 | Apache-2.0 | Direct test |

## Runtime, installer and web assets

| Component | Observed version or source | Terms and evidence |
| --- | --- | --- |
| .NET runtime for Windows x64 | 10.0.15 | MIT, with third-party notices in the [runtime pack](https://www.nuget.org/packages/Microsoft.NETCore.App.Runtime.win-x64/10.0.15) |
| Windows SDK .NET projection pack | 10.0.17763.57 | [Microsoft Windows SDK terms](https://aka.ms/WinSDKLicenseURL), declared by the [projection pack](https://www.nuget.org/packages/Microsoft.Windows.SDK.NET.Ref/10.0.17763.57) |
| ONNX Runtime | Native DLL reports 1.28.3.20261013.1.1b92d6e | Brought in by [Microsoft Windows ML](https://www.nuget.org/packages/Microsoft.Windows.AI.MachineLearning/2.5.96). Use the Microsoft package terms and accompanying third-party notices for this build. |
| NSIS and LZMA | 3.13, `SetCompressor /SOLID lzma` | [zlib/libpng and CPL-1.0](https://nsis.sourceforge.io/License), by component |
| Inter and Outfit | Google Fonts stylesheets in the landing site | [Inter SIL OFL 1.1](https://github.com/rsms/inter/blob/master/LICENSE.txt), [Outfit SIL OFL 1.1](https://github.com/google/fonts/blob/main/ofl/outfit/OFL.txt) |
| Font Awesome Free | 6.4.0 via cdnjs | [CC BY 4.0 for icons, SIL OFL 1.1 for fonts and MIT for code](https://github.com/FortAwesome/Font-Awesome/blob/6.4.0/LICENSE.txt) |

WinGet and Windows Update are host services. OnlyWinget calls them on the user's system. The [SQLite core](https://sqlite.org/copyright.html) in the SQLite NuGet package is public domain. The SQLitePCLRaw wrapper has separate Apache-2.0 terms.

## Maintaining this register

When dependencies, runtime packs or bundled assets change, review the project references, lockfiles, package license files and the new x64 publish output. Update the versions and terms here. Record package notices and licenses for any new native files and check the exact setup and ZIP during [release verification](docs/release.md#artifact).
