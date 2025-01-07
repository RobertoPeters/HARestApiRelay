@echo off

rem =========================================================
rem =                                                       =
rem =             Publish script HARestApiRelay             =
rem =                                                       =
rem =========================================================
dotnet publish -c Release -r linux-musl-x64 --self-contained true -o DeployLinux