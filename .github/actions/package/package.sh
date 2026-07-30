#!/bin/bash

outputFolder=_output
artifactsFolder=_artifacts
uiFolder="$outputFolder/UI"
framework="${FRAMEWORK:=net8.0}"

rm -rf $artifactsFolder
mkdir -p $artifactsFolder

for runtime in _output/*
do
  name="${runtime##*/}"
  folderName="$runtime/$framework"
  radarrFolder="$folderName/Radarr"
  archiveName="Radarr.$BRANCH.$RADARRVERSION.$name"

  if [ ! -d "$radarrFolder" ]; then
    continue
  fi
    
  echo "Creating package for $name"

  echo "Copying UI"
  cp -r $uiFolder $radarrFolder
  
  echo "Setting permissions"
  find $radarrFolder -name "ffprobe" -exec chmod a+x {} 2>/dev/null || true
  find $radarrFolder -name "Radarr" -exec chmod a+x {} 2>/dev/null || true
  find $radarrFolder -name "Radarr.Update" -exec chmod a+x {} 2>/dev/null || true
  
  if [[ "$name" == *"osx"* ]]; then
    echo "Creating macOS package"
      
    packageName="$name-app"
    packageFolder="$outputFolder/$packageName"
      
    rm -rf $packageFolder
    mkdir -p $packageFolder
      
    cp -r distribution/osx/Radarr.app $packageFolder
    mkdir -p $packageFolder/Radarr.app/Contents/MacOS
      
    echo "Copying Binaries"
    cp -r $radarrFolder/* $packageFolder/Radarr.app/Contents/MacOS
      
    echo "Removing Update Folder"
    rm -rf $packageFolder/Radarr.app/Contents/MacOS/Radarr.Update
              
    echo "Packaging macOS app Artifact"
    (cd $packageFolder; zip -rq "../../$artifactsFolder/$archiveName-app.zip" ./Radarr.app)
  fi

  echo "Packaging Artifact"
  if [[ "$name" == *"linux"* ]] || [[ "$name" == *"osx"* ]] || [[ "$name" == *"freebsd"* ]]; then
    tar -zcf "./$artifactsFolder/$archiveName.tar.gz" -C $folderName Radarr
  fi
    
  if [[ "$name" == *"win"* ]]; then
    if [ "$RUNNER_OS" = "Windows" ]; then
      (cd $folderName; 7z a -tzip "../../../$artifactsFolder/$archiveName.zip" ./Radarr)
    else
      (cd $folderName; zip -rq "../../../$artifactsFolder/$archiveName.zip" ./Radarr)
    fi
  fi
done

# Copy Inno Setup Windows installers if present
if compgen -G "distribution/windows/setup/output/Radarr.*.exe" > /dev/null; then
  cp distribution/windows/setup/output/Radarr.*.exe _artifacts/
fi
