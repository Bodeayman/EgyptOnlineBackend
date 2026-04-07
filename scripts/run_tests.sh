#!/bin/bash
set -e
echo "Running Tests..."
dotnet test EgyptOnline.sln --no-restore --configuration Release
echo "Tests completed successfully!"
