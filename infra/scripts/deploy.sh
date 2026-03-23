#!/bin/bash
set -e
cd /home/smakosz
curl -sSL https://raw.githubusercontent.com/SzczepanGrela/SmakoszWebApp/master/docker-compose.yml -o docker-compose.yml
docker compose pull
docker compose up -d
docker image prune -f
