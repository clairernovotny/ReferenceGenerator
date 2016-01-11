#!/usr/bin/env bash

if ! [ -x "$(command -v mono)" ]; then
  echo >&2 "Could not find 'mono' on the path."
  exit 1
fi

if ! [ -x "$(command -v curl)" ]; then
  echo >&2 "Could not find 'curl' on the path."
  exit 1
fi

if ! [ -d ../.nuget ]; then
  mkdir ../.nuget
fi

if ! [ -x ../.nuget/nuget.exe ]; then
  echo ""
  echo "Downloading nuget.exe..."
  echo ""

  curl https://dist.nuget.org/win-x86-commandline/latest/nuget.exe -o ../.nuget/nuget.exe -L
  if [ $? -ne 0 ]; then
    echo >&2 ""
    echo >&2 "The download of nuget.exe has failed."
    exit 1
  fi

  chmod 755 ../.nuget/nuget.exe
fi

echo ""
echo "Restoring NuGet packages..."
echo ""

mono ../.nuget/nuget.exe restore ../ReferenceGenerator.Mono.sln
if [ $? -ne 0 ]; then
  echo >&2 "NuGet package restore has failed."
  exit 1
fi

echo ""
echo "Building..."
echo ""

xbuild ../ReferenceGenerator.Mono.sln /property:Configuration=Release
if [ $? -ne 0 ]; then
  echo >&2 ""
  echo >&2 "The build has failed."
  exit 1
fi