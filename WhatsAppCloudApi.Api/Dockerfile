FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["NuGet.config", "."]
COPY ["WhatsAppCloudApi.Api/WhatsAppCloudApi.Api.csproj", "WhatsAppCloudApi.Api/"]
COPY ["WhatsAppCloudApi.Application/WhatsAppCloudApi.Application.csproj", "WhatsAppCloudApi.Application/"]
COPY ["WhatsAppCloudApi.Infrastructure/WhatsAppCloudApi.Infrastructure.csproj", "WhatsAppCloudApi.Infrastructure/"]
COPY ["WhatsAppCloudApi.Domain/WhatsAppCloudApi.Domain.csproj", "WhatsAppCloudApi.Domain/"]
COPY ["WhatsAppCloudApi.Shared/WhatsAppCloudApi.Shared.csproj", "WhatsAppCloudApi.Shared/"]
RUN dotnet restore "WhatsAppCloudApi.Api/WhatsAppCloudApi.Api.csproj"
COPY . .
WORKDIR "/src/WhatsAppCloudApi.Api"
RUN dotnet build "WhatsAppCloudApi.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WhatsAppCloudApi.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WhatsAppCloudApi.Api.dll"]
