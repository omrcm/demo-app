#!/bin/bash
/app/summon -e ${ENV_SELECTOR} --provider /app/summon-conjur -f /app/secrets.yml dotnet VK.Fx.DataFeed.ApiHost.dll