dotnet publish src/Simplerr.csproj `
  -r win-x64 `
  -c Release `
  --self-contained `
  -p:PublishSingleFile=true `
  -o ./dist/windows