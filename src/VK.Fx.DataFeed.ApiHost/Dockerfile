#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM quay.vakifkatilim.local/base/vk.aspnetcore.runtime:5.0-latest AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 443

FROM quay.vakifkatilim.local/base/vk.aspnetcore.sdk:5.0-latest  AS build
WORKDIR /src

ARG BRANCH_NAME
ARG NUGET_SOURCE=vk-ms-emar

COPY ["src/VK.Fx.DataFeed.ApiHost/VK.Fx.DataFeed.ApiHost.csproj", "src/VK.Fx.DataFeed.ApiHost/"]
COPY ["src/VK.Fx.DataFeed.Application/VK.Fx.DataFeed.Application.csproj", "src/VK.Fx.DataFeed.Application/"]
COPY ["src/VK.Fx.DataFeed.Application.Contract/VK.Fx.DataFeed.Application.Contract.csproj", "src/VK.Fx.DataFeed.Application.Contract/"]
COPY ["src/VK.Fx.DataFeed.Domain/VK.Fx.DataFeed.Domain.csproj", "src/VK.Fx.DataFeed.Domain/"]
COPY ["src/VK.Fx.DataFeed.Domain.Shared/VK.Fx.DataFeed.Domain.Shared.csproj", "src/VK.Fx.DataFeed.Domain.Shared/"]
COPY ["src/VK.Fx.DataFeed.BoaProxy/VK.Fx.DataFeed.BoaProxy.csproj", "src/VK.Fx.DataFeed.BoaProxy/"]

RUN dotnet restore "src/VK.Fx.DataFeed.ApiHost/VK.Fx.DataFeed.ApiHost.csproj" -s http://artifact.vakifkatilim.local/artifactory/api/nuget/${NUGET_SOURCE}-virtual-${BRANCH_NAME} --verbosity n --ignore-failed-sources
COPY . .
WORKDIR "/src/src/VK.Fx.DataFeed.ApiHost"
RUN dotnet build --no-restore "VK.Fx.DataFeed.ApiHost.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "VK.Fx.DataFeed.ApiHost.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

COPY ["src/VK.Fx.DataFeed.ApiHost/summon", "/app/summon"]
COPY ["src/VK.Fx.DataFeed.ApiHost/summon-conjur", "/app/summon-conjur"]
COPY ["src/VK.Fx.DataFeed.ApiHost/entrypoint.sh", "/app/entrypoint.sh"]
COPY ["src/VK.Fx.DataFeed.ApiHost/secrets.yml", "/app/secrets.yml"]

RUN chmod +x /app/summon
RUN chmod +x /app/summon-conjur
RUN chmod +x /app/entrypoint.sh
RUN chmod 644 /app/secrets.yml

ENTRYPOINT ["/bin/bash", "/app/entrypoint.sh"]