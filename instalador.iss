[Setup]
AppName=PbPb Downloader
AppVersion=1.0
DefaultDirName={pf}\PbPb Downloader
DefaultGroupName=PbPb Downloader
OutputBaseFilename=Instalador_PbPbDownloader
OutputDir=C:\Installers
Compression=lzma
SolidCompression=yes
WizardStyle=modern

[Files]
; Copiar o executável principal
Source: "E:\Desktop\MasterDownloader\MasterDownloader\bin\Debug\net8.0-windows\PbPb Downloader.exe"; DestDir: "{app}"; Flags: ignoreversion

; Copiar o arquivo download.txt (se quiser incluir um inicial)
Source: "E:\Desktop\MasterDownloader\MasterDownloader\bin\Debug\net8.0-windows\download.txt"; DestDir: "{app}"; Flags: ignoreversion

; Source: "E:\Desktop\MasterDownloader\MasterDownloader\bin\Debug\net8.0-windows\PbPb Downloader.dll"; DestDir: "{app}"; Flags: ignoreversion

; Copiar toda a pasta "app" (yt-dlp.exe, ffmpeg.exe, etc)
Source: "E:\Desktop\MasterDownloader\MasterDownloader\bin\Debug\net8.0-windows\app\*"; DestDir: "{app}\app"; Flags: recursesubdirs createallsubdirs ignoreversion

[Dirs]
Name: "{app}\downloaded"


[Icons]
Name: "{group}\PbPb Downloader"; Filename: "{app}\PbPb Downloader.exe"
Name: "{commondesktop}\PbPb Downloader"; Filename: "{app}\PbPb Downloader.exe"; Tasks: desktopicon
Name: "{group}\Desinstalar PbPb Downloader"; Filename: "{uninstallexe}"

[Tasks]
Name: "desktopicon"; Description: "Criar atalho na área de trabalho"; GroupDescription: "Opções adicionais:"

[Run]
Filename: "{app}\PbPb Downloader.exe"; Description: "Executar PbPb Downloader agora"; Flags: nowait postinstall skipifsilent
