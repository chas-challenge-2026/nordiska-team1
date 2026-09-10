#!/bin/bash
docker compose -p nordiska-v2 -f infra/v2/docker-compose.yml down -v
dotnet run --project ./backend/tools/Nordiska.DevSetup/Nordiska.DevSetup.csproj -- .
dotnet run --project ./backend/src/Nordiska.FrontendApi/Nordiska.FrontendApi.csproj --launch-profile http


# chmod +x run_dev.sh