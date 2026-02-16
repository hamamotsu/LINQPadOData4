# LINQPadOData4

Simple OData v4 LINQPad dynamic driver.

> Forked from [meancrazy/LINQPadOData4](https://github.com/meancrazy/LINQPadOData4) and updated for LINQPad 9 / .NET 10.

## Features

- Connect to OData v4 endpoints from LINQPad
- Auto-generate strongly-typed C# classes from `$metadata`
- Browse entity sets, properties, and navigation properties in the schema explorer
- Support for Basic / Windows / Client Certificate authentication
- Custom HTTP headers, proxy, and invalid SSL certificate acceptance

## Compatibility

| LINQPad | .NET | Driver |
|---------|------|--------|
| 9.x     | 10   | This release (`OData4.LINQPadDriver.Net10`) |
| 8.x     | 8    | [Original NuGet package v2.0.0](https://www.nuget.org/packages/OData4.LINQPadDriver/) |

## Installation

### LPX6 (recommended for LINQPad 9)

1. Download `OData4.LINQPadDriver.Net10.lpx6` from the [Releases](https://github.com/hamamotsu/LINQPadOData4/releases) page
2. Drag & drop into LINQPad 9, or double-click the file

### Build from source

```bash
dotnet build -c Release
```

The post-build step copies the output to `%LocalAppData%\LINQPad\Drivers\DataContext\NetCore\OData4.LINQPadDriver.Net10\` for local testing.

## Key Dependencies

- [Microsoft.OData.Client](https://www.nuget.org/packages/Microsoft.OData.Client/) 8.4.3
- [Microsoft.CodeAnalysis.CSharp](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp/) 4.14.0
- [LINQPad.Reference](https://www.nuget.org/packages/LINQPad.Reference/) 1.3.1

## License

[MIT](LICENSE.md) - Original copyright (c) Dmitrii Smirnov
