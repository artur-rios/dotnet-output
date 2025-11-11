@echo off

:: Define the path variables
set TEST_RESULTS_PATH=.\tests\TestResults
set TEST_REPORTS_PATH=.\docs\coverage-report
set TESTS_PATH=.\tests

:: Ensure TEST_RESULTS_PATH exists
if not exist "%TEST_RESULTS_PATH%" (
    echo Creating folder: %TEST_RESULTS_PATH%
    mkdir "%TEST_RESULTS_PATH%"
)

:: Ensure TEST_REPORTS_PATH exists
if not exist "%TEST_REPORTS_PATH%" (
    echo Creating folder: %TEST_REPORTS_PATH%
    mkdir "%TEST_REPORTS_PATH%"
)

:: Clear contents of TEST_RESULTS_PATH
echo Cleaning up previous test results...
del /s /q "%TEST_RESULTS_PATH%\*.*" >nul 2>&1
for /d %%i in ("%TEST_RESULTS_PATH%\*") do rd /s /q "%%i"

:: Clear contents of TEST_REPORTS_PATH
echo Cleaning up previous test reports...
del /s /q "%TEST_REPORTS_PATH%\*.*" >nul 2>&1
for /d %%i in ("%TEST_REPORTS_PATH%\*") do rd /s /q "%%i"
echo Clean completed.

echo Running tests and collecting coverage...
:: Find all .csproj files under the ../tests path and run tests
:: Skip the "Setup" folder while running tests
for /r "%TESTS_PATH%" %%f in (*.csproj) do (
    echo %%f | findstr /i "\\Setup\\" >nul
    if errorlevel 1 (
        echo Running tests for project: %%f
        dotnet test "%%f" --collect:"XPlat Code Coverage;Format=json,lcov,cobertura" --results-directory %TEST_RESULTS_PATH%
    ) else (
        echo Skipping project in "Setup" folder: %%f
    )
)

echo Generating reports...
reportgenerator -reports:"%TEST_RESULTS_PATH%\**\coverage.cobertura.xml" -targetdir:"%TEST_REPORTS_PATH%" -reporttypes:Html

echo Coverage report generated in %TEST_REPORTS_PATH%.
