@ECHO OFF

SET Src=src
RMDIR /S /Q "%Src%\DeployLinux" >NUL

ECHO.
ECHO ** Publish Src for Linux
ECHO.
pushd %Src%
CALL publishLinux.bat >NUL
popd

ECHO.
ECHO ** Checking Src for Linux
ECHO.
IF NOT EXIST "%Src%\DeployLinux\HARestApiRelay.dll" (
  ECHO Camas Release build not found "%Src%\DeployLinux\HARestApiRelay.dll"
  GOTO ERROR
)

ECHO.
ECHO ** Creating docker image
ECHO.
docker compose down
docker build -f Dockerfile -t robertpeters/harestapirelay:dev .
docker compose up -d

GOTO SUCCESS


:ERROR
ECHO.
ECHO !! ERROR - Error occured
GOTO END

:SUCCESS
GOTO END

:END
ECHO.
PAUSE