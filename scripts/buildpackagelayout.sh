if ! [ -d ../artifacts/build/dotnet ]; then
  mkdir -p ../artifacts/build/dotnet
fi

if ! [ -d ../artifacts/build/portable-net45+win+wpa81+wp80+MonoAndroid10+xamarinios10+MonoTouch10 ]; then
  mkdir -p ../artifacts/build/portable-net45+win+wpa81+wp80+MonoAndroid10+xamarinios10+MonoTouch10
fi

if ! [ -d ../artifacts/tools ]; then
  mkdir -p ../artifacts/tools
fi

chmod 755 ../packages/ILRepack.2.0.8/tools/ILRepack.exe

mono "../packages/ILRepack.2.0.8/tools/ILRepack.exe" "../src/ReferenceGenerator/bin/$1/ReferenceGenerator.exe" "../src/ReferenceGenerator/bin/$1/System.Collections.Immutable.dll" "../src/ReferenceGenerator/bin/$1/System.Reflection.Metadata.dll" "../src/ReferenceGenerator/bin/$1/Newtonsoft.Json.dll" /out:"../artifacts/tools/RefGen.exe" /internalize /targetplatform:"v4,/Library/Frameworks/Mono.framework/Versions/Current/lib/mono/xbuild-frameworks/.NETPortable/v4.5"

cp -f "../src/ReferenceGenerator/bin/$1/ReferenceGenerator.exe.config" "../artifacts/tools/RefGen.exe.config"
cp -f "../src/ReferenceGenerator/NuSpec.ReferenceGenerator.targets" "../artifacts/build/dotnet/NuSpec.ReferenceGenerator.targets"
cp -f "../src/ReferenceGenerator/NuSpec.ReferenceGenerator.targets" "../artifacts/build/portable-net45+win+wpa81+wp80+MonoAndroid10+xamarinios10+MonoTouch10/NuSpec.ReferenceGenerator.targets"