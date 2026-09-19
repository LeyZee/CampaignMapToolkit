rem INFO: This script is used to copy all relevant data and tools for a standalone release

set out_build_dir="CampaignMapToolkit\"

if exist %out_build_dir% rmdir /s /q %out_build_dir%

xcopy /exclude:build_caime_exclude_list.txt CAIME\bin %out_build_dir%\CAIME\
xcopy /e /exclude:build_caime_templates_exclude_list.txt Templates %out_build_dir%\Templates\
rem xcopy /e Projects %out_build_dir%\Projects\
mkdir %out_build_dir%\Projects\
xcopy /e /exclude:build_caime_exclude_list.txt CAIME\Tools %out_build_dir%\Tools\

rem Copy distribution text files (source of truth lives at repo root)
xcopy /Y CREDITS.txt %out_build_dir%\CAIME\
xcopy /Y EULA.txt %out_build_dir%\CAIME\
xcopy /Y LICENSE %out_build_dir%\CAIME\
xcopy /Y README.md %out_build_dir%\CAIME\

rem Copy user guide markdown files (read by the Help > User Guides window)
xcopy /e /Y Docs %out_build_dir%\Docs\

