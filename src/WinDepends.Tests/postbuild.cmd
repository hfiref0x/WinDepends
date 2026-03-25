echo ----------------------------------------------
echo %1 post-build script
echo ----------------------------------------------

IF EXIST %2 (
    Echo Copy %2 to Bin folder
    copy %2 ..\Bin /y
 ) ELSE ( 
    echo %2 dll file was not found, skipping
 )

IF EXIST %3 (
    Echo Copy %3 to Bin folder
    copy %3 ..\Bin /y 
 ) ELSE ( 
    echo %3 lib file was not found, skipping
 )
