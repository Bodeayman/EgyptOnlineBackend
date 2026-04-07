#!/bin/bash
set -e
echo "Running Tests..."
dotnet test EgyptOnline.csproj --no-restore --configuration Release
echo "Tests completed successfully!"
