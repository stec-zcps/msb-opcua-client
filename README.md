# msb-opcua-client

OPCUA-Client to MSB-Websocket-Client, very early stage

written as .NET-Core-3.1 application

for build lookup publish.ps1 and publishsinglefile.ps1 scripts

able to read and write nodes (MSB functions), monitor nodes (MSB events), call methods (MSB functions, without parameters currently)

missing:
- security stuff like credentials usage, certificate validification

- differing subscriptions according to specified subscription update intervals

- parameter support for opcua methods

using
https://github.com/OPCFoundation/UA-.NETStandard

https://github.com/research-virtualfortknox/msb-client-websocket-csharp