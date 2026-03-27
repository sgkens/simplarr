#!/bin/bash

dotnet publish src/Simplerr.csproj \
  -r linux-x64 \
  -c Release \
  --self-contained \
  -p:PublishSingleFile=true \
  -o ./dist/linux